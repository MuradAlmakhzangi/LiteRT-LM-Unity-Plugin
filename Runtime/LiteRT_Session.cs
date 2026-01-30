using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;

public sealed class LiteRT_Session : IDisposable
{
    private static readonly ConcurrentDictionary<IntPtr, SessionState> _states = new();

    public static readonly SamplingParams DefaultSamplingParams = new();

    private sealed class SessionState
    {
        public Action<string> OnToken;
        public UniTaskCompletionSource<int> Completion;

        public readonly Queue<string> TokenQueue = new(128);
        public bool DrainScheduled;
        public readonly object Gate = new();
    }

    private readonly LiteRT_Engine _parentEngine;
    private readonly SemaphoreSlim _generationLock = new(1, 1);
    private IntPtr _handle;

    internal LiteRT_Session(LiteRT_Engine engine, IntPtr handle)
    {
        _parentEngine = engine;
        _handle = handle;
    }

    /// <summary>
    /// Streams tokens via onToken. Returns a completion code (0 = success).
    /// Throws on error codes; cancelled maps to task cancellation.
    /// </summary>
    public async UniTask<ResponseCode> GenerateTextAsync(string prompt, Action<string> onToken = null)
    {
        await _generationLock.WaitAsync();

        var state = new SessionState
        {
            OnToken = onToken,
            Completion = new UniTaskCompletionSource<int>()
        };

        _states[_handle] = state;

        try
        {
            litert_lm_native.generate_text_async(prompt, _handle, _tokenCallback, _finalCallback);

            var code = (ResponseCode)await state.Completion.Task;

            if (code == ResponseCode.kCancelled)
                throw new OperationCanceledException("Generation cancelled.");

            if (code != ResponseCode.kOk)
                throw new Exception($"Generation failed: {code}");

            return code;
        }
        finally
        {
            _generationLock.Release();
        }
    }

    public async UniTask<ResponseCode> SetSystemPromptAsync(string systemPrompt)
    {
        await _generationLock.WaitAsync();

        var state = new SessionState
        {
            OnToken = null,
            Completion = new UniTaskCompletionSource<int>()
        };

        _states[_handle] = state;

        try
        {
            int rc = litert_lm_native.prefill_system_prompt(
                _handle,
                systemPrompt,
                _finalCallback
            );

            if (rc != 0)
            {
                // startup failed, clean up immediately
                _states.TryRemove(_handle, out _);
                throw new Exception($"Prefill failed to start (rc={rc})");
            }

            ResponseCode code = (ResponseCode)await state.Completion.Task;

            if (code == ResponseCode.kCancelled)
            {
                throw new OperationCanceledException("Prefill cancelled.");
            }

            if (code != ResponseCode.kOk)
            {
                throw new Exception($"Prefill failed: {code}");
            }

            return code;
        }
        finally
        {
            _generationLock.Release();
        }
    }

    #region Native Callbacks

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void TokenCallback(IntPtr session, byte* data, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void FinalCallback(IntPtr session, int result);

    internal static readonly unsafe TokenCallback _tokenCallback = OnTokenCallback;
    internal static readonly FinalCallback _finalCallback = OnFinalCallback;

    [MonoPInvokeCallback(typeof(TokenCallback))]
    private static unsafe void OnTokenCallback(IntPtr sessionPtr, byte* data, int length)
    {
        if (!_states.TryGetValue(sessionPtr, out var state)) return;
        if (data == null || length <= 0) return;

        string token = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, length));

        bool schedule = false;
        lock (state.Gate)
        {
            state.TokenQueue.Enqueue(token);
            if (!state.DrainScheduled)
            {
                state.DrainScheduled = true;
                schedule = true;
            }
        }

        if (schedule)
        {
            // calls safe method that does the await
            ScheduleDrainOnMainThread(sessionPtr, state).Forget();
        }
    }

    private static async UniTaskVoid ScheduleDrainOnMainThread(IntPtr sessionPtr, SessionState state)
    {
        await UniTask.SwitchToMainThread();

        while (true)
        {
            string next;
            Action<string> cb;

            lock (state.Gate)
            {
                if (state.TokenQueue.Count == 0)
                {
                    state.DrainScheduled = false;
                    return;
                }
                next = state.TokenQueue.Dequeue();
                cb = state.OnToken;
            }

            cb?.Invoke(next);
        }
    }

    [MonoPInvokeCallback(typeof(FinalCallback))]
    private static void OnFinalCallback(IntPtr sessionPtr, int result)
    {
        if (!_states.TryRemove(sessionPtr, out var state)) return;
        state.Completion.TrySetResult(result);
    }
    #endregion

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;

        _generationLock.Wait();
        try
        {
            if (_parentEngine.Handle == IntPtr.Zero) {
                throw new ObjectDisposedException("Session's parent engine has been destroyed before being disposed of");
            }

            // Only cancel if a request is actually in-flight.
            if (_states.ContainsKey(_handle)) {
                litert_lm_native.cancel_generation(_handle);
            }

            _parentEngine.WaitUntilDone(10000);

            litert_lm_native.destroy_session(_handle);
            _handle = IntPtr.Zero;
        }
        finally
        {
            _generationLock.Release();
        }
    }
}
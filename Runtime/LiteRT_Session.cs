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
    }

    private readonly LiteRT_Engine _parentEngine;
    // removed semaphore as requested
    private IntPtr _handle;
    private int _disposed; // 0 == false, 1 == true

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
        if(_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(LiteRT_Session));
        if(Interlocked.CompareExchange(ref _disposed, 0, 0) == 1) throw new ObjectDisposedException(nameof(LiteRT_Session));
    
        var state = new SessionState {
            OnToken = onToken,
            Completion = new UniTaskCompletionSource<int>()
        };
    
        _states[_handle] = state;
    
        litert_lm_native.generate_text_async(prompt, _handle, _tokenCallback, _finalCallback);

        int codeInt = await state.Completion.Task;
    
        // continuation from Completion can be on native thread
        await UniTask.SwitchToMainThread();
    
        var code = (ResponseCode)codeInt;
    
        if(code == ResponseCode.kCancelled) throw new OperationCanceledException("Generation cancelled.");
        if(code != ResponseCode.kOk) throw new Exception($"Generation failed: {code}");
        return code;
    }
    
    public async UniTask<ResponseCode> SetSystemPromptAsync(string systemPrompt)
    {
        if(_handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(LiteRT_Session));
        if(Interlocked.CompareExchange(ref _disposed, 0, 0) == 1) throw new ObjectDisposedException(nameof(LiteRT_Session));
    
        var state = new SessionState {
            OnToken = null,
            Completion = new UniTaskCompletionSource<int>()
        };
    
        _states[_handle] = state;
    
        int rc = litert_lm_native.prefill_system_prompt(_handle, systemPrompt, _finalCallback);
        if(rc != 0) {
            _states.TryRemove(_handle, out _);
            throw new Exception($"Prefill failed to start (rc={rc})");
        }

        int codeInt = await state.Completion.Task;
    
        await UniTask.SwitchToMainThread();
    
        var code = (ResponseCode)codeInt;
        if(code == ResponseCode.kCancelled) throw new OperationCanceledException("Prefill cancelled.");
        if(code != ResponseCode.kOk) throw new Exception($"Prefill failed: {code}");
        return code;
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
        // quick guards to avoid processing if we don't have state
        if (sessionPtr == IntPtr.Zero) return;
        if (data == null || length <= 0) return;
        if (!_states.TryGetValue(sessionPtr, out _)) return;

        // copy token bytes to managed string (cheap)
        string token = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, length));

        // Fire-and-forget a safe async method that switches to main thread and invokes callback.
        // We intentionally do NOT await inside the native callback; we immediately schedule the work.
        // Using .Forget() is OK here — the token delivery is best-effort; the Completion/Dispose logic
        // handles race conditions separately.
        ProcessTokenAsync(sessionPtr, token).Forget();
    }

    private static async UniTask ProcessTokenAsync(IntPtr sessionPtr, string token)
    {
        // switch to main thread
        await UniTask.SwitchToMainThread();

        // Get a snapshot of the OnToken delegate in case state changed concurrently.
        Action<string> cb = null;
        if (_states.TryGetValue(sessionPtr, out var curState))
        {
            cb = curState.OnToken;
        }

        try
        {
            cb?.Invoke(token);
        }
        catch (Exception ex)
        {
            // don't let user exceptions bubble into UniTask machinery unobserved.
            UnityEngine.Debug.LogException(ex);
        }
    }

    [MonoPInvokeCallback(typeof(FinalCallback))]
    private static void OnFinalCallback(IntPtr sessionPtr, int result)
    {
        if (sessionPtr == IntPtr.Zero) return;

        if (!_states.TryRemove(sessionPtr, out var state)) return;

        // complete the awaiting task
        state.Completion.TrySetResult(result);
    }
    #endregion

    public void Dispose()
    {
        // idempotent dispose
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        if (_handle == IntPtr.Zero) return;

        // best-effort: cancel generation if active
        try
        {
            if (_states.ContainsKey(_handle))
            {
                litert_lm_native.cancel_generation(_handle);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"Exception while cancelling generation: {ex}");
        }

        // wait for engine to finish any threads it needs to (keeps previous behavior)
        try
        {
            _parentEngine.WaitUntilDone(10000);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"WaitUntilDone threw: {ex}");
        }

        try
        {
            litert_lm_native.destroy_session(_handle);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"destroy_session threw: {ex}");
        }
        finally
        {
            _handle = IntPtr.Zero;
        }
    }
}
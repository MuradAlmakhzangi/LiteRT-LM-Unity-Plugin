using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AOT;
using Cysharp.Threading.Tasks;

public sealed class LiteRT_Session : IDisposable
{
    private static readonly Dictionary<IntPtr, SessionState> _states = new();

    public static readonly SamplingParams DefaultParams = new SamplingParams()
    {
        TopK = 40,
        TopP = 0.9f,
        Seed = 0,
        Temperature = 1,
    };

    private class SessionState
    {
        public Action<string> OnToken;
        public TaskCompletionSource<string> Completion;
    }

    private readonly LiteRT_Engine _parentEngine;
    private SamplingParams _SamplingParams;  // not readonly so we can pass ref
    private readonly SemaphoreSlim _generationLock = new SemaphoreSlim(1, 1);

    private IntPtr _handle;

    internal LiteRT_Session(LiteRT_Engine engine, IntPtr handle, SamplingParams SamplingParams)
    {
        _parentEngine = engine;
        _SamplingParams = SamplingParams;
        _handle = handle;
    }

    /// <summary>
    /// Generate text normally, respecting EOS.
    /// </summary>
    public async UniTask<string> GenerateTextAsync(string prompt, Action<string> onToken = null)
    {
        await _generationLock.WaitAsync();

        try
        {
            var tcs = new TaskCompletionSource<string>();
            _states[_handle] = new SessionState { OnToken = onToken, Completion = tcs };

            litert_lm_native.generate_text_async(
                prompt,
                _handle,
                _tokenCallback,
                _finalCallback
            );

            return await tcs.Task;
        }
        finally
        {
            _states.Remove(_handle);
            _generationLock.Release();
        }
    }

    public int NumberOfTokens(string prompt)
    {
        return litert_lm_native.number_of_tokens(prompt, _parentEngine.Handle);
    }

    #region Native Callbacks

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void TokenCallback(IntPtr session, IntPtr token);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FinalCallback(IntPtr session, IntPtr result);

    private static readonly TokenCallback _tokenCallback = OnTokenCallback;
    private static readonly FinalCallback _finalCallback = OnFinalCallback;

    [MonoPInvokeCallback(typeof(TokenCallback))]
    private static void OnTokenCallback(IntPtr sessionPtr, IntPtr tokenPtr)
    {
        if (!_states.TryGetValue(sessionPtr, out var state)) return;

        string token = Marshal.PtrToStringUTF8(tokenPtr);
        UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            state.OnToken?.Invoke(token);
        });
    }

    [MonoPInvokeCallback(typeof(FinalCallback))]
    private static void OnFinalCallback(IntPtr sessionPtr, IntPtr resultPtr)
    {
        if (!_states.TryGetValue(sessionPtr, out var state)) return;

        string result = Marshal.PtrToStringUTF8(resultPtr);
        _states.Remove(sessionPtr);

        UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            if (result == "CANCELLED"){
                state.Completion.TrySetCanceled();
            }
            else if (result.StartsWith("ERROR")){
                state.Completion.TrySetException(new Exception($"Generation failed, {result}"));
            }
            else{
                state.Completion.TrySetResult(result);
            }
        });
    }

    #endregion

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;

        if(_parentEngine.Handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException("Session's parent engine has been destroyed before being disposed of");
        } 

        litert_lm_native.cancel_generation(_handle); // safe call, nothing happens if already invoked

        // Free the session immediately. When decode loop checks flag,
        // it will call OnError -> which is mapped to TrySetCanceled().
        litert_lm_native.destroy_session(_handle);

        _handle = IntPtr.Zero;
    }

    public async UniTask SetSystemPrompt(string systemPrompt)
    {
        var tcs = new TaskCompletionSource<string>();
        _states[_handle] = new SessionState { OnToken = null, Completion = tcs };

        int rc = litert_lm_native.prefill_system_prompt(
            _handle,
            systemPrompt,
            _finalCallback  // reuse same callback as generation
        );

        Dictionary<int, string> responseCodeToError = new()
        {
            {-1, "Empty system prompt"},
            {-2, "Invalid session pointer"},
            {-3, "Failed to start"}
        };
        if (rc != 0)
        {
            throw new Exception($"Prefill system prompt failed: {responseCodeToError[rc]}");
        }

        string result = await tcs.Task;
        if (result == "ERROR")
        {
            throw new Exception("System prompt prefill failed");
        }
    }
}
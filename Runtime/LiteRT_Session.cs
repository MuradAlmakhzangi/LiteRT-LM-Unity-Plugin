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

    public static readonly SessionParams DefaultParams = new SessionParams()
    {
        TopK = 40,
        TopP = 0.9f,
        Seed = 0,
        Temperature = 1,
        BatchSize = 32,
    };

    private class SessionState
    {
        public Action<string> OnToken;
        public TaskCompletionSource<string> Completion;
    }

    private readonly LiteRT_Engine _parentEngine;
    private SessionParams _sessionParams;  // not readonly so we can pass ref
    private readonly SemaphoreSlim _generationLock = new SemaphoreSlim(1, 1);

    private IntPtr _handle;

    internal LiteRT_Session(LiteRT_Engine engine, IntPtr handle, SessionParams sessionParams)
    {
        _parentEngine = engine;
        _sessionParams = sessionParams;
        _handle = handle;
    }

    /// <summary>
    /// Generate text normally, respecting EOS.
    /// </summary>
    public async UniTask<string> GenerateTextAsync(string prompt, Action<string> onToken = null)
    {
        return await RunGeneration(prompt, ignoreEOS: false, maxTokens: -1, onToken);
    }

    /// <summary>
    /// Generate text ignoring EOS, up to maxTokens.
    /// </summary>
    public async UniTask<string> GenerateTextAsyncIgnoreEOS(string prompt, int maxTokens, Action<string> onToken = null)
    {
        return await RunGeneration(prompt, ignoreEOS: true, maxTokens: maxTokens, onToken);
    }

    private async UniTask<string> RunGeneration(string prompt, bool ignoreEOS, int maxTokens, Action<string> onToken)
    {
        await _generationLock.WaitAsync();

        try
        {
            var tcs = new TaskCompletionSource<string>();
            _states[_handle] = new SessionState { OnToken = onToken, Completion = tcs };

            litert_lm_native.generate_text_async(
                prompt,
                _handle,
                ignoreEOS,
                maxTokens,
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
            if (result == "CANCELLED")
                state.Completion.TrySetCanceled();
            else if (result == "ERROR")
                state.Completion.TrySetException(new Exception("Generation failed"));
            else
                state.Completion.TrySetResult(result);
        });
    }

    #endregion

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;

        // Trigger cancellation — decode loop will bail
        litert_lm_native.cancel_generation(_handle);

        // Free the session immediately. When decode loop checks flag,
        // it will call OnError -> which you mapped to TrySetCanceled().
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

        if (rc != 0)
        {
            throw new Exception($"Prefill system prompt failed: {rc}");
        }

        string result = await tcs.Task;
        if (result == "ERROR")
        {
            throw new Exception("System prompt prefill failed");
        }
    }
}
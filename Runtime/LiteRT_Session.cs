
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    private IntPtr _handle;
    private readonly LiteRT_Engine _parentEngine; // this and batch size not currently in use, keeping if modifications to be made
    private SessionParams _sessionParams;

    internal LiteRT_Session(LiteRT_Engine engine, IntPtr handle, SessionParams sessionParams)
    {
        _handle = handle;
        _parentEngine = engine;
        _sessionParams = sessionParams;
    }

    // SEE: Max token as a parameter caused a crash on generation, omitted for regular completions where EOS is not ignored
    public async UniTask<string> GenerateTextAsync(string prompt, Action<string> onToken = null)
    {
        var tcs = new TaskCompletionSource<string>();

        _states[_handle] = new SessionState
        {
            OnToken = onToken,
            Completion = tcs
        };

        litert_lm_native.generate_text_async(prompt, _handle, false, -1, _tokenCallback, _finalCallback);
        return await tcs.Task;
    }

    
    public async UniTask<string> GenerateTextAsyncIgnoreEOS(string prompt, int maxTokens, Action<string> onToken = null)
    {
        UnityEngine.Debug.Log($"Generating, have params of batch {_sessionParams.BatchSize}, topk {_sessionParams.TopK}");
        var tcs = new TaskCompletionSource<string>();

        _states[_handle] = new SessionState
        {
            OnToken = onToken,
            Completion = tcs
        };

        litert_lm_native.generate_text_async(prompt, _handle, true, maxTokens, _tokenCallback, _finalCallback);
        return await tcs.Task;
    }

    public string GenerateTextSync(string prompt)
    {
        byte[] buffer = new byte[4096];
        int status = litert_lm_native.generate_text_sync_buffer(prompt, _handle, buffer, buffer.Length);
        if (status != 0)
        {
            throw new Exception($"Text generation failed: code {status}");
        }

        int len = Array.IndexOf(buffer, (byte)0);
        if (len < 0) len = buffer.Length;
        return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            litert_lm_native.destroy_session(_handle);
            _states.Remove(_handle);
            _handle = IntPtr.Zero;
        }
    }

    public int NumberOfTokens(string prompt)
    {
        return litert_lm_native.number_of_tokens(prompt, _parentEngine.Handle);
    }

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
            state.Completion.TrySetResult(result);
        });
    }
}

using System;
using System.Runtime.InteropServices;

internal static class litert_lm_native
{
    private const string name = "litert_lm_unity";

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int create_engine(
        string model_path,
        int numThreads,
        int batchSize,
        bool clearCacheOnPrefill,
        int benchmarkPrefillTokenCount,
        int benchmarkDecodeTokenCount,
        out IntPtr engine);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int create_session(
        IntPtr engine,
        out IntPtr out_session,
        int maxOutputTokens,
        ref SamplingParams sessionParams);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int generate_text_sync_buffer(
        string input,
        IntPtr session,
        byte[] outputBuffer,
        int bufferSize);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void generate_text_async(
        string input,
        IntPtr session,
        LiteRT_Session.TokenCallback onToken,
        LiteRT_Session.FinalCallback onFinal);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void destroy_engine(IntPtr engine);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void destroy_session(IntPtr session);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void cancel_generation(IntPtr session);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int prefill_system_prompt(
        IntPtr session,
        string systemPrompt,
        LiteRT_Session.FinalCallback onFinal);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void set_min_logging_level(int level);

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void disable_logging();

    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int wait_until_done(IntPtr engine, int timeoutMs);
}
using System;
using System.Runtime.InteropServices;

internal static class litert_lm_native
{
    private const string LibraryName = "litert_lm_unity";
    private const CallingConvention NativeCallingConvention = CallingConvention.Cdecl;

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern int create_engine(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string model_path,
        int numThreads,
        int batchSize,
        bool clearCacheOnPrefill,
        int benchmarkPrefillTokenCount,
        int benchmarkDecodeTokenCount,
        out IntPtr engine);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern int create_session(
        IntPtr engine,
        out IntPtr out_session,
        int maxOutputTokens,
        ref SamplingParams sessionParams);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern int create_conversation(
        IntPtr engine,
        out IntPtr out_conversation,
        ref SamplingParams samplingParams,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string systemPrompt,
        int maxOutputTokens,
        bool prefillSystemPromptOnInit,
        bool enableThinking);
    
    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void generate_text_async(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string input,
        IntPtr session,
        GenerationContext.TokenCallback onToken,
        GenerationContext.FinalCallback onFinal);
    
    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void send_message_async(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string input,
        IntPtr conversation,
        GenerationContext.TokenCallback onToken,
        GenerationContext.FinalCallback onFinal
    );

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void destroy_engine(IntPtr engine);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void destroy_session(IntPtr session);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void destroy_conversation(IntPtr conversation);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void cancel_session_generation(IntPtr session);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void cancel_conversation_generation(IntPtr conversation);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern int prefill_system_prompt(
        IntPtr session,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string systemPrompt,
        GenerationContext.FinalCallback onFinal);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void set_min_logging_level(int level);

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern void disable_logging();

    [DllImport(LibraryName, CallingConvention = NativeCallingConvention, ExactSpelling = true)]
    internal static extern int wait_until_done(IntPtr engine, int timeoutMs);
}

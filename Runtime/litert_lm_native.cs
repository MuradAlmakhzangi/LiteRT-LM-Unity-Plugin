using System;
using System.Runtime.InteropServices;

internal static class litert_lm_native
{
    private const string name = "litert_lm_llm_unity";


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int create_engine(string modelPath, int numThreads, out IntPtr out_engine, int maxnNumTokens);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int create_session(IntPtr engine, out IntPtr out_session, ref SessionParams sessionParams);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int generate_text_sync_buffer(string input, IntPtr session, byte[] outputBuffer, int bufferSize);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void generate_text_async(string input, IntPtr session, bool ignoreEOS, int maxnNumTokens,
                                                    LiteRT_Session.TokenCallback onToken,
                                                    LiteRT_Session.FinalCallback onFinal);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void destroy_engine(IntPtr engine);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void destroy_session(IntPtr session);


    [DllImport(name, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int number_of_tokens(string input, IntPtr engine);
}

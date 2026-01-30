using System;
using System.Collections.Generic;

public sealed class LiteRT_Engine : IDisposable
{
    internal IntPtr Handle { get; private set; }
    private readonly static Dictionary<int, string> createEngineResponseToErrorMessage = new()
    {
        {-1, "Error creating engine"},
        {-2, "Error loading model file"},
        {-3, "Error creating `engine_settings` object"},
        {-4, "Error accessing cpu config to set thread count"}
    };

    /// <summary>
    /// Load a LiteRT-LM llm, use this instead of the constructor to instantiate a LiteRT_Engine
    /// </summary>
    /// <param name="modelPath">Path to the .litertlm model file</param>
    /// <param name="numThreads">Number of cpu threads to use</param>
    /// <param name="batchSize">Prefill batch size to use on inference, bound to the engine object by LiteRT-LM</param>
    /// <param name="clearCacheOnPrefill">Clear cache on prefill, usefull for debugging/benchmarking</param>
    /// <param name="benchmarkTokenDecodeCount">Force a token count to be reached on generations, used for tg128 benchmarking</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static LiteRT_Engine Load(string modelPath, int numThreads, int batchSize, bool clearCacheOnPrefill = false, int benchmarkTokenPrefillCount = -1, int benchmarkTokenDecodeCount = -1)
    {
        int result = litert_lm_native.create_engine(modelPath, numThreads,
                                                    batchSize, clearCacheOnPrefill,
                                                    benchmarkTokenPrefillCount, benchmarkTokenDecodeCount, 
                                                    out var engine);

        
        if (result != 0)
        {
            throw new Exception($"Engine setup failed: {createEngineResponseToErrorMessage[result]}");
        }
        return new LiteRT_Engine(engine);
    }

    private LiteRT_Engine(IntPtr handle)
    {
        Handle = handle;
    }

    public LiteRT_Session CreateSession(SamplingParams SamplingParams, int maxOutputTokens)
    {
        int result = litert_lm_native.create_session(Handle, out var session, maxOutputTokens, ref SamplingParams);
        if (result != 0)
        {
            throw new Exception($"Session creation failed with code {result}");
        }
        return new LiteRT_Session(this, session);
    }

    public LiteRT_Session CreateSession(SamplingParams SamplingParams)
    {
        return CreateSession(SamplingParams, -1);
    }

    /// <summary>
    /// Creates a session with the default parameters
    /// </summary>
    public LiteRT_Session CreateSession()
    {
        return CreateSession(LiteRT_Session.DefaultSamplingParams, -1);
    }

    public void WaitUntilDone(int timeoutMs = 10000)
    {
        if (Handle == IntPtr.Zero) return;
        litert_lm_native.wait_until_done(Handle, timeoutMs);
    }
    
    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            litert_lm_native.destroy_engine(Handle);
            Handle = IntPtr.Zero;
        }
    }

}
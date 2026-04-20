using System;
using System.Collections.Generic;

public sealed class Engine : IDisposable
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
    public static Engine Load(string modelPath, int numThreads, int batchSize, bool clearCacheOnPrefill = false, int benchmarkTokenPrefillCount = -1, int benchmarkTokenDecodeCount = -1)
    {
        int result = litert_lm_native.create_engine(modelPath, numThreads,
                                                    batchSize, clearCacheOnPrefill,
                                                    benchmarkTokenPrefillCount, benchmarkTokenDecodeCount, 
                                                    out var engine);

        
        if (result != 0)
        {
            throw new Exception($"Engine setup failed: {createEngineResponseToErrorMessage[result]}");
        }
        return new Engine(engine);
    }

    private Engine(IntPtr handle)
    {
        Handle = handle;
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

using System;

public sealed class LiteRT_Engine : IDisposable
{
    internal IntPtr Handle { get; private set; }

    public static LiteRT_Engine Load(string modelPath, int numThreads, int maxnNumTokens)
    {
        int result = litert_lm_native.create_engine(modelPath, numThreads, out var engine, maxnNumTokens);
        if (result != 0)
        {
            throw new Exception($"Engine setup failed with code {result}");
        }
        return new LiteRT_Engine(engine);
    }
    public static LiteRT_Engine Load(string modelPath, int numThreads)
    {
        return Load(modelPath, numThreads, -1);
    }

    private LiteRT_Engine(IntPtr handle)
    {
        Handle = handle;
    }

    public LiteRT_Session CreateSession(SessionParams sessionParams)
    {
        int result = litert_lm_native.create_session(Handle, out var session, ref sessionParams);
        if (result != 0)
        {
            throw new Exception($"Session creation failed with code {result}");
        }
        return new LiteRT_Session(this, session, sessionParams);
    }

    // Creates a session with the default batch size for LiteRT, (Seems to default to the whole prompt?)
    // This is more efficient in certain cases, especiially when compared to low batch sizes, as this does only 1 allocation for the buffers
    // Where any other batch size has to re-allocate for every token
    public LiteRT_Session CreateSession()
    {
        return CreateSession(LiteRT_Session.DefaultParams);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            litert_lm_native.destroy_engine(Handle);
            Handle = IntPtr.Zero;
        }
    }

    public int NumberOfTokens(string text)
    {
        return litert_lm_native.number_of_tokens(text, Handle);
    }
}
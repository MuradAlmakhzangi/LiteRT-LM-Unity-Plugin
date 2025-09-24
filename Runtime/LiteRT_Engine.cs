using System;

public sealed class LiteRT_Engine : IDisposable
{
    internal IntPtr Handle { get; private set; }

    public static LiteRT_Engine Load(string modelPath, int numThreads)
    {
        // Max num tokens is a parameter from a earlier test, no longer works, but simpler to keep as is
        int result = litert_lm_native.create_engine(modelPath, numThreads, out var engine, -1);
        if (result != 0)
        {
            throw new Exception($"Engine setup failed with code {result}");
        }
        return new LiteRT_Engine(engine);
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

    /// <summary>
    /// Creates a session with the default parameters
    /// </summary>
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
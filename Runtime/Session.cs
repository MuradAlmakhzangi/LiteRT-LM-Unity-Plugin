using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class Session : GenerationContext
{
    public static readonly SamplingParams DefaultSamplingParams = new();

    private Session(Engine engine, IntPtr handle)
        : base(engine, handle, nameof(Session))
    { }

    /// <summary>
    /// Streams tokens via onToken. Returns a completion code (0 = success).
    /// Throws on error codes; cancelled maps to task cancellation.
    /// </summary>
    public UniTask<ResponseCode> GenerateTextAsync(
        string prompt,
        Action<string> onToken = null,
        CancellationToken cancellationToken = default)
    {
        return RunStreamingOperationAsync(
            (handle, tokenCallback, finalCallback) =>
                litert_lm_native.generate_text_async(prompt, handle, tokenCallback, finalCallback),
            onToken,
            cancellationToken);
    }

    public UniTask<ResponseCode> SetSystemPromptAsync(string systemPrompt)
    {
        return RunCompletionOperationAsync(
            (handle, finalCallback) => litert_lm_native.prefill_system_prompt(handle, systemPrompt, finalCallback));
    }

    protected override bool SupportsCancellation => true;

    protected override void DestroyHandle(IntPtr handle)
    {
        litert_lm_native.destroy_session(handle);
    }

    public static Session Create(Engine engine, SamplingParams SamplingParams, int maxOutputTokens)
    {
        int result = litert_lm_native.create_session(engine.Handle, out var session, maxOutputTokens, ref SamplingParams);
        if (result != 0)
        {
            throw new Exception($"Session creation failed with code {result}");
        }
        return new Session(engine, session);
    }

    public static Session Create(Engine engine, SamplingParams SamplingParams)
    {
        return Create(engine, SamplingParams, -1);
    }

    /// <summary>
    /// Creates a session with the default parameters
    /// </summary>
    public static Session Create(Engine engine)
    {
        return Create(engine, DefaultSamplingParams, -1);
    }
}

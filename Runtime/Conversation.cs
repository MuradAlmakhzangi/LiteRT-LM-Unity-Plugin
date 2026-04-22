using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class Conversation : GenerationContext
{
    private Conversation(Engine engine, IntPtr handle)
        : base(engine, handle, nameof(Conversation))
    { }

    public UniTask<ResponseCode> SendMessageAsync(
        string message,
        Action<string> onToken = null,
        CancellationToken cancellationToken = default)
    {
        return RunStreamingOperationAsync(
            (handle, tokenCallback, finalCallback) =>
                litert_lm_native.send_message_async(message, handle, tokenCallback, finalCallback),
            onToken,
            cancellationToken);
    }

    protected override void DestroyHandle(IntPtr handle)
    {
        litert_lm_native.destroy_conversation(handle);
    }


    public static Conversation Create(
        Engine engine,
        SamplingParams samplingParams,
        string systemPrompt = "",
        int maxOutputTokens = -1,
        bool prefillSystemPromptOnInit = false)
    {
        int result = litert_lm_native.create_conversation(
            engine.Handle,
            out var conversation,
            ref samplingParams,
            systemPrompt ?? string.Empty,
            maxOutputTokens,
            prefillSystemPromptOnInit);

        if (result != 0)
        {
            throw new Exception($"Conversation creation failed with code {result}");
        }

        return new Conversation(engine, conversation);
    }

    public static Conversation Create(
        Engine engine,
        string systemPrompt = "",
        int maxOutputTokens = -1,
        bool prefillSystemPromptOnInit = true)
    {
        return Create(engine, Session.DefaultSamplingParams, systemPrompt, maxOutputTokens, prefillSystemPromptOnInit);
    }
}

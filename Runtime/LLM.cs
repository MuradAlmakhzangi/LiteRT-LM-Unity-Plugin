using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Interface for interaction with the LLM, uses a Conversation as the main API for interaction
/// </summary>
public class LLM : IDisposable
{    
    private Engine _engine;
    private Conversation _conversation;

    private readonly string _systemPrompt;
    private readonly SamplingParams _samplingParams;
    private readonly int _maxOutputTokens;
    private readonly bool _prefillSystemPromptOnInit;
    private readonly bool _enableThinking;

    public LLM(string systemPrompt,
               SamplingParams samplingParams,
               int maxOutputTokens,
               bool prefillSystemPromptOnInit,
               bool enableThinking)
    {
        _systemPrompt = systemPrompt;
        _samplingParams = samplingParams;
        _maxOutputTokens = maxOutputTokens;
        _prefillSystemPromptOnInit = prefillSystemPromptOnInit;
        _enableThinking = enableThinking; 
    }    

    public LLM(string systemPrompt) : this(systemPrompt, Session.DefaultSamplingParams, -1, true, false)
    {}


    /// <summary>
    /// Load the .litertlm model from /StreamingAssets/
    /// </summary>
    /// <param name="filepath">Relative filepath from /StreamingAssets/</param>
    /// <param name="threads">Thread count for the LLM</param>
    /// <param name="batches">Prefill batch size</param>
    /// <param name="benchmarkPrefillCount">Number of prefill tokens for benchmarking</param>
    /// <param name="benchmarkDecodeTokenCount">Number of decode tokens for benchmarking</param>
    /// <returns></returns>
    public async UniTask LoadFromStreamingAssets(string filepath, int threads, int batches, int benchmarkPrefillCount = -1, int benchmarkDecodeTokenCount = -1)
    {
        this.Dispose(); // Ensure safety when reloading
        string modelPath = await ResolveModelPath(filepath);

        _engine = Engine.Load(
            modelPath,
            threads,
            batches,
            false,
            benchmarkPrefillCount,
            benchmarkDecodeTokenCount
        );
        
        _conversation = Conversation.Create(
            _engine,
            _samplingParams,
            _systemPrompt,
            _maxOutputTokens,
            _prefillSystemPromptOnInit,
            _enableThinking
        );
    }

    public static async UniTask<string> ResolveModelPath(string filePath)
    {
        string src = Path.Combine(Application.streamingAssetsPath, filePath);

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android's streaming assets path returns a url that must be extracted to the apk
        string dest = Path.Combine(Application.persistentDataPath, filePath);

        if (!Directory.Exists(Path.GetDirectoryName(dest)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
        }

        if (!File.Exists(dest))
        {
            var www = new WWW(src);
            while (!www.isDone) await UniTask.Yield();
            File.WriteAllBytes(dest, www.bytes);
        }

        return dest;
#else
        await UniTask.Yield(); // no longer truly asynchronous in editor
        return src;
#endif
    }

    public void Dispose()
    {
        _conversation?.Dispose();
        _conversation = null;

        _engine?.Dispose();
        _engine = null;
    }

    public async UniTask<string> RunConversation(
        string prompt,
        Action<string> onToken = null,
        CancellationToken ct = default)
    {
        StringBuilder responseBuilder = new();
        await _conversation.SendMessageAsync(prompt, (string token) =>
        {
            onToken?.Invoke(token);
            responseBuilder.Append(token);
        }, ct);
        string result = responseBuilder.ToString();
        return result;
    }
}

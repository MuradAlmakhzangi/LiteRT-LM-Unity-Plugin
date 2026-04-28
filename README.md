# LiteRT-LM-Unity-Plugin

## Setup:
1. Import UniTask (dependancy of this project) in unity using their git url of `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`
2. Import in Unity using git url of `https://github.com/MuradAlmakhzangi/LiteRT-LM-Unity-Plugin.git`
3. Add the model's `.litertlm` file in <StreamingAssets> 
4. Add the dynamic library in <Plugins/Android/arm-v8a>


## Startup Guide:
```cs
using UnityEngine;


public class Test : MonoBehaviour
{
    [SerializeField] private int _threadCount;
    [SerializeField] private int _batchSize = 32;
    [SerializeField] private const string _modelPath;
    [SerializeField, TextArea] private string _systemPrompt;

    private LLM _llm;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        _llm = new LLM(_systemPrompt);
        await _llm.LoadFromStreamingAssets(_modelPath, _threadCount, _batchSize);

        await _llm.RunConversation("Hello", (string token) =>
        {
            Debug.Log(token);
        });
    }

    private void OnApplicationQuit()
    {
        _llm.Dispose();
    }
}
```

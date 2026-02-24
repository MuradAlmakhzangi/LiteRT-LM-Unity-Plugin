using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct SamplingParams
{
    public int TopK;
    public float TopP;
    public int Seed;
    public float Temperature;
    public SamplingType SamplingType;

    public SamplingParams(
        int topK = 40,
        float topP = 0.9f,
        int seed = 0,
        float temperature = 1,
        SamplingType samplingType = SamplingType.Default
        )
    {
        TopK = topK;
        TopP = topP;
        Seed = seed;
        Temperature = temperature;
        SamplingType = samplingType;
    }
}
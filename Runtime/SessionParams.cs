using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SessionParams
{
    public int TopK;
    public float TopP;
    public int Seed;
    public int Temperature;

    public SessionParams(
        int topK = 40,
        float topP = 0.9f,
        int seed = 0,
        int temperature = 1
        )
    {
        TopK = topK;
        TopP = topP;
        Seed = seed;
        Temperature = temperature;
    }
}
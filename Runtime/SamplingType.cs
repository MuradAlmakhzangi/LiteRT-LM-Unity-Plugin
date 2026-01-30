public enum SamplingType : int
{
    Default = -1, // Default to whatever LiteRT-LM considers the default
    Unspesified = 0,
    Top_K = 1,
    Top_P = 2,
    Greedy = 3
}

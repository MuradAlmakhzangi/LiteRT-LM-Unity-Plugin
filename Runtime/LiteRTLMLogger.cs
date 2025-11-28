public static class LiteRTLMLogger
{
    /// <summary>
    /// Sets the minimum log level for the native plugins
    /// </summary>
    /// <param name="level">enum representing the logging level</param>
    public static void SetMinLogLevel(LogLevel level)
    {
        litert_lm_native.set_min_logging_level((int) level);
    }

    /// <summary>
    /// Disables logging from the native library, equivalent to <c>SetMinLogLevel(LogLevel.None)</c>;
    /// </summary>
    public static void DisableLogging()
    {
        litert_lm_native.disable_logging();
    }
}
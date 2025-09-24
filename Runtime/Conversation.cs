using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class ChatSession
{
    private readonly List<ChatMessage> _messages = new();

    public void AddMessage(MessageRole role, string content)
    {
        _messages.Add(new ChatMessage(role, content));
    }

    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Adds correct formatting for multi-message conversations
    /// being sent as a single prompt to the LLM
    /// </summary>
    public string BuildPrompt()
    {
        var sb = new StringBuilder();
        foreach (var msg in _messages)
        {
            if (msg.Role == MessageRole.User)
                sb.AppendLine($"<|user|>\n{msg.Content}\n");
            else if (msg.Role == MessageRole.Assistant)
                sb.AppendLine($"<|assistant|>\n{msg.Content}\n");
            else if (msg.Role == MessageRole.System)
                sb.AppendLine($"<|system|>\n{msg.Content}\n");
        }
        sb.Append("<|assistant|>\n"); // cue model to respond
        return sb.ToString();
    }
}
public enum MessageRole { System, User, Assistant };
public record ChatMessage(MessageRole Role, string Content);


namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}
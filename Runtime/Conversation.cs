using System.Collections.Generic;
using System.Text;

public class Conversation
{
    private readonly List<Message> _messages = new();

    public void AddMessage(MessageRole role, string content)
    {
        _messages.Add(new Message(role, content));
    }

    public void Clear()
    {
        _messages.Clear();
    }

    public string BuildPrompt()
    {
        var sb = new StringBuilder();
        foreach (Message msg in _messages)
        {
            string prefix = msg.Role switch
            {
                MessageRole.System => "System: ",
                MessageRole.User => "User: ",
                MessageRole.Assistant => "Assistant: ",
                _ => ""
            };
            sb.AppendLine($"{prefix}{msg.Content}");
        }

        sb.Append("Assistant: "); // cue for next turn
        return sb.ToString();
    }
}
public enum MessageRole { System, User, Assistant };
public record Message(MessageRole Role, string Content);


namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}
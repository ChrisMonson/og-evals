namespace Og.Core.Runtime;

/// <summary>Where a session's text goes.</summary>
public interface IOutputSink
{
    void Write(string message);
}

public sealed class CollectingOutputSink : IOutputSink
{
    public List<string> Lines { get; } = [];

    public void Write(string message) => Lines.Add(message);

    public string Text => string.Join("\n", Lines);

    public void Clear() => Lines.Clear();
}

namespace LittleGuy3000.Core;

public sealed record Annotation(string Shape, double X, double Y, double Width, double Height, string Label);
public sealed record AssistantAnswer(string Answer, string? CaptureId, List<Annotation>? Annotations, string? StepStatus, string? Summary = null, ReplyBatch? Replies = null, ResearchReport? Research = null);

public static class AnnotationValidator
{
    public static IReadOnlyList<Annotation> Validate(AssistantAnswer answer, ScreenSnapshot snapshot, DateTimeOffset now, PixelRect currentBounds)
    {
        if (answer.CaptureId != snapshot.Id || now - snapshot.CapturedAt > TimeSpan.FromSeconds(30)
            || currentBounds != snapshot.Window.Bounds || !snapshot.Transform.IsValid) return [];
        var commands = answer.Annotations ?? [];
        if (commands.Count > 6) return [];
        return commands.Where(a => a is not null && (a.Shape is "ring" or "arrow")
            && a.Label is { Length: <= 80 }
            && new PixelRect(a.X, a.Y, a.Width, a.Height).IsValid
            && a.Width * a.Height <= snapshot.Transform.ImageWidth * (double)snapshot.Transform.ImageHeight * 0.25
            && a.X >= 0 && a.Y >= 0 && a.X + a.Width <= snapshot.Transform.ImageWidth
            && a.Y + a.Height <= snapshot.Transform.ImageHeight).ToArray();
    }
}

public sealed class TurnLifetime : IDisposable
{
    private CancellationTokenSource _source = new();
    public long Generation { get; private set; }
    public CancellationToken Token => _source.Token;
    public long Restart() { Cancel(); _source.Dispose(); _source = new(); return Generation; }
    public void Cancel() { Generation++; _source.Cancel(); }
    public bool IsCurrent(long generation) => generation == Generation && !_source.IsCancellationRequested;
    public void Dispose() { Cancel(); _source.Dispose(); }
}

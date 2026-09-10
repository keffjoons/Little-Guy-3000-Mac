namespace LittleGuy3000.Core;

public sealed record ReplyDraft(string Recipient, string Comment, string Text);
public sealed record ReplyBatch(string Layout, bool FocusedComposerMatches, List<ReplyDraft> Drafts);

public static class ReplyDraftPolicy
{
    public static ReplyBatch Validate(AssistantAnswer answer, string captureId)
    {
        var batch = answer.Replies;
        if (answer.CaptureId != captureId || batch is null || batch.Layout is not ("single" or "multiple" or "uncertain")
            || batch.Drafts is null || batch.Drafts.Count > 12
            || batch.Layout == "single" && batch.Drafts.Count != 1
            || batch.Drafts.Any(d => d is null || string.IsNullOrWhiteSpace(d.Recipient) || d.Recipient.Length > 120
                || string.IsNullOrWhiteSpace(d.Comment) || d.Comment.Length > 600
                || string.IsNullOrWhiteSpace(d.Text) || d.Text.Length > 2000
                || d.Text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))))
            throw new InvalidDataException("Little Guy couldn't match the drafts to this screen. Try again with the comments clearly visible.");
        return batch;
    }

    public static bool CanInsert(ReplyBatch batch) => batch.Layout == "single" && batch.FocusedComposerMatches && batch.Drafts.Count == 1;
}

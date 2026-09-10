using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Companion;
using LittleGuy3000.Desktop.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private readonly ReplyPadWindow _replyPad = new();

    private async Task RunReplyCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        System.Diagnostics.Process? host = null;
        try
        {
            AppWindow.Show(); Activate(); await Task.Delay(250);
            string fixture = Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE")!;
            host = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fixture, "--composer-host")
            { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            nint handle = (nint)long.Parse((await host!.StandardOutput.ReadLineAsync())!);
            await Task.Delay(500);
            _provider = new LittleGuy3000.Codex.CodexProvider();
            await _provider.ConnectAsync(fixture, Path.Combine(_store.DirectoryPath, "reply-fixture"));
            _availableModels = await _provider.ModelsAsync(); _connected = true; Settings.ScreenEnabled = true;
            Native.SetForegroundWindow(handle); Native.PostMessageW(handle, 0x8050, 1, 0); await Task.Delay(300);
            report["testEditorForeground"] = Native.GetForegroundWindow() == handle;
            var syntheticTarget = _capture.Describe(handle, new Native.Point(180,380), true)!;
            await DraftRepliesAsync(syntheticTarget);
            report["singleInsertedOrSafeFallback"] = _summaryText.StartsWith("Reply filled in.") && _bubble.Visible
                || _replyPad.Visible && _replyPad.DraftCount == 1 && Native.GetForegroundWindow() != handle;
            Settings.ReplyTone = "MULTIPLE_FIXTURE";
            Native.SetForegroundWindow(handle); Native.PostMessageW(handle, 0x8050, 1, 0); await Task.Delay(200);
            nint foregroundBeforePad = Native.GetForegroundWindow();
            await DraftRepliesAsync(syntheticTarget);
            report["multipleDraftPad"] = _replyPad.Visible && _replyPad.DraftCount == 2;
            report["multipleNotInserted"] = !_summaryText.StartsWith("Reply filled in.");
            report["padDidNotStealFocus"] = Native.GetForegroundWindow() == foregroundBeforePad;
            report["replyHotkeyRegistered"] = _tray.ReplyHotkeyAvailable;
            Native.SetWindowDisplayAffinity(_replyPad.Handle, 0);
            await Task.Delay(400);
            var context = _capture.Describe(_replyPad.Handle, new Native.Point(0,0), true)!;
            using var screenshot = await _capture.CaptureAsync(context, new([]), CancellationToken.None);
            await File.WriteAllBytesAsync(Path.Combine(directory, "reply-pad.png"), screenshot.Png);
            HideApp(); report["hideDismissesPad"] = !_replyPad.Visible;
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        finally
        {
            if (host is not null) { if (!host.HasExited) { host.Kill(); await host.WaitForExitAsync(); } host.Dispose(); }
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "reply-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }

    private void ReplyTone_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        Settings.ReplyTone = ReplyToneBox.Text; _store.SaveSettings();
    }

    private async Task DraftRepliesAsync(WindowContext? syntheticTarget = null)
    {
        if (_paused || _quitting || _busy) return;
        Native.GetCursorPos(out var cursor);
        // Use the active conversation, regardless of where the mouse drifted after clicking its editor.
        var selected = syntheticTarget ?? _capture.Describe(Native.GetForegroundWindow(), cursor, true);
        CancelActive(); long generation = _lifetime.Restart(); var token = _lifetime.Token;
        _walkthrough.Cancel(); _quickGoalPending = false; _replyPad.Clear(); DropSnapshot();
        _hidden = false; _compactMode = true; _summaryText = ""; _target = selected;
        var anchor = new PixelPoint(cursor.X, cursor.Y);
        _companion.Anchor = anchor; ApplyPreferences(); AppWindow.Hide();
        _bubble.ShowAt(anchor, false); _bubble.SetMessage("Little Guy · reply drafts", "Reading the visible conversation…", true);
        if (!Settings.ScreenEnabled || selected is null || selected.ProcessId == Environment.ProcessId)
        { ShowNotice(!Settings.ScreenEnabled ? "Enable screen context in Settings to draft replies." : "Open a conversation or post, click its reply box, then press Ctrl+Shift+R.", true); return; }
        if (!new CapturePolicy(Settings.Exclusions).Allows(selected.ProcessName))
        { ShowNotice("Reply drafting is disabled for this excluded application.", true); return; }
        _busy = true; SendButton.IsEnabled = false; CancelButton.Visibility = Visibility.Visible;
        try
        {
            // Capture field identity before any await can change the active target.
            var focused = await ReplyComposer.CaptureAsync(selected, token);
            using var snapshot = await _capture.CaptureAsync(selected, new(Settings.Exclusions), token);
            if (!_connected)
            {
                for (int i = 0; _connecting && i < 40; i++) await Task.Delay(100, token);
                if (!_connected && !_connecting) await ConnectAsync(false, false);
            }
            if (!_lifetime.IsCurrent(generation)) return;
            if (!_connected || _provider is null) { ShowNotice("Connect your account in Settings to draft replies.", true); return; }
            if (!ConfigureTurn()) return;
            string field = "No supported empty focused editor. Provide copyable drafts only.";
            if (focused is not null)
            {
                var top = snapshot.Transform.ToImage(new(focused.Bounds.X, focused.Bounds.Y));
                var bottom = snapshot.Transform.ToImage(new(focused.Bounds.Right, focused.Bounds.Bottom));
                field = JsonSerializer.Serialize(new { name = focused.Name, x = top.X, y = top.Y, width = bottom.X - top.X, height = bottom.Y - top.Y });
            }
            string tone = Settings.ReplyTone ?? ""; if (tone.Length > 4000) tone = tone[..4000];
            SetState(CompanionState.Thinking, "Drafting replies in your tone…");
            var answer = await _provider.DraftRepliesAsync(snapshot, tone, field, token);
            if (!_lifetime.IsCurrent(generation)) return;
            var batch = ReplyDraftPolicy.Validate(answer, snapshot.Id);
            QuestionText.Text = "Reply drafts";
            AnswerText.Text = string.Join("\n\n", batch.Drafts.Select(d => $"{d.Recipient} · {d.Comment}\n{d.Text}"));
            if (batch.Drafts.Count == 0) { ShowNotice(answer.Answer); return; }
            bool inserted = focused is not null && ReplyDraftPolicy.CanInsert(batch) && Settings.ScreenEnabled
                && new CapturePolicy(Settings.Exclusions).Allows(selected.ProcessName)
                && await ReplyComposer.InsertAsync(focused, selected, batch.Drafts[0].Text, token);
            if (!_lifetime.IsCurrent(generation)) return;
            _summaryText = inserted ? "Reply filled in. Review it, then send when you're ready." : "Your drafts are ready to edit and copy.";
            if (inserted)
                _bubble.SetMessage("Little Guy · reply ready", _summaryText + "\n" + CompactReply.Preview(batch.Drafts[0].Text, ""), false);
            else
            {
                _bubble.Hide(); _compactMode = false; _companion.Anchor = null;
                _replyPad.ShowDrafts(batch, anchor, batch.Layout == "multiple" ? "Replies for the visible comments, in screen order."
                : "Your draft is ready. The reply box couldn't be safely filled, so copy it below.");
            }
            SetState(CompanionState.Success, _summaryText);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) ShowNotice(ex.Message, true); }
        finally
        {
            if (_lifetime.IsCurrent(generation)) { _busy = false; SendButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; UpdateWalkthroughUi(); }
        }
    }
}

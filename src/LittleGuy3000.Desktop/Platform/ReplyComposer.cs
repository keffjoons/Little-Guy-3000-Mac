using System.Windows.Automation;
using LittleGuy3000.Core;

namespace LittleGuy3000.Desktop.Platform;

internal sealed record ComposerTarget(int[] RuntimeId, int ProcessId, PixelRect Bounds, string Name, DateTimeOffset CapturedAt);

/// <summary>Targets an accessibility element directly. Never sends keys, activates windows or submits.</summary>
internal static class ReplyComposer
{
    public static async Task<ComposerTarget?> CaptureAsync(WindowContext window, CancellationToken token)
    {
        try { return await Task.Run(() => Read(window), token).WaitAsync(TimeSpan.FromSeconds(3), token); }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static ComposerTarget? Read(WindowContext window)
    {
        if (Native.GetForegroundWindow() != window.Handle) return null;
        var element = AutomationElement.FocusedElement;
        if (element is null) return null;
        var info = element.Current;
        if (info.ProcessId == Environment.ProcessId || !info.HasKeyboardFocus || !info.IsEnabled || info.IsOffscreen || info.IsPassword
            || info.ControlType != ControlType.Edit || !element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) return null;
        // Browsers can expose renderer-process elements. Validate membership by ancestor HWND as well.
        if (!BelongsToWindow(element, window.Handle)) return null;
        var value = (ValuePattern)pattern;
        if (value.Current.IsReadOnly || value.Current.Value.Length != 0) return null;
        string name = info.Name ?? "";
        if (new[] { "search", "address", "password", "username", "email", "url" }.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase))) return null;
        var rect = info.BoundingRectangle;
        var bounds = new PixelRect(rect.X, rect.Y, rect.Width, rect.Height);
        if (!bounds.IsValid || !window.Bounds.Contains(new(bounds.X, bounds.Y)) || !window.Bounds.Contains(new(bounds.Right, bounds.Bottom))) return null;
        return new(element.GetRuntimeId(), info.ProcessId, bounds, name.Length > 200 ? name[..200] : name, DateTimeOffset.UtcNow);
    }

    private static bool BelongsToWindow(AutomationElement element, nint window)
    {
        for (int i = 0; i < 32 && element is not null; i++)
        {
            if ((nint)element.Current.NativeWindowHandle == window) return true;
            element = TreeWalker.RawViewWalker.GetParent(element);
        }
        return false;
    }

    public static Task<bool> InsertAsync(ComposerTarget target, WindowContext window, string draft, CancellationToken token)
        => Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (DateTimeOffset.UtcNow - target.CapturedAt > TimeSpan.FromMinutes(3)
                    || !Native.IsWindow(window.Handle) || Native.IsIconic(window.Handle)
                    || Native.GetWindowThreadProcessId(window.Handle, out uint pid) == 0 || pid != window.ProcessId
                    || Native.Bounds(window.Handle) != window.Bounds) return false;
                var current = Read(window);
                if (current is null || current.ProcessId != target.ProcessId || !current.RuntimeId.SequenceEqual(target.RuntimeId)
                    || current.Bounds != target.Bounds || current.Name != target.Name) return false;
                var element = AutomationElement.FocusedElement;
                if (element is null || !element.GetRuntimeId().SequenceEqual(target.RuntimeId)
                    || !element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) return false;
                var value = (ValuePattern)pattern;
                token.ThrowIfCancellationRequested();
                if (Native.GetForegroundWindow() != window.Handle || !element.Current.HasKeyboardFocus || value.Current.IsReadOnly || value.Current.Value != "") return false;
                value.SetValue(draft);
                // A provider that silently ignores SetValue must not be reported as successful.
                return value.Current.Value == draft;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
        }, token);
}

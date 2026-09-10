namespace LittleGuy3000.Desktop.Platform;

/// <summary>UI-thread-owned policy for every app window, including transient overlays.</summary>
internal static class CaptureVisibility
{
    private static readonly HashSet<nint> Windows = [];
    internal static bool Visible { get; private set; }
    internal static nint[] Handles => Windows.ToArray();
    internal static void Register(nint handle)
    {
        Windows.Add(handle);
        SetAffinity(handle, Visible ? 0u : 0x11u);
    }
    private static bool SetAffinity(nint handle, uint expected)
    {
        // Avoid repeatedly rebuilding compositor surfaces when the preference has not changed.
        return Native.GetWindowDisplayAffinity(handle, out uint current) && current == expected
            || Native.SetWindowDisplayAffinity(handle, expected);
    }
    internal static void Unregister(nint handle) => Windows.Remove(handle);
    internal static bool Apply(bool visible)
    {
        Visible = visible;
        bool success = true;
        foreach (var handle in Windows)
            success &= SetAffinity(handle, visible ? 0u : 0x11u);
        return success;
    }
}

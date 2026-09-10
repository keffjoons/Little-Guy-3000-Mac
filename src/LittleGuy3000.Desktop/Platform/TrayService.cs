using System.Drawing;
using System.Runtime.InteropServices;
using LittleGuy3000.Core;

namespace LittleGuy3000.Desktop.Platform;

internal sealed class TrayService : IDisposable
{
    private readonly NativeWindow _window = new();
    private readonly Icon _icon;
    internal nint MessageWindow => _window.Handle;
    public event Action? Ask, Show, Settings, Pause, Hide, Quit, Locked, DisplayChanged;
    internal const string ActivationTitle = "LittleGuy3000.Activation";
    public event Action? Hotkey;
    public event Action? QuickExplain, QuickWalkthrough, DraftReplies;
    public bool ReplyHotkeyAvailable { get; }
    public event Action? InterfaceOverview, TutorialRegion, TutorialNext;
    public bool TutorialRegionHotkeyAvailable { get; }
    public bool TutorialNextHotkeyAvailable { get; }
    public bool OverviewHotkeyAvailable { get; }
    public event Action? CircleAsk, VoiceCommand;
    public bool CircleHotkeyAvailable { get; }
    public bool CommandHotkeyAvailable { get; }
    public bool HotkeyAvailable { get; private set; }
    public bool HideHotkeyAvailable { get; }
    public bool QuitHotkeyAvailable { get; }
    public bool ExplainHotkeyAvailable { get; }
    public bool WalkthroughHotkeyAvailable { get; }
    public TrayService(Icon icon, bool shift)
    {
        _icon = icon;
        SetWindowTextW(_window.Handle, ActivationTitle);
        _window.Message += OnMessage;
        Native.WTSRegisterSessionNotification(_window.Handle, 0);
        SetHotkey(shift);
        HideHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 2, 0x4003, 0x48); // Ctrl+Alt+H
        QuitHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 3, 0x4003, 0x51); // Ctrl+Alt+Q
        ExplainHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 4, 0x4002, 0xDB); // Ctrl+[
        WalkthroughHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 5, 0x4002, 0xDD); // Ctrl+]
        ReplyHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 6, 0x4006, 0x52); // Ctrl+Shift+R
        OverviewHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 7, 0x4006, 0xDC); // Ctrl+Shift+backslash
        CircleHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 8, 0x4003, 0x43); // Ctrl+Alt+C
        CommandHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 9, 0x4003, 0x20); // Ctrl+Alt+Space
        TutorialRegionHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 10, 0x4006, 0xDD); // Ctrl+Shift+]
        TutorialNextHotkeyAvailable = Native.RegisterHotKey(_window.Handle, 11, 0x4003, 0x0D); // Ctrl+Alt+Enter
        var data = Data(); Shell_NotifyIconW(0, ref data);
    }
    public void SetHotkey(bool shift)
    {
        Native.UnregisterHotKey(_window.Handle, 1);
        HotkeyAvailable = Native.RegisterHotKey(_window.Handle, 1, (uint)(0x4002 | (shift ? 4 : 0)), 0x20);
    }
    private nint? OnMessage(uint msg, nuint w, nint l)
    {
        if (msg == 0x8002) { if (w == 2) Settings?.Invoke(); else Show?.Invoke(); return 0; }
        if (msg == 0x312) { switch ((int)w) { case 1: Hotkey?.Invoke(); break; case 2: Hide?.Invoke(); break; case 3: Quit?.Invoke(); break; case 4: QuickExplain?.Invoke(); break; case 5: QuickWalkthrough?.Invoke(); break; case 6: DraftReplies?.Invoke(); break; case 7: InterfaceOverview?.Invoke(); break; case 8: CircleAsk?.Invoke(); break; case 9: VoiceCommand?.Invoke(); break; case 10: TutorialRegion?.Invoke(); break; case 11: TutorialNext?.Invoke(); break; } return 0; }
        if (msg == 0x2B1 && w == 7) { Locked?.Invoke(); return 0; }
        if (msg == 0x7E) { DisplayChanged?.Invoke(); return 0; }
        if (msg == 0x8001)
        {
            if ((uint)l == 0x202) Ask?.Invoke();
            if ((uint)l == 0x205) ShowMenu();
            return 0;
        }
        return null;
    }
    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        try
        {
            AppendMenuW(menu, 0, 1, "Ask Little Guy"); AppendMenuW(menu, 0, 2, "Settings");
            AppendMenuW(menu, 0, 3, "Pause / resume"); AppendMenuW(menu, 0, 5, "Hide\tCtrl+Alt+H"); AppendMenuW(menu, 0x800, 0, ""); AppendMenuW(menu, 0, 4, "Quit\tCtrl+Alt+Q");
            Native.GetCursorPos(out var cursor); Native.SetForegroundWindow(_window.Handle);
            uint command = TrackPopupMenu(menu, 0x100 | 0x2, cursor.X, cursor.Y, 0, _window.Handle, 0);
            switch (command) { case 1: Ask?.Invoke(); break; case 2: Settings?.Invoke(); break; case 3: Pause?.Invoke(); break; case 4: Quit?.Invoke(); break; case 5: Hide?.Invoke(); break; }
        }
        finally { DestroyMenu(menu); }
    }
    private NotifyData Data() => new() { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = _window.Handle, Id = 1, Flags = 1 | 2 | 4, Callback = 0x8001, Icon = _icon.Handle, Tip = Brand.Name };
    public void Dispose() { for (int id = 1; id <= 11; id++) Native.UnregisterHotKey(_window.Handle, id); Native.WTSUnRegisterSessionNotification(_window.Handle); var data = Data(); Shell_NotifyIconW(2, ref data); _window.Dispose(); _icon.Dispose(); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint message, ref NotifyData data);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(nint window, string title);
}

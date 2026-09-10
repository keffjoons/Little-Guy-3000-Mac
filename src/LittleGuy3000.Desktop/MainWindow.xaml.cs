using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Codex;
using LittleGuy3000.Desktop.Companion;
using LittleGuy3000.Desktop.Platform;
using LittleGuy3000.Desktop.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using VirtualKey = Windows.System.VirtualKey;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly LocalStore _store;
    private readonly CompanionWindow _companion;
    private readonly BubbleWindow _bubble;
    private readonly TrayService _tray;
    private readonly AnnotationOverlay _overlay;
    private readonly ScreenCaptureService _capture = new();
    private readonly TurnLifetime _lifetime = new();
    private readonly WalkthroughSession _walkthrough = new();
    private readonly LocalSpeechService _speech;
    private readonly DispatcherQueueTimer _frameTimer, _keyTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private CodexProvider? _provider;
    private ScreenSnapshot? _snapshot;
    private WindowContext? _target;
    private bool _loaded, _busy, _paused, _quitting, _drawing, _connected, _holding, _holdStarted, _submittingVoice, _connecting, _recovering;
    private double _holdTime;
    private CompanionState _state;
    private int _spokenLength;
    private string _lastQuestion = "";
    private bool _hidden;
    private nint _lastExternalWindow;
    private bool _compactMode, _quickGoalPending;
    private string _summaryText = "";
    private IReadOnlyList<AvailableModel> _availableModels = [];
    private nint Hwnd => WinRT.Interop.WindowNative.GetWindowHandle(this);
    private UserSettings Settings => _store.Settings;

    public MainWindow()
    {
        InitializeComponent();
        string? overrideDirectory = Environment.GetEnvironmentVariable("LITTLEGUY_DATA_DIR");
        _store = new LocalStore(overrideDirectory);
        if (Environment.GetCommandLineArgs().Contains("--self-test")) Settings.ShowInScreenCapture = true;
        CaptureVisibility.Apply(Settings.ShowInScreenCapture);
        CaptureVisibility.Register(Hwnd);
        Closed += (_, _) => CaptureVisibility.Unregister(Hwnd);
        Root.RequestedTheme = ElementTheme.Dark;
        BuildInfo.Text = $"{Brand.Name} · Development build {Brand.Version}\nExplicit voice commands can open supported apps and control media. Reply drafts are never sent automatically. Clipboard reading is off.";
        Title = Brand.Name; ExtendsContentIntoTitleBar = true; SetTitleBar(DragRegion);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(570, 810));
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = true;
        _companion = new(DispatcherQueue); _overlay = new(DispatcherQueue); _bubble = new();
        _bubble.OpenDetails += OpenDetails;
        _bubble.PlacementChanged += anchor => _companion.Anchor = anchor;
        _bubble.StopSpeech += StopNarration;
        _bubble.Confirm += () => _ = ContinueTutorialAsync();
        _bubble.Reply += text => { if (_busy) return; if (_circlePending) { _ = AskCircleAsync(text); return; } if (_quickGoalPending) { _ = StartTutorialGoalAsync(text); } else _ = SubmitAsync(text); };
        _bubble.Dismissed += HideApp;
        _tray = new(SmileyRenderer.CreateIcon(), Settings.ShiftHotkey);
        _speech = new();
        _speech.PartialTranscript += text => { long generation = _lifetime.Generation; Dispatch(() => { if (_lifetime.IsCurrent(generation) && !_paused) { StatusText.Text = text; if (_compactMode && _speech.HasSession) _bubble.SetMessage("Little Guy · transcribing",text,true); } }); };
        _speech.AudioLevel += level => { long generation = _lifetime.Generation; Dispatch(() => { if (_lifetime.IsCurrent(generation) && _speech.Recording && !_paused) { string status = level > 1 ? $"Listening · microphone level {level}% · release to finish" : "Listening · speak now · release to finish"; StatusText.Text = status; if (_compactMode) _bubble.SetMessage("Little Guy · listening",status,true); } }); };
        _speech.Error += text => Dispatch(() => ShowNotice(text, true));
        _speech.SpeechEnded += () => Dispatch(() => { if (!_busy && !_speech.Listening && !_paused) SetState(CompanionState.Idle, "Ready when you are."); });
        _tray.Ask += () => _ = ActivateAtPointerAsync(false);
        _tray.Hotkey += () => _ = ActivateAtPointerAsync(true);
        _tray.QuickExplain += () => _ = QuickInvokeAsync(false);
        _tray.QuickWalkthrough += () => _ = QuickInvokeAsync(true);
        _tray.TutorialRegion += () => _ = SelectInterfaceAsync(true);
        _tray.TutorialNext += () => _ = ContinueTutorialAsync();
        _tray.DraftReplies += () => _ = DraftRepliesAsync();
        _tray.InterfaceOverview += () => _ = SelectInterfaceAsync();
        _tray.CircleAsk += () => _ = CircleAskAsync();
        _tray.VoiceCommand += () => BeginSpecialVoice(true);
        _tray.Settings += () => { OpenDetails(); ShowTab("settings"); };
        _tray.Show += OpenDetails;
        _tray.Hide += HideApp;
        _tray.Pause += TogglePause; _tray.Quit += () => _ = QuitAsync();
        _tray.Locked += () => Pause(true); _tray.DisplayChanged += () => { CancelActive(); _overlay.Clear(); DropSnapshot(); };
        AppWindow.Closing += (_, e) => { if (!_quitting) { e.Cancel = true; HideApp(); } };
        _frameTimer = DispatcherQueue.CreateTimer(); _frameTimer.Interval = TimeSpan.FromMilliseconds(120); _frameTimer.Tick += (_, _) => _ = UpdateCharacterAsync(); _frameTimer.Start();
        _keyTimer = DispatcherQueue.CreateTimer(); _keyTimer.Interval = TimeSpan.FromMilliseconds(25); _keyTimer.Tick += (_, _) => PollShortcut(); _keyTimer.Start();
        Root.Loaded += async (_, _) =>
        {
            LoadSettings(); _loaded = true;
            await UpdateCharacterAsync();
            if (Environment.GetCommandLineArgs().Contains("--self-test")) await RunSelfTestAsync();
            else
            {
                await ConnectAsync(false, false);
                if (Environment.GetCommandLineArgs().Contains("--settings")) { OpenDetails(); ShowTab("settings"); }
                else if (_connected && Environment.GetCommandLineArgs().Contains("--connect") && !Environment.GetCommandLineArgs().Contains("--show")) { AppWindow.Hide(); SetState(CompanionState.Idle, "Ctrl+[ to explain · Ctrl+] to guide"); }
                else OpenDetails();
            }
        };
    }
    private void Dispatch(Action action) => DispatcherQueue.TryEnqueue(() => { if (!_quitting) action(); });
    private void SetState(CompanionState state, string message)
    {
        _state = state; _companion.State = state; StatusText.Text = message;
        if (_compactMode && state is CompanionState.Thinking or CompanionState.Listening && string.IsNullOrEmpty(_summaryText)) _bubble.SetMessage("Little Guy · " + (state == CompanionState.Listening ? "listening" : "thinking…"), message, true);
    }
    private void ShowNotice(string message, bool error = false)
    {
        NoticeText.Text = message; Notice.Visibility = Visibility.Visible;
        if (_compactMode) _bubble.SetMessage("Little Guy · " + (error ? "needs a hand" : "a quick note"), message, false);
        StatusText.Text = message;
        if (error) SetState(CompanionState.Error, message);
    }
    private void ShowTab(string tab)
    {
        AskPanel.Visibility = tab == "ask" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tab == "settings" ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = tab == "history" ? Visibility.Visible : Visibility.Collapsed;
    }
    private void LoadSettings()
    {
        CodexPathBox.Text = string.IsNullOrWhiteSpace(Settings.CodexExecutable) ? FindCodex() ?? "" : Settings.CodexExecutable;
        CommandsToggle.IsOn = Settings.VoiceCommandsEnabled;
        StreamVisibilityToggle.IsOn = Settings.ShowInScreenCapture;
        ScreenToggle.IsOn = Settings.ScreenEnabled; VoiceToggle.IsOn = Settings.VoiceEnabled; SpeakToggle.IsOn = Settings.AutoSpeak;
        HistoryToggle.IsOn = Settings.HistoryEnabled; MotionToggle.IsOn = Settings.ReducedMotion; CompanionToggle.IsOn = Settings.ShowCompanion;
        HotkeyToggle.IsOn = Settings.ShiftHotkey; ExcludedBox.Text = Settings.ExcludedApps;
        DetailEffortCombo.SelectedIndex = Settings.DetailEffort == "high" ? 1 : 0;
        ReplyToneBox.Text = Settings.ReplyTone;
        SpeechRateSlider.Value = Settings.SpeechRate; _speech.SetRate(Settings.SpeechRate);
        VoiceInfo.Text = _speech.RecognitionDescription;
        RefreshMicrophones();
        InitializeNarration(); InitializeResearch();
        ApplyPreferences();
    }
    private static string? FindCodex()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        { try { string path = Path.Combine(entry.Trim('"'), "codex.exe"); if (File.Exists(path)) return path; } catch (ArgumentException) { } }
        return null;
    }
    private void ApplyPreferences()
    {
        ApplyCaptureVisibility();
        _companion.Enabled = Settings.ShowCompanion && !_paused && !_hidden; _companion.ReducedMotion = Settings.ReducedMotion; _bubble.ReducedMotion = Settings.ReducedMotion;
        ShortcutText.Text = Settings.ShiftHotkey ? "CTRL + SHIFT + SPACE" : "CTRL + SPACE";
        HotkeyInfo.Text = _tray.HotkeyAvailable ? "Tap to type. Hold to talk when the microphone is enabled. Escape stops the current interaction." : "This shortcut is already registered. Choose the alternative above.";
        HotkeyInfo.Text += "\nShift+Enter or Ctrl+Enter: send · Enter: new line\n" + (_tray.HideHotkeyAvailable ? "Ctrl+Alt+H: hide Little Guy" : "Ctrl+Alt+H is in use by another app; use Hide in the tray menu.")
            + "\n" + (_tray.QuitHotkeyAvailable ? "Ctrl+Alt+Q: quit Little Guy" : "Ctrl+Alt+Q is in use by another app; use Quit in the tray menu.");
        HotkeyInfo.Text = (_tray.ExplainHotkeyAvailable ? "Ctrl+[: explain in a bubble" : "Ctrl+[ is in use by another app.") + "\n" + (_tray.WalkthroughHotkeyAvailable ? "Ctrl+]: tutorial for the pointed control" : "Ctrl+] is in use by another app.") + "\n" + HotkeyInfo.Text;
        HotkeyInfo.Text += "\n" + (_tray.OverviewHotkeyAvailable ? "Ctrl+Shift+\\: select an interface to explain" : "Ctrl+Shift+\\ is in use by another app.");
        HotkeyInfo.Text += "\n" + (_tray.ReplyHotkeyAvailable ? "Ctrl+Shift+R: draft replies" : "Ctrl+Shift+R is in use by another app.");
        HotkeyInfo.Text += "\n" + (_tray.CircleHotkeyAvailable ? "Ctrl+Alt+C: circle and ask" : "Ctrl+Alt+C is in use.") + "\n" + (_tray.CommandHotkeyAvailable ? "Hold Ctrl+Alt+Space: voice command" : "Ctrl+Alt+Space is in use.");
        HotkeyInfo.Text += "\n" + (_tray.TutorialRegionHotkeyAvailable ? "Ctrl+Shift+]: select a tutorial area" : "Ctrl+Shift+] is in use.") + "\n" + (_tray.TutorialNextHotkeyAvailable ? "Ctrl+Alt+Enter: check step and continue" : "Ctrl+Alt+Enter is in use; use the step button.");
        MicButton.IsEnabled = Settings.VoiceEnabled && _speech.RecognitionAvailable && !_paused;
    }
    private async Task ConnectAsync(bool login, bool showSettings = true)
    {
        if (_connecting || _quitting) return;
        _connecting = true;
        try
        {
            if (showSettings) ShowTab("settings"); AccountText.Text = "Connecting to Codex…";
            string executable = CodexPathBox.Text.Trim(' ', '"');
            if (!File.Exists(executable)) throw new IOException("Choose an installed codex.exe. Install the official Codex CLI if it is missing.");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("--version"); using var version = Process.Start(start)!;
            string versionText = await version.StandardOutput.ReadToEndAsync(); await version.WaitForExitAsync();
            if (!versionText.Trim().EndsWith("0.153.4", StringComparison.Ordinal)) throw new InvalidOperationException("This development build has validated Codex 0.153.4. Use that version until additional versions pass the compatibility tests.");
            Settings.CodexExecutable = executable; _store.SaveSettings();
            if (_provider is null)
            {
                _provider = new CodexProvider(); var ownedProvider = _provider;
                _provider.SummaryProgress += text => { long generation = _lifetime.Generation; Dispatch(() => { if (ReferenceEquals(_provider, ownedProvider) && _lifetime.IsCurrent(generation) && _compactMode && !string.IsNullOrWhiteSpace(text)) { _summaryText = text; _bubble.SetMessage(_overviewActive ? "Little Guy · interface overview" : _walkthrough.Objective is null ? "Little Guy · quick explanation" : $"Little Guy · step {_walkthrough.Step}", text, true); } }); };
                _provider.AccountChanged += () => Dispatch(() => { if (ReferenceEquals(_provider, ownedProvider)) _ = CheckAccountAsync(); });
                _provider.Disconnected += () => Dispatch(() => { if (ReferenceEquals(_provider, ownedProvider)) _ = RecoverConnectionAsync(ownedProvider); });
                await _provider.ConnectAsync(executable, Path.Combine(_store.DirectoryPath, "codex"));
            }
            await CheckAccountAsync();
            if (!_connected && login)
            {
                var uri = await _provider.StartLoginAsync();
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                AccountText.Text = "Finish signing in in your browser. Codex handles the secure sign-in callback.";
            }
        }
        catch (Exception ex)
        {
            if (_provider is not null) { var failed = _provider; _provider = null; await failed.DisposeAsync(); }
            _connected = false; AccountText.Text = ex.Message; ShowNotice(ex.Message, true);
        }
        finally { _connecting = false; }
    }
    private async Task RecoverConnectionAsync(CodexProvider lost)
    {
        if (_recovering || _quitting) return;
        _recovering = true; _provider = null; _connected = false;
        CancelActive(); DropSnapshot();
        try
        {
            await lost.DisposeAsync();
            for (int attempt = 0; attempt < 2 && !_quitting && !_connected; attempt++)
            {
                SetState(CompanionState.Thinking, "Reconnecting to Codex…");
                await Task.Delay(TimeSpan.FromSeconds(attempt + 1));
                if (!_quitting) await ConnectAsync(false, false);
            }
            if (!_quitting) ShowNotice(_connected ? "Reconnected. The interrupted question was not resent; ask again to continue." : "Couldn't reconnect. Check Codex in Settings.", !_connected);
        }
        finally { _recovering = false; }
    }
    private async Task CheckAccountAsync()
    {
        if (_provider is null) { AccountText.Text = "Not connected. Select Connect / sign in."; return; }
        try
        {
            var account = await _provider.ReadAccountAsync(); _connected = account.Connected;
            if (Environment.GetCommandLineArgs().Contains("--connect"))
                await File.WriteAllTextAsync(Path.Combine(_store.DirectoryPath, "connection-status.json"), JsonSerializer.Serialize(new { connected = _connected, checkedAt = DateTimeOffset.UtcNow }));
            AccountText.Text = account.Connected ? $"Connected to ChatGPT\n{account.Email ?? "Account connected"} · {account.Plan ?? "Plan unavailable"}" : "Not signed in. Connect your ChatGPT account.";
            if (_connected)
            {
                var models = await _provider.ModelsAsync(); _availableModels = models; ModelCombo.ItemsSource = models;
                ModelCombo.SelectedItem = models.FirstOrDefault(x => x.Id == Settings.Model) ?? models.FirstOrDefault(x => x.IsDefault) ?? models.FirstOrDefault();
                SetupButton.Visibility = Visibility.Collapsed; SetState(CompanionState.Success, "Connected. Little Guy is ready.");
            }
        }
        catch (Exception ex) { AccountText.Text = ex.Message; }
    }
    private async Task ActivateAtPointerAsync(bool fromHotkey)
    {
        if (_circlePending && fromHotkey) { BeginSpecialVoice(false); return; }
        if (_paused) return;
        if (_compactMode || _bubble.Visible) { OpenDetails(); return; }
        _hidden = false; ApplyPreferences();
        CancelActive(); long generation = _lifetime.Restart();
        _target = _capture.TargetUnderPointer(); DropSnapshot();
        if (fromHotkey) { _holding = true; _holdStarted = false; _holdTime = _clock.Elapsed.TotalSeconds; }
        ShowTab("ask"); WelcomePanel.Visibility = Visibility.Collapsed;
        QuestionBox.Text = ""; ContextText.Text = "Text only · no screen attached";
        Native.GetCursorPos(out var cursor); var work = Native.WorkArea(cursor);
        int x = (int)Math.Clamp(cursor.X + 34, work.X, Math.Max(work.X, work.Right - AppWindow.Size.Width));
        int y = (int)Math.Clamp(cursor.Y + 26, work.Y, Math.Max(work.Y, work.Bottom - AppWindow.Size.Height));
        // The target was resolved before this panel is allowed to activate.
        Task<ScreenSnapshot>? captureTask = null;
        if (Settings.ScreenEnabled && _target is not null)
        {
            if (new CapturePolicy(Settings.Exclusions).Allows(_target.ProcessName)) captureTask = _capture.CaptureAsync(_target, new(Settings.Exclusions), _lifetime.Token);
            else { ContextText.Text = "Screen capture is disabled for this application."; _target = null; }
        }
        AppWindow.Move(new Windows.Graphics.PointInt32(x, y)); AppWindow.Show(); Activate();
        if (captureTask is not null)
        {
            SetState(CompanionState.Thinking, "Looking at the selected window…");
            try
            {
                var snapshot = await captureTask;
                if (!_lifetime.IsCurrent(generation)) { snapshot.Dispose(); return; }
                _snapshot = snapshot; ContextText.Text = $"Screen attached · {snapshot.Window.ProcessName} · {snapshot.Transform.ImageWidth} × {snapshot.Transform.ImageHeight}";
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { ShowNotice(ex.Message, true); }
        }
        if (!_speech.Listening) { SetState(CompanionState.Idle, "Ask about what you're looking at."); QuestionBox.Focus(FocusState.Programmatic); }
    }
    private void OpenDetails()
    {
        _hidden = false; _compactMode = false; _bubble.Hide(); _companion.Anchor = null; ApplyPreferences();
        ShowTab(!_connected || !Settings.ScreenEnabled ? "settings" : "ask");
        AppWindow.Show(); Activate(); QuestionBox.Focus(FocusState.Programmatic);
    }
    private bool ConfigureTurn()
    {
        if (_provider is null) return false;
        string desired = _compactMode ? "low" : Settings.DetailEffort == "high" ? "high" : "medium";
        var model = _compactMode ? _availableModels.FirstOrDefault(m => m.Id == "gpt-6-astra")
            : _availableModels.FirstOrDefault(m => m.Id == Settings.Model) ?? _availableModels.FirstOrDefault(m => m.IsDefault) ?? _availableModels.FirstOrDefault();
        if (_availableModels.Count == 0 && Environment.GetCommandLineArgs().Contains("--self-test"))
        { _provider.Effort = desired; _provider.Compact = _compactMode; return true; }
        if (model is null || model.SupportedReasoningEfforts?.Contains(desired) != true)
        { ShowNotice($"The connected model does not offer {desired} reasoning. Check the model in Settings.", true); return false; }
        _provider.Model = model.Id; _provider.Effort = desired; _provider.Compact = _compactMode; return true;
    }
    private async Task QuickInvokeAsync(bool walkthrough, WindowContext? syntheticTarget = null)
    {
        if (_paused || _quitting || walkthrough && _busy) return;
        if (walkthrough)
        {
            if (_walkthrough.Objective is not null) { await ContinueTutorialAsync(); return; }
            if (_quickGoalPending) { _bubble.FocusReply(); return; }
            PrepareTutorial(syntheticTarget ?? _capture.TargetUnderPointer());
            return;
        }
        var selected = syntheticTarget ?? _capture.TargetUnderPointer();
        Native.GetCursorPos(out var triggerPoint);
        nint triggerWindow = Native.GetAncestor(Native.WindowFromPoint(triggerPoint), 2);
        if (selected is null && _compactMode && (triggerWindow == _bubble.Handle || triggerWindow == Hwnd)) selected = _target;
        CancelActive(); _hidden = false; _compactMode = true; _summaryText = "";
        long quickGeneration = _lifetime.Restart();
        _target = selected; DropSnapshot(); _holding = false;
        Native.GetCursorPos(out var cursor);
        var anchor = selected?.Cursor ?? new PixelPoint(cursor.X, cursor.Y);
        _companion.Anchor = anchor; ApplyPreferences(); AppWindow.Hide();
        _bubble.ShowAt(anchor, walkthrough); _bubble.SetMessage("Little Guy · thinking…", "Looking at the control you pointed at…", true);
        if (!Settings.ScreenEnabled || selected is null)
        { ShowNotice(!Settings.ScreenEnabled ? "Enable screen context in Settings so I can explain what you point at." : "Point at a control in another app, then press Ctrl+[ to explain or Ctrl+] to guide."); return; }
        if (!_connected)
        {
            for (int i = 0; _connecting && i < 100; i++) await Task.Delay(50);
            if (!_connected) await ConnectAsync(false, false);
            if (!_connected) { ShowNotice("Open Settings and connect your ChatGPT account first."); return; }
        }
        if (!_compactMode || _hidden || _paused || !_lifetime.IsCurrent(quickGeneration)) return;
        _walkthrough.Cancel(); ModeCombo.SelectedIndex = 0; _lastQuestion = "What does the control at my pointer do?";
        await SubmitAsync(_lastQuestion);
    }
    private void PollShortcut()
    {
        if (_quitting) return;
        nint foreground = Native.GetForegroundWindow();
        if (foreground != 0 && Native.GetWindowThreadProcessId(foreground, out uint foregroundPid) != 0 && foregroundPid != Environment.ProcessId)
            _lastExternalWindow = foreground;
        if (_holding)
        {
            bool down = (Native.GetAsyncKeyState(0x20) & 0x8000) != 0;
            if (down && !_holdStarted && _clock.Elapsed.TotalSeconds - _holdTime > 0.22 && Settings.VoiceEnabled)
            { _holdStarted = true; StartListening(); }
            if (!down) { _holding = false; if (_holdStarted) StopListening(); }
        }
        if ((_selector.Active || _busy || _speech.Listening || AppWindow.IsVisible || _bubble.Visible) && (Native.GetAsyncKeyState(0x1B) & 1) != 0) Dismiss();
    }
    private void StartListening()
    {
        try { QuestionBox.Text = ""; _speech.Start(); MicButton.Content = "Stop mic"; SetState(CompanionState.Listening, "Listening… release the shortcut when you're done."); }
        catch (Exception ex) { ShowNotice(ex.Message, true); }
    }
    private async void StopListening()
    {
        if (_specialVoice != SpecialVoice.None) { await FinishSpecialVoiceAsync(); return; }
        if (_submittingVoice || !_speech.HasSession) return;
        _submittingVoice = true; _speech.StopListening();
        long generation = _lifetime.Generation;
        try
        {
            SetState(CompanionState.Thinking, "Transcribing on your PC…");
            string transcript = await _speech.FinishAsync();
            if (!_lifetime.IsCurrent(generation)) return;
            QuestionBox.Text = transcript;
            MicButton.Content = "Mic";
            if (!string.IsNullOrWhiteSpace(QuestionBox.Text)) await SubmitAsync(fromVoice: true);
            else ShowNotice("I didn't catch any speech. Try again or type your question.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) ShowNotice(ex.Message, true); }
        finally { _submittingVoice = false; MicButton.Content = "Mic"; VoiceInfo.Text = _speech.RecognitionDescription; }
    }
    private async Task SubmitAsync(string? overrideQuestion = null, WalkthroughAction walkthroughAction = WalkthroughAction.Continue, bool fromVoice = false)
    {
        string question = overrideQuestion ?? QuestionBox.Text.Trim();
        if (_paused || _busy || string.IsNullOrWhiteSpace(question)) return;
        // Explicit supported commands work from ordinary Ask as well as the command shortcut.
        // Walkthrough objectives and questions about a circled item remain explanation requests.
        if (_quickGoalPending) { await StartTutorialGoalAsync(question); return; }
        if (!_circlePending && _walkthrough.Objective is null
            && ModeCombo.SelectedIndex != (int)AnswerMode.Walkthrough
            && VoiceCommandParser.LooksLikeDesktopRequest(question))
        {
            if (!Settings.VoiceCommandsEnabled) { ShowNotice("App and media commands are turned off. Enable them in Settings to run this request."); return; }
            if (fromVoice && _speech.LastConfidence < .6f) { ShowNotice("I heard ‘" + question + "’, but I'm not confident enough to act. Repeat it or type your command."); return; }
            await ExecuteVoiceCommandAsync(question);
            return;
        }
        if (!_connected || _provider is null) { ShowNotice("Connect your ChatGPT account in Settings first."); return; }
        if (!ConfigureTurn()) return;
        if (_walkthrough.Objective is null && ModeCombo.SelectedIndex == (int)AnswerMode.Walkthrough)
        { _walkthrough.Start(question); walkthroughAction = WalkthroughAction.Start; }
        CancelActive(false); long generation = _lifetime.Restart(); var token = _lifetime.Token;
        _busy = true; _summaryText = ""; if (_walkthrough.Objective is null && overrideQuestion is null) _lastQuestion = question; _spokenLength = 0;
        UpdateWalkthroughUi();
        WelcomePanel.Visibility = Visibility.Collapsed; ActionButtons.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible; SendButton.IsEnabled = false;
        QuestionText.Text = question; AnswerText.Text = ""; QuestionBox.Text = ""; Notice.Visibility = Visibility.Collapsed;
        AnswerScroll.ChangeView(null, 0, null);
        SetState(CompanionState.Thinking, "Little Guy is thinking…");
        try
        {
            bool observedThisTurn = false;
            if (_walkthrough.Objective is not null || _compactMode)
            {
                observedThisTurn = await RefreshSelectedAsync(token);
                token.ThrowIfCancellationRequested();
                if ((walkthroughAction == WalkthroughAction.Confirm || _compactMode) && !observedThisTurn)
                {
                    AnswerText.Text = _walkthrough.CurrentInstruction;
                    ShowNotice("I need a fresh view to check this step. Enable screen context, point at the app and press the ask shortcut, then choose I've done this.");
                    return;
                }
            }
            if (_snapshot is not null && !_capture.IsFresh(_snapshot.Window)) { DropSnapshot(); ContextText.Text = "Window changed · text-only follow-up"; }
            AnswerMode mode = _walkthrough.Objective is not null ? AnswerMode.Walkthrough : (AnswerMode)Math.Max(0, ModeCombo.SelectedIndex);
            string request = _walkthrough.Objective is null ? question : _walkthrough.BuildRequest(walkthroughAction, question);
            var answer = await _provider.AskAsync(request, _snapshot, mode, partial => Dispatch(() =>
            {
                if (!_lifetime.IsCurrent(generation)) return;
                AnswerText.Text = partial;
                if (_compactMode && string.IsNullOrEmpty(_summaryText) && !string.IsNullOrWhiteSpace(partial)) _bubble.SetMessage("Little Guy", CompactReply.Preview(null, partial), true);
                if (Settings.AutoSpeak && !_compactMode) SpeakNewSentences(partial, false);
            }), token);
            if (!_lifetime.IsCurrent(generation)) return;
            bool walkthroughComplete = false;
            if (_walkthrough.Objective is not null)
            {
                bool observed = observedThisTurn && _snapshot is not null && answer.CaptureId == _snapshot.Id && _capture.IsFresh(_snapshot.Window);
                var result = _walkthrough.Apply(answer, walkthroughAction, observed);
                if (result == WalkthroughResult.NeedsObservation)
                {
                    AnswerText.Text = _walkthrough.CurrentInstruction;
                    ShowNotice("Little Guy couldn't verify this step against the current window. Point at the app again, then choose I've done this.");
                    return;
                }
                walkthroughComplete = result == WalkthroughResult.Complete;
                if (walkthroughComplete) { ModeCombo.SelectedIndex = 0; SetState(CompanionState.Success, "Walkthrough complete."); }
                UpdateWalkthroughUi();
            }
            AnswerText.Text = answer.Answer; ActionButtons.Visibility = _walkthrough.Objective is null ? Visibility.Visible : Visibility.Collapsed;
            if (_compactMode)
            {
                _summaryText = CompactReply.Preview(answer.Summary, answer.Answer);
                _bubble.SetWalkthrough(_walkthrough.Objective is not null);
                _bubble.SetMessage(walkthroughComplete ? "Little Guy · all done" : _walkthrough.Objective is null ? "Little Guy · quick explanation" : $"Little Guy · step {_walkthrough.Step}", _summaryText, false);
            }
            _store.SaveExchange(question, answer.Answer);
            if (_snapshot is not null && _capture.IsFresh(_snapshot.Window))
            {
                var commands = AnnotationValidator.Validate(answer, _snapshot, DateTimeOffset.UtcNow, Native.Bounds(_snapshot.Window.Handle));
                if (commands.Count > 0) { _overlay.Show(_snapshot, commands, Hwnd); SetState(CompanionState.Guiding, "Follow the highlighted control."); }
            }
            if (Settings.AutoSpeak) SpeakNewSentences(_compactMode ? _summaryText : answer.Answer, true);
            else if (_state != CompanionState.Guiding && !walkthroughComplete) SetState(CompanionState.Idle, _walkthrough.Objective is null ? "Ask a follow-up, or point at something new." : "Follow the step, then press Ctrl+Alt+Enter to check and continue.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_lifetime.IsCurrent(generation)) ShowNotice(ex.Message, true); }
        finally
        {
            if (_lifetime.IsCurrent(generation)) { _busy = false; SendButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; UpdateWalkthroughUi(); }
        }
    }
    private void SpeakNewSentences(string text, bool final)
    {
        if (_narrationSuppressed) return;
        int end = final ? text.Length : 0;
        if (!final) for (int i = _spokenLength; i < text.Length - 1; i++)
            if (text[i] == '\n' || (text[i] is '.' or '?' or '!') && char.IsWhiteSpace(text[i + 1])) end = i + 1;
        if (end <= _spokenLength) return;
        string phrase = text[_spokenLength..end]; _spokenLength = end;
        SpeakNarration(phrase);
    }
    private void CancelActive(bool cancelSpeechInput = true)
    {
        _googleSignIn?.Cancel(); _selector.Cancel(); _specialVoice = SpecialVoice.None; _commandExecuting = false; _holding = false; _overviewActive = false; _lifetime.Cancel(); _overlay.Clear(); _speech.StopSpeaking(); _narrator.Stop(); _narrationSuppressed = false;
        if (cancelSpeechInput) _speech.CancelListening();
        if (_provider is not null) _ = _provider.InterruptAsync();
        _busy = false; SendButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; MicButton.Content = "Mic";
        UpdateWalkthroughUi();
    }
    private void DropSnapshot() { _quickGoalPending = false; _tutorialStartRegion = null; _circlePending = false; _bubble.SetInputHint(null); _snapshot?.Dispose(); _snapshot = null; }
    private void UpdateWalkthroughUi()
    {
        ModeCombo.IsEnabled = !_busy && _walkthrough.Objective is null;
        if (_walkthrough.Objective is not null) ModeCombo.SelectedIndex = (int)AnswerMode.Walkthrough;
        WalkthroughButtons.Visibility = _walkthrough.Objective is null ? Visibility.Collapsed : Visibility.Visible;
        WalkthroughText.Text = $"Step {_walkthrough.Step} · {_walkthrough.Objective}";
        foreach (var row in WalkthroughStepActions.Children.OfType<StackPanel>()) foreach (var button in row.Children.OfType<Button>()) button.IsEnabled = !_busy;
        BackStepButton.IsEnabled = !_busy && _walkthrough.Step > 1;
    }
    private void Pause(bool value)
    {
        if (value) { _replyPad.Clear(); _bubble.Hide(); _companion.Anchor = null; _compactMode = false; }
        _paused = value; CancelActive(); DropSnapshot(); _holding = false;
        _companion.Enabled = Settings.ShowCompanion && !value;
        SetState(value ? CompanionState.Paused : CompanionState.Idle, value ? "Paused. Resume from the tray menu." : "Ready when you are.");
        ApplyPreferences();
    }
    private void TogglePause() => Pause(!_paused);
    private void Dismiss() { if (_compactMode) { HideApp(); return; } CancelActive(); DropSnapshot(); if (PinToggle.IsChecked != true) AppWindow.Hide(); SetState(CompanionState.Idle, "Ready when you are."); }
    private void HideApp() { _replyPad.Hide(); CancelActive(); DropSnapshot(); _holding = false; _hidden = true; _compactMode = false; _bubble.Hide(); _companion.Anchor = null; _companion.Enabled = false; AppWindow.Hide(); }
    private async Task QuitAsync()
    {
        if (_quitting) return;
        CancelActive(); _googleSignIn?.Cancel(); _quitting = true; _frameTimer.Stop(); _keyTimer.Stop();
        DropSnapshot(); _selector.Dispose(); _replyPad.Dispose(); _bubble.Dispose(); _companion.Dispose(); _overlay.Dispose(); _tray.Dispose(); _narrator.Dispose(); _speech.Dispose(); _store.Dispose();
        if (_provider is not null) await _provider.DisposeAsync(); _lifetime.Dispose(); Close(); Application.Current.Exit();
    }
    private async Task UpdateCharacterAsync()
    {
        if (_drawing || _quitting || !AppWindow.IsVisible) return;
        _drawing = true;
        try
        {
            CompanionState state = SettingsPanel.Visibility == Visibility.Visible ? (CompanionState)Math.Max(0, PreviewCombo.SelectedIndex) : _state;
            using var image = SmileyRenderer.Render(220, state, _clock.Elapsed.TotalSeconds, Settings.ReducedMotion);
            using var memory = new MemoryStream(); image.Save(memory, ImageFormat.Png);
            using var random = new InMemoryRandomAccessStream(); using (var writer = new DataWriter(random)) { writer.WriteBytes(memory.ToArray()); await writer.StoreAsync(); writer.DetachStream(); }
            random.Seek(0); var source = new BitmapImage(); await source.SetSourceAsync(random);
            MascotImage.Source = source; SettingsMascotImage.Source = source;
        }
        finally { _drawing = false; }
    }
    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        Settings.VoiceCommandsEnabled = CommandsToggle.IsOn;
        Settings.ScreenEnabled = ScreenToggle.IsOn; Settings.VoiceEnabled = VoiceToggle.IsOn; Settings.AutoSpeak = SpeakToggle.IsOn;
        Settings.HistoryEnabled = HistoryToggle.IsOn; Settings.ReducedMotion = MotionToggle.IsOn; Settings.ShowCompanion = CompanionToggle.IsOn;
        bool shiftChanged = Settings.ShiftHotkey != HotkeyToggle.IsOn; Settings.ShiftHotkey = HotkeyToggle.IsOn;
        if (shiftChanged) _tray.SetHotkey(Settings.ShiftHotkey);
        if (!Settings.ScreenEnabled) { CancelActive(); _replyPad.Clear(); DropSnapshot(); _overlay.Clear(); }
        if (!Settings.VoiceCommandsEnabled && (_specialVoice == SpecialVoice.Command || _commandExecuting)) CancelActive();
        if (!Settings.VoiceEnabled) { if (_specialVoice != SpecialVoice.None || _speech.Listening || _commandExecuting) CancelActive(); else _speech.CancelListening(); } if (!Settings.AutoSpeak) StopNarration();
        _store.SaveSettings(); ApplyPreferences();
    }
    private void Excluded_Changed(object sender, TextChangedEventArgs e) { if (!_loaded) return; Settings.ExcludedApps = ExcludedBox.Text; _store.SaveSettings(); if (_target is not null && !new CapturePolicy(Settings.Exclusions).Allows(_target.ProcessName)) { CancelActive(); DropSnapshot(); _target = null; } }
    private void Preview_Changed(object sender, SelectionChangedEventArgs e) { if (_loaded) _companion.State = (CompanionState)Math.Max(0, PreviewCombo.SelectedIndex); }
    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_loaded || ModelCombo.SelectedItem is not AvailableModel model || _provider is null) return; _provider.Model = model.Id; Settings.Model = model.Id; _store.SaveSettings(); }
    private void DetailEffort_Changed(object sender, SelectionChangedEventArgs e) { if (!_loaded) return; Settings.DetailEffort = DetailEffortCombo.SelectedIndex == 1 ? "high" : "medium"; _store.SaveSettings(); }
    private void AskTab_Click(object sender, RoutedEventArgs e) => ShowTab("ask");
    private void SettingsTab_Click(object sender, RoutedEventArgs e) => ShowTab("settings");
    private void HistoryTab_Click(object sender, RoutedEventArgs e)
    {
        ShowTab("history"); RefreshHistory();
    }
    private async void Connect_Click(object sender, RoutedEventArgs e) => await ConnectAsync(true);
    private async void CheckAccount_Click(object sender, RoutedEventArgs e) { if (_provider is null) await ConnectAsync(false); else await CheckAccountAsync(); }
    private async void Logout_Click(object sender, RoutedEventArgs e) { if (_provider is null) return; try { CancelActive(); await _provider.NewConversationAsync(); await _provider.LogoutAsync(); await CheckAccountAsync(); } catch (Exception ex) { AccountText.Text = ex.Message; } }
    private async void Usage_Click(object sender, RoutedEventArgs e)
    {
        if (_provider is null) { UsageText.Text = "Connect your account first."; return; }
        try
        {
            var usage = await _provider.UsageAsync(); var lines = new List<string>();
            if (usage.TryGetProperty("rateLimitsByLimitId", out var limits) && limits.ValueKind == JsonValueKind.Object)
                foreach (var bucket in limits.EnumerateObject())
                    foreach (string key in new[] { "primary", "secondary" })
                        if (bucket.Value.TryGetProperty(key, out var window) && window.ValueKind == JsonValueKind.Object)
                            lines.Add($"{bucket.Name}: {100 - window.GetProperty("usedPercent").GetDouble():0}% remaining · resets {DateTimeOffset.FromUnixTimeSeconds(window.GetProperty("resetsAt").GetInt64()).ToLocalTime():g}");
            UsageText.Text = lines.Count > 0 ? string.Join("\n", lines) : "Usage information is currently unavailable.";
        }
        catch (Exception ex) { UsageText.Text = ex.Message; }
    }
    private async void Send_Click(object sender, RoutedEventArgs e) => await SubmitAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) { CancelActive(); SetState(CompanionState.Idle, "Stopped."); }
    private void Mic_Click(object sender, RoutedEventArgs e) { if (_speech.Listening) StopListening(); else { CancelActive(); _lifetime.Restart(); StartListening(); } }
    private async void NewChat_Click(object sender, RoutedEventArgs e) { CancelActive(); DropSnapshot(); _walkthrough.Cancel(); _lastQuestion = ""; ModeCombo.SelectedIndex = 0; if (_provider is not null) await _provider.NewConversationAsync(); AnswerText.Text = QuestionText.Text = ""; WelcomePanel.Visibility = Visibility.Visible; ActionButtons.Visibility = WalkthroughButtons.Visibility = Visibility.Collapsed; ContextText.Text = "Text only · no screen attached"; }
    private async void ShowMe_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        CancelActive(); _lifetime.Restart();
        try { await RefreshSelectedAsync(_lifetime.Token); await SubmitAsync("Show me exactly where the control from your last answer is. Only point if it is visible."); }
        catch (OperationCanceledException) { }
    }
    private async Task StartWalkthroughAsync()
    {
        if (_busy) return;
        string objective = string.IsNullOrWhiteSpace(QuestionBox.Text) ? _lastQuestion : QuestionBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(objective)) { ShowNotice("Enter what you want to accomplish, choose Walkthrough, then send your question."); return; }
        _walkthrough.Start(objective); UpdateWalkthroughUi();
        await SubmitAsync(objective, WalkthroughAction.Start);
    }
    private async void Walkthrough_Click(object sender, RoutedEventArgs e) => await StartWalkthroughAsync();
    private async void Done_Click(object sender, RoutedEventArgs e) => await ContinueTutorialAsync();
    private async void Explain_Click(object sender, RoutedEventArgs e) => await SubmitAsync("Explain why this step is needed.", WalkthroughAction.Explain);
    private async void Back_Click(object sender, RoutedEventArgs e) { if (_busy) return; _walkthrough.Back(); UpdateWalkthroughUi(); await SubmitAsync("Revisit the previous instructions.", WalkthroughAction.Back); }
    private async void Repeat_Click(object sender, RoutedEventArgs e) => await SubmitAsync("Repeat this step.", WalkthroughAction.Repeat);
    private async void Skip_Click(object sender, RoutedEventArgs e) => await SubmitAsync("Skip this step and explain any consequence.", WalkthroughAction.Skip);
    private void EndWalkthrough_Click(object sender, RoutedEventArgs e) { CancelActive(); _walkthrough.Cancel(); ModeCombo.SelectedIndex = 0; UpdateWalkthroughUi(); SetState(CompanionState.Idle, "Walkthrough ended."); }
    private async Task<bool> RefreshSelectedAsync(CancellationToken token)
    {
        var tutorialRegion = _tutorialStartRegion;
        DropSnapshot();
        if (!Settings.ScreenEnabled || _target is null) { ContextText.Text = "Text only · point at an app and press the ask shortcut to attach it"; return false; }
        Native.GetCursorPos(out var cursor);
        if (_compactMode || _walkthrough.Objective is not null) cursor = new Native.Point((int)_target.Cursor.X, (int)_target.Cursor.Y);
        // Follow an application's newly opened dialog, while never silently switching to another app.
        if (_lastExternalWindow != 0 && Native.GetWindowThreadProcessId(_lastExternalWindow, out uint pid) != 0 && pid == _target.ProcessId)
            _target = _capture.Describe(_lastExternalWindow, cursor, true) ?? _target;
        _target = _capture.Describe(_target.Handle, cursor, true);
        if (_target is null) { ShowNotice("The selected window is unavailable. Point at it and press the shortcut again."); return false; }
        try
        {
            var snapshot = await _capture.CaptureAsync(_target, new(Settings.Exclusions), token, tutorialRegion);
            if (token.IsCancellationRequested) { snapshot.Dispose(); token.ThrowIfCancellationRequested(); }
            _snapshot = snapshot; ContextText.Text = "Fresh screen · " + _target.ProcessName; return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { ShowNotice(ex.Message, true); return false; }
    }
    private void Copy_Click(object sender, RoutedEventArgs e) { var content = new DataPackage(); content.SetText(AnswerText.Text); Clipboard.SetContent(content); }
    private async void DeleteHistory_Click(object sender, RoutedEventArgs e)
    {
        CancelActive(); DropSnapshot(); _walkthrough.Cancel(); _lastQuestion = ""; _target = null;
        _replyPad.Clear(); _researchReport = null; ResearchResultText.Text = ""; ResearchLink.NavigateUri = null; _store.DeleteHistory();
        try { if (_provider is not null) await _provider.NewConversationAsync(); }
        catch { ShowNotice("Local history deleted. Reconnect Codex to start a new conversation.", true); }
        AnswerText.Text = QuestionText.Text = ""; HistoryItems.Children.Clear();
        ActionButtons.Visibility = WalkthroughButtons.Visibility = Visibility.Collapsed;
        ContextText.Text = "Text only · no screen attached";
        ShowNotice("Local text history deleted.");
    }
    private async void QuestionBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ((Native.GetAsyncKeyState(0x11) | Native.GetAsyncKeyState(0x10)) & 0x8000) != 0)
        { e.Handled = true; await SubmitAsync(); }
    }
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Escape) { e.Handled = true; Dismiss(); } }
    private void CloseNotice_Click(object sender, RoutedEventArgs e) => Notice.Visibility = Visibility.Collapsed;

    private async Task RunSelfTestAsync()
    {
        string directory = Path.Combine(_store.DirectoryPath, "self-test"); Directory.CreateDirectory(directory);
        var report = new Dictionary<string, object> { ["speechAvailable"] = _speech.RecognitionAvailable, ["speech"] = _speech.RecognitionDescription, ["hotkey"] = _tray.HotkeyAvailable };
        try
        {
            using var sheet = new System.Drawing.Bitmap(800, 220); using var graphics = System.Drawing.Graphics.FromImage(sheet); graphics.Clear(System.Drawing.Color.FromArgb(21, 24, 20));
            foreach (CompanionState state in Enum.GetValues<CompanionState>()) { using var face = SmileyRenderer.Render(100, state, 1.5); graphics.DrawImageUnscaled(face, (int)state * 100, 20); using var font = new System.Drawing.Font("Segoe UI", 9); graphics.DrawString(state.ToString(), font, System.Drawing.Brushes.White, (int)state * 100 + 8, 135); }
            sheet.Save(Path.Combine(directory, "characters.png"), ImageFormat.Png);
            await Task.Delay(800); var context = _capture.Describe(Hwnd, new Native.Point(0, 0), true)!;
            using var snapshot = await _capture.CaptureAsync(context, new CapturePolicy([]), CancellationToken.None);
            await File.WriteAllBytesAsync(Path.Combine(directory, "interface.png"), snapshot.Png);
            report["capture"] = "passed"; report["imageWidth"] = snapshot.Transform.ImageWidth; report["imageHeight"] = snapshot.Transform.ImageHeight;
            var probePoint = new Native.Point((int)context.Bounds.X + 130, (int)context.Bounds.Y + 160);
            var before = Native.WindowFromPoint(probePoint);
            _overlay.Show(snapshot, [new Annotation("ring", 100, 120, 100, 50, "Synthetic control")], Hwnd);
            report["overlayClickThrough"] = _overlay.VisibleCount == 1 && Native.WindowFromPoint(probePoint) == before;
            report["overlayCountBeforeMove"] = _overlay.VisibleCount;
            var position = AppWindow.Position;
            AppWindow.Move(new Windows.Graphics.PointInt32(position.X + 20, position.Y));
            await Task.Delay(150);
            report["overlayClearedAfterMove"] = _overlay.VisibleCount == 0;
            AppWindow.Move(position);
            await Task.Delay(500); // Let the compositor settle before the next synthetic capture.
        }
        catch (Exception ex) { report["capture"] = ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetCommandLineArgs().Contains("--tutorial-check")) await RunTutorialCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--stream-visibility-check")) await RunStreamVisibilityCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--walkthrough-check")) await RunWalkthroughCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--drive-check")) await RunDriveCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--narration-check")) await RunNarrationCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--bubble-check")) await RunBubbleCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--reply-check")) await RunReplyCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--overview-check")) await RunOverviewCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--roadmap-check")) await RunRoadmapCheckAsync(directory);
        if (Environment.GetCommandLineArgs().Contains("--command-routing-check")) await RunCommandRoutingCheckAsync(directory);
    }

    private async Task RunBubbleCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        var sample = new Window { Title = "Little Guy synthetic settings", Content = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.White), Padding = new Thickness(30), Child = new TextBlock { Text = "SAMPLE SETTINGS\n\nEnable sound", FontSize = 26, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black) } } };
        try
        {
            sample.AppWindow.Resize(new Windows.Graphics.SizeInt32(700, 400)); sample.AppWindow.Move(new Windows.Graphics.PointInt32(100, 140)); sample.Activate();
            await Task.Delay(500);
            nint sampleHandle = WinRT.Interop.WindowNative.GetWindowHandle(sample);
            var bounds = Native.Bounds(sampleHandle);
            var target = _capture.Describe(sampleHandle, new Native.Point((int)bounds.X + 90, (int)bounds.Y + 100), true)!;
            _provider = new CodexProvider(); await _provider.ConnectAsync(Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE")!, Path.Combine(_store.DirectoryPath, "fixture-codex"));
            _availableModels = await _provider.ModelsAsync(); _connected = true;
            Settings.ScreenEnabled = true; Settings.AutoSpeak = false; Settings.HistoryEnabled = false;
            nint focus = Native.GetForegroundWindow();
            await QuickInvokeAsync(false, target);
            report["explainWithoutPanel"] = _bubble.Visible && !AppWindow.IsVisible && _bubble.Text == "This control changes the sample setting.";
            report["bubbleDidNotStealFocus"] = Native.GetForegroundWindow() == focus;
            report["quickUsesGpt6Low"] = _provider.Model == "gpt-6-astra" && _provider.Effort == "low" && _provider.Compact;
            await Task.Delay(150);
            Native.SetWindowDisplayAffinity(_bubble.Handle, 0);
            using (var image = await _capture.CaptureAsync(_capture.Describe(_bubble.Handle, new Native.Point(0, 0), true)!, new CapturePolicy([]), CancellationToken.None))
                await File.WriteAllBytesAsync(Path.Combine(directory, "bubble.png"), image.Png);
            Native.SetWindowDisplayAffinity(_bubble.Handle, 0x11);
            string? captureId = _snapshot?.Id; string fullAnswer = AnswerText.Text;
            OpenDetails();
            report["openPreservesAnswerAndContext"] = AppWindow.IsVisible && !_bubble.Visible && AnswerText.Text == fullAnswer && _snapshot?.Id == captureId;
            await SubmitAsync("Explain further.");
            report["panelUsesMedium"] = _provider.Effort == "medium" && !_provider.Compact;
            Settings.DetailEffort = "high"; await SubmitAsync("Explain carefully.");
            report["panelUsesHigh"] = _provider.Effort == "high";
            await QuickInvokeAsync(true, target);
            report["tutorialWaitsForGoal"] = _quickGoalPending && _walkthrough.Objective is null;
            await StartTutorialGoalAsync("Enable the sample option");
            report["walkthroughInBubble"] = !AppWindow.IsVisible && _walkthrough.Step == 1 && _bubble.Visible && _bubble.Text == "Open the sample options panel.";
            await QuickInvokeAsync(true, target);
            report["walkthroughAdvancesInBubble"] = _walkthrough.Step == 2 && _bubble.Text == "Enable the sample option.";
            await QuickInvokeAsync(true, target);
            report["walkthroughCompletesInBubble"] = _walkthrough.Objective is null && _bubble.Text.Contains("All done");
            report["explainHotkeyRegistered"] = _tray.ExplainHotkeyAvailable; report["walkthroughHotkeyRegistered"] = _tray.WalkthroughHotkeyAvailable;
            HideApp(); report["hideDismissesBubble"] = !_bubble.Visible && !AppWindow.IsVisible;
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        finally { sample.Close(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "bubble-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }

    private async Task RunWalkthroughCheckAsync(string directory)
    {
        var report = new Dictionary<string, object>();
        try
        {
            string executable = Environment.GetEnvironmentVariable("LITTLEGUY_FIXTURE_EXE") ?? throw new InvalidOperationException("Missing synthetic fixture executable.");
            _provider = new CodexProvider(); await _provider.ConnectAsync(executable, Path.Combine(_store.DirectoryPath, "fixture-codex"));
            _connected = (await _provider.ReadAccountAsync()).Connected;
            Settings.ScreenEnabled = true; Settings.AutoSpeak = false; Settings.HistoryEnabled = false;
            _target = _capture.Describe(Hwnd, new Native.Point(0, 0), true); _lastExternalWindow = 0;
            ModeCombo.SelectedIndex = 0;
            QuestionBox.Text = "Enable the sample option";
            await SubmitAsync();
            await StartWalkthroughAsync();
            await Task.Delay(150);
            report["started"] = _walkthrough.Step == 1 && AnswerText.Text.StartsWith("Step 1") && _walkthrough.Objective == "Enable the sample option";
            var buttonPoint = DoneStepButton.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0, 0));
            report["controlsVisibleBelowLongAnswer"] = WalkthroughButtons.Visibility == Visibility.Visible && DoneStepButton.IsEnabled && buttonPoint.Y > 0 && buttonPoint.Y + DoneStepButton.ActualHeight <= Root.ActualHeight;
            using (var screen = await _capture.CaptureAsync(_capture.Describe(Hwnd, new Native.Point(0, 0), true)!, new CapturePolicy([]), CancellationToken.None))
                await File.WriteAllBytesAsync(Path.Combine(directory, "walkthrough.png"), screen.Png);
            await SubmitAsync("I've done this.", WalkthroughAction.Confirm);
            report["advanced"] = _walkthrough.Step == 2 && AnswerText.Text.StartsWith("Step 2") && _walkthrough.Completed.Count == 1 && DoneStepButton.IsEnabled;
            await SubmitAsync("I've done this.", WalkthroughAction.Confirm);
            report["completed"] = _walkthrough.Objective is null && WalkthroughButtons.Visibility == Visibility.Collapsed && AnswerText.Text.Contains("complete");
            ModeCombo.SelectedIndex = (int)AnswerMode.Walkthrough; QuestionBox.Text = "Another sample objective";
            await SubmitAsync();
            report["startsFromModeSelector"] = _walkthrough.Step == 1 && _walkthrough.Objective == "Another sample objective" && DoneStepButton.IsEnabled;
            EndWalkthrough_Click(this, new RoutedEventArgs());
            report["hideHotkeyRegistered"] = _tray.HideHotkeyAvailable; report["quitHotkeyRegistered"] = _tray.QuitHotkeyAvailable;
            PinToggle.IsChecked = true;
            Native.PostMessageW(_tray.MessageWindow, 0x312, 2, 0);
            await Task.Delay(100);
            report["hideHotkeyWorksWhenPinned"] = !AppWindow.IsVisible && _hidden;
            _hidden = false; ApplyPreferences(); AppWindow.Show(); Activate();
        }
        catch (Exception ex) { report["error"] = ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "walkthrough-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetCommandLineArgs().Contains("--shortcuts-check")) Native.PostMessageW(_tray.MessageWindow, 0x312, 3, 0);
    }
}

using System.Diagnostics;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Codex;

if (args.Contains("--local-speech")) return await LocalSpeechTests.RunAsync(args.Contains("--cpu"));

if (args.Contains("--composer-host")) return ReplyComposerTests.Host();
if (args.Contains("--composer-check")) return await ReplyComposerTests.RunAsync();

if (args.FirstOrDefault() == "app-server") return await FakeServer.RunAsync();

if (args.FirstOrDefault() == "--brand-assets")
{
    string directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
    using var icon = LittleGuy3000.Desktop.Companion.SmileyRenderer.CreateIcon();
    using (var stream = File.Create(Path.Combine(directory, "AppIcon.ico"))) icon.Save(stream);
    using var face = LittleGuy3000.Desktop.Companion.SmileyRenderer.Render(256, CompanionState.Idle, 0, true);
    face.Save(Path.Combine(directory, "LittleGuy3000.png"), System.Drawing.Imaging.ImageFormat.Png);
    foreach (var asset in new (string Name, int Width, int Height)[] {
        ("SplashScreen.scale-200.png",1240,600), ("LockScreenLogo.scale-200.png",48,48),
        ("Square150x150Logo.scale-200.png",300,300), ("Square44x44Logo.scale-200.png",88,88),
        ("Square44x44Logo.targetsize-24_altform-unplated.png",24,24),
        ("Square44x44Logo.targetsize-48_altform-lightunplated.png",48,48),
        ("StoreLogo.png",50,50), ("Wide310x150Logo.scale-200.png",620,300) })
    {
        using var bitmap = new System.Drawing.Bitmap(asset.Width, asset.Height);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        int size = Math.Min(asset.Width, asset.Height);
        using var art = LittleGuy3000.Desktop.Companion.SmileyRenderer.Render(size, CompanionState.Idle, 0, true);
        graphics.DrawImageUnscaled(art, (asset.Width-size)/2, (asset.Height-size)/2);
        bitmap.Save(Path.Combine(directory, asset.Name), System.Drawing.Imaging.ImageFormat.Png);
    }
    Console.WriteLine("Original Little Guy 3000 icon assets generated.");
    return 0;
}

if (args.Contains("--research-live") || args.Contains("--research-fixture"))
{
    bool live = args.Contains("--research-live");
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".local/app/settings.json")));
    await using var provider = new CodexProvider();
    await provider.ConnectAsync(live ? settings.RootElement.GetProperty("CodexExecutable").GetString()! : Path.Combine(AppContext.BaseDirectory, "LittleGuy3000.Tests.exe"), Path.Combine(root, live ? ".local/app/codex" : ".local/research-fixture/codex"));
    provider.Model = "gpt-6-astra"; provider.Effort = "low";
    provider.ResearchActivity += type => Console.WriteLine("Research activity: " + type);
    var answer = await provider.ResearchAsync("Research three Windows 11 keyboard accessibility shortcuts using official Microsoft sources. Keep the report short.", _ => { }, CancellationToken.None);
    Console.WriteLine("Research status: " + answer.Answer + " | " + answer.Research?.Summary);
    answer.Research!.Validate();
    if (provider.ResearchSearchCount < 1) throw new Exception("No live search event was observed; the report is not verified research.");
    string output = Path.Combine(root, live ? ".local/research-live-report.json" : ".local/research-fixture-report.json");
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { searchEvents = provider.ResearchSearchCount, report = answer.Research }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("PASS research: live search events, structured findings, and source URL validation.");
    var followup = await provider.AskAsync("Explain a button", null, AnswerMode.Balanced, _ => { }, CancellationToken.None);
    Console.WriteLine("PASS normal explanation after research: " + !string.IsNullOrWhiteSpace(followup.Answer));
    return 0;
}

if (args.Contains("--transport"))
{
    string directory = Path.Combine(Path.GetTempPath(), "LittleGuy3000-transport-" + Guid.NewGuid().ToString("N"));
    await using var provider = new CodexProvider();
    await provider.ConnectAsync(Path.Combine(AppContext.BaseDirectory, "LittleGuy3000.Tests.exe"), directory);
    var account = await provider.ReadAccountAsync();
    if (!account.Connected) throw new Exception("Account decoding failed.");
    var partials = new List<string>();
    var result = await provider.AskAsync("First question", null, AnswerMode.Balanced, text => partials.Add(text), CancellationToken.None);
    if (result.Answer != "Synthetic answer." || partials.Count == 0) throw new Exception("Streaming response failed.");
    using (var cancel = new CancellationTokenSource(300))
    {
        try { await provider.AskAsync("CANCEL_FIXTURE", null, AnswerMode.Balanced, _ => { }, cancel.Token); throw new Exception("Cancellation was ignored."); }
        catch (OperationCanceledException) { }
    }
    partials.Clear();
    var next = await provider.AskAsync("Next question", null, AnswerMode.Balanced, text => partials.Add(text), CancellationToken.None);
    if (next.Answer != "Synthetic answer." || partials.Any(x => x.Contains("STALE"))) throw new Exception("A stale event contaminated the next turn.");
    await provider.NewConversationAsync();
    Console.WriteLine("PASS real C# client: initialization, isolated thread options, account decoding, streamed JSON, cancellation, late-event suppression, new conversation and child disposal.");
    return 0;
}

if (args.Contains("--overview-live"))
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    await using var provider = new CodexProvider { Model = "gpt-6-astra", Effort = "low", Compact = true };
    await provider.ConnectAsync(Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE")!, Path.Combine(root,".local/app/codex"));
    using var bitmap = new System.Drawing.Bitmap(1000,600);
    using (var g = System.Drawing.Graphics.FromImage(bitmap))
    {
        g.Clear(System.Drawing.Color.White); using var font = new System.Drawing.Font("Segoe UI",24);
        g.DrawString("SAMPLE AUDIO INTERFACE\n\nGain [knob]     Mix [dial]\n\nBypass [button]\n\nOutput meter [|||||]",font,System.Drawing.Brushes.Black,30,30);
    }
    using var png = new MemoryStream(); bitmap.Save(png,System.Drawing.Imaging.ImageFormat.Png);
    var overviewWindow = new WindowContext(0,0,"SyntheticInterface","Sample audio controls",new(0,0,1000,600),new(300,200),96);
    using var overviewImage = new ScreenSnapshot("overview-live",DateTimeOffset.UtcNow,overviewWindow,new(overviewWindow.Bounds,1000,600),png.ToArray());
    var answer = await provider.ExplainInterfaceAsync(overviewImage,_ => {},CancellationToken.None);
    Console.WriteLine(answer.Answer);
    if (answer.CaptureId != overviewImage.Id || !new[] { "Gain", "Mix", "Bypass", "meter" }.All(t=>answer.Answer.Contains(t,StringComparison.OrdinalIgnoreCase))) return 3;
    Console.WriteLine("PASS live interface overview covers all four sample controls."); return 0;
}

if (args.Contains("--reply-live") || args.Contains("--reply-fixture"))
{
    bool live = args.Contains("--reply-live");
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    string executable = live ? Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE")! : Path.Combine(AppContext.BaseDirectory, "LittleGuy3000.Tests.exe");
    await using var provider = new CodexProvider { Model = "gpt-6-astra", Effort = "low", Compact = true };
    await provider.ConnectAsync(executable, live ? Path.Combine(root, ".local/app/codex") : Path.Combine(root, ".local/reply-fixture/codex"));
    if (!(await provider.ReadAccountAsync()).Connected) return 2;
    for (int i = 0; i < 2; i++)
    {
        using var bitmap = new System.Drawing.Bitmap(900, 650);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.White); using var font = new System.Drawing.Font("Segoe UI", 20);
            g.DrawString("MY POST: Meet Little Guy 3000.", font, System.Drawing.Brushes.Black, 30, 30);
            g.DrawString("Alex: This looks useful!", font, System.Drawing.Brushes.Black, 30, 130);
            g.DrawString(i == 0 ? "Reply to Alex:" : "Sam: Love the smiley.", font, System.Drawing.Brushes.Black, 30, 240);
            if (i == 0) g.DrawRectangle(System.Drawing.Pens.Gray, 30, 290, 800, 150);
        }
        using var png = new MemoryStream(); bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var replyWindow = new WindowContext(0, 0, "SyntheticReplies", i == 0 ? "SINGLE_FIXTURE" : "MULTIPLE_FIXTURE", new(0,0,900,650), new(100,350),96);
        using var replySnapshot = new ScreenSnapshot("reply-" + i, DateTimeOffset.UtcNow, replyWindow, new(replyWindow.Bounds,900,650), png.ToArray());
        var response = await provider.DraftRepliesAsync(replySnapshot, "Friendly, concise. No emojis. Do not invent personal experiences.", i == 0 ? "Reply to Alex; x=30 y=290 width=800 height=150" : "No focused field", CancellationToken.None);
        var replyBatch = ReplyDraftPolicy.Validate(response, replySnapshot.Id);
        Console.WriteLine(JsonSerializer.Serialize(replyBatch));
        if (replyBatch.Layout != (i == 0 ? "single" : "multiple") || replyBatch.Drafts.Count != i + 1 || i == 0 && !replyBatch.FocusedComposerMatches || i == 1 && ReplyDraftPolicy.CanInsert(replyBatch)) return 3;
    }
    var normal = await provider.AskAsync("Give a one sentence explanation of a volume control.", null, AnswerMode.QuickAnswer, _ => { }, CancellationToken.None);
    if (normal.Replies is not null || string.IsNullOrWhiteSpace(normal.Answer)) return 4;
    Console.WriteLine("PASS reply batches, current capture ID, Low effort, tone input, and return to explanation mode.");
    return 0;
}

if (args.Contains("--speech"))
{
    using var wave = new MemoryStream();
    using (var synth = new SpeechSynthesizer()) { synth.SetOutputToWaveStream(wave); synth.Speak("Show me how to change this setting."); synth.SetOutputToNull(); }
    wave.Position = 0;
    using var recognizer = new SpeechRecognitionEngine(); recognizer.LoadGrammar(new DictationGrammar()); recognizer.SetInputToWaveStream(wave);
    var timer = Stopwatch.StartNew(); var result = recognizer.Recognize(TimeSpan.FromSeconds(15));
    Console.WriteLine(JsonSerializer.Serialize(new { recognized = result?.Text, confidence = result?.Confidence, elapsedMs = timer.ElapsedMilliseconds, input = "Synthetic speech, in-memory only" }));
    Array.Clear(wave.GetBuffer());
    return result?.Text.Contains("setting", StringComparison.OrdinalIgnoreCase) == true ? 0 : 1;
}
if (args.Contains("--walkthrough-live"))
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    string codex = Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE") ?? throw new Exception("Set LITTLEGUY_CODEX_EXE.");
    await using var provider = new CodexProvider();
    await provider.ConnectAsync(codex, Path.Combine(root, ".local/app/codex"));
    if (!(await provider.ReadAccountAsync()).Connected) return 2;
    var flow = new WalkthroughSession(); flow.Start("Open the options panel and enable sound in this sample application.");
    for (int stage = 0; stage < 3; stage++)
    {
        using var bitmap = new System.Drawing.Bitmap(700, 400);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.White);
            using var font = new System.Drawing.Font("Segoe UI", 22);
            string screen = stage switch {
                0 => "SAMPLE SETTINGS APPLICATION\nOptions panel: CLOSED\n\n[Open options]",
                1 => "SAMPLE SETTINGS APPLICATION\nOptions panel: OPEN\nSound: OFF\n[Enable sound]",
                _ => "SAMPLE SETTINGS APPLICATION\nOptions panel: OPEN\nSound: ON\nSettings saved successfully" };
            g.DrawString(screen, font, System.Drawing.Brushes.Black, 20, 25);
        }
        using var png = new MemoryStream(); bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var context = new WindowContext(0, 0, "SampleSettings", "Synthetic walkthrough", new(0, 0, 700, 400), new(100, 200), 96);
        using var screenImage = new ScreenSnapshot("walkthrough-" + stage, DateTimeOffset.UtcNow, context, new(context.Bounds, 700, 400), png.ToArray());
        var action = stage == 0 ? WalkthroughAction.Start : WalkthroughAction.Confirm;
        var response = await provider.AskAsync(flow.BuildRequest(action, stage == 0 ? flow.Objective! : "I've done this."), screenImage, AnswerMode.Walkthrough, _ => { }, CancellationToken.None);
        var result = flow.Apply(response, action, response.CaptureId == screenImage.Id);
        Console.WriteLine(JsonSerializer.Serialize(new { stage, response.Answer, response.StepStatus, matchedCapture = response.CaptureId == screenImage.Id, result = result.ToString(), step = flow.Step }));
        if (stage == 0 && result != WalkthroughResult.Waiting || stage == 1 && result != WalkthroughResult.Advanced || stage == 2 && result != WalkthroughResult.Complete) return 3;
        Array.Clear(png.GetBuffer());
    }
    return 0;
}
if (args.Contains("--login-link"))
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    string codex = Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE") ?? throw new Exception("Set LITTLEGUY_CODEX_EXE.");
    await using var provider = new CodexProvider();
    await provider.ConnectAsync(codex, Path.Combine(root, ".local/app/codex"));
    Console.WriteLine((await provider.StartLoginAsync()).AbsoluteUri);
    Console.Out.Flush();
    var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
    while (DateTimeOffset.UtcNow < deadline)
    {
        await Task.Delay(2000);
        if ((await provider.ReadAccountAsync()).Connected) { Console.WriteLine("SIGN_IN_COMPLETE"); return 0; }
    }
    Console.WriteLine("SIGN_IN_EXPIRED"); return 2;
}
if (args.Contains("--account") || args.Contains("--live") || args.Contains("--catalog") || args.Contains("--quick-live"))
{
    string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    string codex = Environment.GetEnvironmentVariable("LITTLEGUY_CODEX_EXE") ?? throw new Exception("Set LITTLEGUY_CODEX_EXE.");
    await using var provider = new CodexProvider();
    await provider.ConnectAsync(codex, Path.Combine(root, ".local/app/codex"));
    var account = await provider.ReadAccountAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { connected = account.Connected }));
    if (!account.Connected) return 2;
    if (args.Contains("--catalog")) { Console.WriteLine(JsonSerializer.Serialize(await provider.ModelsAsync())); return 0; }
    if (args.Contains("--live") || args.Contains("--quick-live"))
    {
        var timer = Stopwatch.StartNew(); long? summaryMs = null;
        if (args.Contains("--quick-live")) { provider.Model = "gpt-6-astra"; provider.Effort = "low"; provider.Compact = true; }
        provider.SummaryProgress += text => { if (!string.IsNullOrWhiteSpace(text)) summaryMs ??= timer.ElapsedMilliseconds; };
        var answer = await provider.AskAsync("Give a one-sentence explanation of a volume control. Do not use tools.", null, AnswerMode.QuickAnswer, _ => { }, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { answer = answer.Answer, annotationCount = answer.Annotations?.Count ?? 0 }));
        var models = await provider.ModelsAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { availableModelCount = models.Count }));
        using var drawing = new System.Drawing.Bitmap(400, 200);
        using (var graphics = System.Drawing.Graphics.FromImage(drawing))
        {
            graphics.Clear(System.Drawing.Color.White);
            graphics.FillRectangle(System.Drawing.Brushes.LightGreen, 80, 60, 240, 80);
            using var font = new System.Drawing.Font("Segoe UI", 22);
            graphics.DrawString("Enable sound", font, System.Drawing.Brushes.Black, 96, 80);
        }
        using var png = new MemoryStream(); drawing.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var syntheticWindow = new WindowContext(0, 0, "SyntheticFixture", "Synthetic test image", new(0, 0, 400, 200), new(160, 100), 96);
        using var syntheticScreen = new ScreenSnapshot("live-synthetic-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, syntheticWindow, new(syntheticWindow.Bounds, 400, 200), png.ToArray());
        var visual = await provider.AskAsync("Read the exact two-word label in the green rectangle. Do not emit annotations.", syntheticScreen, AnswerMode.QuickAnswer, _ => { }, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { syntheticImageAnswer = visual.Answer }));
        Array.Clear(png.GetBuffer());
        if (!visual.Answer.Contains("Enable sound", StringComparison.OrdinalIgnoreCase)) return 3;
        if (args.Contains("--quick-live"))
        {
            if (string.IsNullOrWhiteSpace(visual.Summary)) throw new Exception("The quick response omitted its summary.");
            Console.WriteLine(JsonSerializer.Serialize(new { quickSummary = visual.Summary, firstSummaryMs = summaryMs, effort = provider.Effort }));
            provider.Effort = "medium"; provider.Compact = false;
            var detail = await provider.AskAsync("Briefly explain what that button would do, and distinguish what you can see from assumptions.", null, AnswerMode.TeachMe, _ => { }, CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(new { detail.Answer, detail.Summary, effort = provider.Effort }));
        }
    }
    return 0;
}

int passed = 0;
void Check(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); Environment.Exit(1); }
}
void Require(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
Check("mixed-DPI transform round trips including negative desktop coordinates", () =>
{
    foreach (double dpi in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var origin in new[] { new PixelPoint(-2560, -1440), new PixelPoint(0, 0), new PixelPoint(1920, 240) })
        {
            var transform = new ScreenTransform(new(origin.X, origin.Y, 1920 * dpi, 1080 * dpi), 1440, 810);
            for (int x = 0; x <= 1440; x += 17)
            {
                var p = new PixelPoint(x, 300); var roundTrip = transform.ToImage(transform.ToDesktop(p));
                Require(Math.Abs(roundTrip.X - p.X) < 0.00001 && Math.Abs(roundTrip.Y - p.Y) < 0.00001);
            }
        }
});
Check("invalid transform cannot be used", () => { try { new ScreenTransform(new(0, 0, 0, 4), 0, 1).ToDesktop(new(1, 1)); throw new Exception("Accepted zero-size image"); } catch (InvalidOperationException) { } });
var window = new WindowContext(123, 456, "test", "Synthetic", new(-1920, 0, 1920, 1080), new(-900, 100), 144);
using var snapshot = new ScreenSnapshot("capture-a", DateTimeOffset.UtcNow, window, new(window.Bounds, 1280, 720), [1, 2, 3]);
AssistantAnswer Answer(params Annotation[] commands) => new("Synthetic answer", snapshot.Id, commands.ToList(), null);
int Valid(AssistantAnswer answer, PixelRect? bounds = null, DateTimeOffset? time = null) => AnnotationValidator.Validate(answer, snapshot, time ?? DateTimeOffset.UtcNow, bounds ?? window.Bounds).Count;
Check("valid annotation accepted", () => Require(Valid(Answer(new Annotation("ring", 10, 10, 30, 40, "Control"))) == 1));
Check("stale capture rejected", () => Require(Valid(Answer(new Annotation("ring", 10, 10, 30, 40, "Control")), time: DateTimeOffset.UtcNow.AddMinutes(1)) == 0));
Check("different capture rejected", () => Require(Valid(Answer(new Annotation("ring", 10, 10, 30, 40, "Control")) with { CaptureId = "old" }) == 0));
Check("moved window rejected", () => Require(Valid(Answer(new Annotation("ring", 10, 10, 30, 40, "Control")), new(0, 0, 1920, 1080)) == 0));
Check("non-finite geometry rejected", () => Require(Valid(Answer(new Annotation("ring", double.NaN, 1, 4, 4, "Control"), new("ring", 1, 1, double.PositiveInfinity, 4, "Control"))) == 0));
Check("off-image geometry rejected", () => Require(Valid(Answer(new Annotation("ring", 1279, 10, 20, 20, "Control"), new("ring", -2, 0, 3, 3, "Control"))) == 0));
Check("unknown shapes rejected", () => Require(Valid(Answer(new Annotation("execute", 1, 1, 4, 4, "Control"))) == 0));
Check("oversized labels rejected", () => Require(Valid(Answer(new Annotation("ring", 1, 1, 4, 4, new string('a', 81)))) == 0));
Check("annotation flood rejected", () => Require(Valid(Answer(Enumerable.Repeat(new Annotation("ring", 1, 1, 4, 4, "Control"), 7).ToArray())) == 0));
Check("capture exclusions normalize paths case and extensions", () => { var policy = new CapturePolicy(["Bitwarden.exe", "keepassxc"]); Require(!policy.Allows("BITWARDEN")); Require(!policy.Allows("KeePassXC.exe")); Require(policy.Allows("notepad")); Require(!policy.Allows("")); });
Check("cancellation rejects late generation", () => { using var lifetime = new TurnLifetime(); long first = lifetime.Restart(); var oldToken = lifetime.Token; long next = lifetime.Restart(); Require(oldToken.IsCancellationRequested && !lifetime.IsCurrent(first) && lifetime.IsCurrent(next)); lifetime.Cancel(); Require(!lifetime.IsCurrent(next)); });
Check("walkthrough supports more than fifteen verified steps", () => { var flow = new WalkthroughSession(); flow.Start("Objective"); for (int i = 0; i < 18; i++) flow.Apply(new("Next instruction", "fresh", [], "verified"), WalkthroughAction.Confirm, true); Require(flow.Step == 19 && flow.Completed.Count == 18 && flow.Objective == "Objective"); flow.Back(); Require(flow.Step == 18 && flow.Completed.Count == 17); flow.Cancel(); Require(flow.Step == 0 && flow.Objective is null); });
Check("whole-window yellow overlays rejected", () => Require(Valid(Answer(new Annotation("ring", 0, 0, 1280, 720, "Window"))) == 0));
Check("walkthrough start and follow-up cannot auto-advance", () => { var flow = new WalkthroughSession(); flow.Start("Original objective"); flow.Apply(new("First instruction", "fresh", [], "complete"), WalkthroughAction.Start, true); flow.Apply(new("Explanation", "fresh", [], "verified"), WalkthroughAction.Continue, true); Require(flow.Step == 1 && flow.Objective == "Original objective" && flow.Completed.Count == 0); });
Check("walkthrough refuses unobserved completion and preserves instruction", () => { var flow = new WalkthroughSession(); flow.Start("Objective"); flow.Apply(new("Open settings", null, [], "pending"), WalkthroughAction.Start, false); var result = flow.Apply(new("All done", null, [], "complete"), WalkthroughAction.Confirm, false); Require(result == WalkthroughResult.NeedsObservation && flow.Step == 1 && flow.CurrentInstruction == "Open settings"); });
Check("walkthrough explain and repeat preserve the action being verified", () => { var flow = new WalkthroughSession(); flow.Start("Objective"); flow.Apply(new("Open settings", null, [], "pending"), WalkthroughAction.Start, true); flow.Apply(new("Because it is needed", null, [], "verified"), WalkthroughAction.Explain, true); flow.Apply(new("Repeated instruction", null, [], "complete"), WalkthroughAction.Repeat, true); Require(flow.Step == 1 && flow.CurrentInstruction == "Open settings"); flow.Apply(new("Enable sound", "fresh", [], "verified"), WalkthroughAction.Confirm, true); Require(flow.Step == 2 && flow.Completed.Single() == "Open settings"); });
Check("walkthrough skip advances without claiming verification", () => { var flow = new WalkthroughSession(); flow.Start("Objective"); flow.Apply(new("Optional setting", null, [], "pending"), WalkthroughAction.Start, false); flow.Apply(new("Next setting", null, [], "pending"), WalkthroughAction.Skip, false); Require(flow.Step == 2 && flow.Completed.Single() == "Skipped: Optional setting"); flow.Back(); Require(flow.Step == 1 && flow.Completed.Count == 0); });
Check("walkthrough completes only on observed confirmation", () => { var flow = new WalkthroughSession(); flow.Start("Objective"); var result = flow.Apply(new("Finished", "fresh", [], "complete"), WalkthroughAction.Confirm, true); Require(result == WalkthroughResult.Complete && flow.Objective is null); });
Check("partial JSON answer streams without exposing geometry", () => Require(CodexProvider.ExtractPartialAnswer("{\"answer\":\"Hello\\nworld\",\"annotations\":[{") == "Hello\nworld"));
Check("incomplete JSON escape withheld", () => Require(CodexProvider.ExtractPartialAnswer("{\"answer\":\"Hello\\u26") == "Hello"));
Check("unicode JSON escape decoded", () => Require(CodexProvider.ExtractPartialAnswer("{\"answer\":\"Hello \\u263a\"}") == "Hello ☺"));
Check("screenshot disposal clears application buffer", () => { var image = new ScreenSnapshot("x", DateTimeOffset.UtcNow, window, snapshot.Transform, [1, 2]); image.Dispose(); Require(image.Png.All(b => b == 0)); });
Check("compact summary streams before the full answer", () => { var json = "{\"summary\":\"Enable sound.\",\"answer\":\"The full explanation"; Require(CodexProvider.ExtractPartialString(json, "summary") == "Enable sound." && CodexProvider.ExtractPartialAnswer(json) == "The full explanation"); });
Check("research and ChatGPT voice commands route explicitly", () => {
    Require(VoiceCommandParser.Parse("Hey Little Guy, research budget microphones and save a Google Sheet").Action == "research");
    Require(VoiceCommandParser.LooksLikeDesktopRequest("Do some research on microphones"));
    Require(VoiceCommandParser.Parse("Open ChatGPT and start a new chat about gardening").Target == "gardening");
    Require(VoiceCommandParser.Parse("Ask Chat GPT to help plan my project").Action == "chatgpt");
    Require(VoiceCommandParser.Parse("Open ChatGPT").Action == "chatgpt");
    Require(VoiceCommandParser.Parse("What is ChatGPT?").Action == "unsupported");
});
Check("ChatGPT handoff encodes the request without changing route or mode", () => {
    var uri = ChatGptHandoff.NewChat("Hello &mode=codex#stuff");
    Require(uri.Host == "threads" && uri.AbsolutePath == "/new" && uri.Query.StartsWith("?mode=chat&prompt=") && uri.Fragment == "");
});
Check("research export escapes HTML and neutralizes spreadsheet formulas", () => {
    var report = new ResearchReport("<script>test</script>", "Summary", [new("Topic", "=IMPORTXML(1)", "Source", "https://example.com/source")]);
    Require(!report.ToHtml().Contains("<script>")); Require(report.ToCsv().Contains("'=IMPORTXML"));
    Require(!ResearchReport.PublicSource("file:///private") && !ResearchReport.PublicSource("https://127.0.0.1/x"));
});
Check("bubble preview is bounded and uses full answer when summary is absent", () => { Require(CompactReply.Preview(null, "A short answer.") == "A short answer."); Require(CompactReply.Preview(new string('x', 500), "").Length <= 240); });
Check("bubble stays above the companion across DPI and monitor edges", () => {
    foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
    foreach (var work in new[] { new PixelRect(-1920, -200, 1920, 1080), new PixelRect(0, 0, 1920, 1080) })
    foreach (var anchor in new[] { new PixelPoint(work.X, work.Y), new PixelPoint(work.Right - 2, work.Bottom - 2), new PixelPoint(work.X + 600, work.Y + 500) }) {
        var layout = CompactReply.PlaceAbove(anchor, work, 380 * scale, 360 * scale, scale);
        var bubble = layout.Bubble; double headTop = layout.CompanionAnchor.Y + 28 * scale;
        Require(bubble.X >= work.X && bubble.Y >= work.Y && bubble.Right <= work.Right && bubble.Bottom < headTop);
        Require(headTop + 74 * scale <= work.Bottom);
    }
});
Check("bubble remains inside mixed-DPI monitor work areas", () => { foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 }) foreach (var work in new[] { new PixelRect(-1920, -200, 1920, 1080), new PixelRect(0, 0, 1920, 1080) }) foreach (var anchor in new[] { new PixelPoint(work.X, work.Y), new PixelPoint(work.Right - 2, work.Bottom - 2) }) { var placed = CompactReply.Place(anchor, work, 360 * scale, 290 * scale, scale); Require(placed.X >= work.X && placed.Y >= work.Y && placed.Right <= work.Right && placed.Bottom <= work.Bottom); } });
var reply = new ReplyDraft("Alex", "Looks useful!", "Thanks, Alex!");
var batch = new ReplyBatch("single", true, [reply]);
AssistantAnswer DraftAnswer(ReplyBatch b, string id = "capture-a") => new("Ready", id, [], null, "Ready", b);
Check("single matched reply can insert", () => Require(ReplyDraftPolicy.CanInsert(ReplyDraftPolicy.Validate(DraftAnswer(batch), "capture-a"))));
Check("multiple and uncertain batches never auto-insert", () => { Require(!ReplyDraftPolicy.CanInsert(batch with { Layout = "multiple" })); Require(!ReplyDraftPolicy.CanInsert(batch with { Layout = "uncertain" })); Require(!ReplyDraftPolicy.CanInsert(batch with { FocusedComposerMatches = false })); });
Check("reply batch rejects stale context and invalid output", () => {
    foreach (var bad in new[] { DraftAnswer(batch, "stale"), DraftAnswer(batch with { Drafts = [] }), DraftAnswer(batch with { Drafts = [reply with { Text = "bad\u001btext" }] }), DraftAnswer(batch with { Layout = "multiple", Drafts = Enumerable.Repeat(reply, 13).ToList() }), DraftAnswer(batch with { Drafts = [reply with { Comment = "" }] }) })
    { try { ReplyDraftPolicy.Validate(bad, "capture-a"); throw new Exception("Invalid draft accepted"); } catch (InvalidDataException) { } }
});
Check("selection normalizes all drag directions", () => {
    var expected = new PixelRect(-1200, -300, 400, 200);
    Require(InterfaceOverview.Rectangle(new(-1200,-300), new(-800,-100)) == expected);
    Require(InterfaceOverview.Rectangle(new(-800,-100), new(-1200,-300)) == expected);
    Require(InterfaceOverview.Rectangle(new(-1200,-100), new(-800,-300)) == expected);
});
Check("selection clips to a single window and rejects tiny or disjoint regions", () => {
    Require(InterfaceOverview.Clip(new(-50,-50,200,200), new(0,0,100,100)) == new PixelRect(0,0,100,100));
    foreach (var selection in new[] { new PixelRect(200,200,100,100), new PixelRect(0,0,10,10), new PixelRect(double.NaN,0,50,50) })
    { try { InterfaceOverview.Clip(selection,new(0,0,100,100)); throw new Exception("Invalid selection accepted"); } catch (InvalidOperationException) { } }
});
Check("voice commands preserve explicit app and song targets",()=>{
    Require(VoiceCommandParser.Parse("Open calculator.")==new VoiceCommand("open","calculator"));
    Require(VoiceCommandParser.Parse("Open my Spotify and play")==new VoiceCommand("play","spotify"));
    Require(VoiceCommandParser.Parse("Play the playlist Sunday Chill on Spotify")==new VoiceCommand("spotify_play","Sunday Chill"));
    Require(VoiceCommandParser.Parse("Play Yesterday by The Beatles on Spotify")==new VoiceCommand("spotify_play","Yesterday by The Beatles"));
});
Check("voice command boundary rejects arbitrary execution and communication",()=>{
    foreach(string command in new[] { "delete all files", "send a message", "open powershell", "open cmd /c calc", "buy a subscription", "click Send", "run shutdown.exe" }) Require(VoiceCommandParser.Parse(command).Action=="unsupported");
});
Check("Spotify compound requests accept spoken punctuation and polite prefixes",()=>{
    foreach (string text in new[] { "Open Spotify, Play ADHD Techno.", "Open Spotify. Play ADHD Techno.", "Open Spotify and then play ADHD Techno", "Open Spotify; then play ADHD Techno", "Hey Little Guy, can you please open Spotify, play ADHD Techno?" })
        Require(VoiceCommandParser.Parse(text)==new VoiceCommand("spotify_play","ADHD Techno"));
    Require(VoiceCommandParser.Parse("Open Spotify, play Earth, Wind & Fire")==new VoiceCommand("spotify_play","Earth, Wind & Fire"));
    Require(VoiceCommandParser.Parse("Open Spotify, open Calculator").Action=="unsupported");
    Require(VoiceCommandParser.Parse("Open Spotify and delete all files").Action=="unsupported");
});
Check("unmatched desktop requests stay local while explanatory questions stay questions",()=>{
    Require(VoiceCommandParser.LooksLikeDesktopRequest("Can you please open an unknown app?"));
    Require(VoiceCommandParser.LooksLikeDesktopRequest("Open Spotify, launch something else"));
    Require(!VoiceCommandParser.LooksLikeDesktopRequest("How do I open Spotify?"));
    Require(!VoiceCommandParser.LooksLikeDesktopRequest("What does the play button do?"));
});
Check("closed circle uses its full stroke rather than identical endpoints",()=>{
    var points=Enumerable.Range(0,25).Select(i=>new PixelPoint(100+50*Math.Cos(i*Math.PI*2/24),100+40*Math.Sin(i*Math.PI*2/24))).ToArray();
    var area=InterfaceOverview.CircleBounds(points);Require(area.Width>=99&&area.Height>=79);
});
Check("Spotify library matching distinguishes the requested playlist from similarly named items",()=>{
    Require(SpotifyControlLabels.IsLibraryPlaylist("ADHD Techno", "ADHD Techno Playlist • Saive"));
    Require(SpotifyControlLabels.IsLibraryPlaylist("ADHD Techno", "ADHD Techno Pinned Playlist • Saive"));
    Require(!SpotifyControlLabels.IsLibraryPlaylist("ADHD Techno", "ADHD Techno Remix Playlist • Saive"));
    Require(!SpotifyControlLabels.IsLibraryPlaylist("ADHD Techno", "ADHD Techno Artist"));
});
Check("Spotify playback matching rejects generic transport buttons and unrelated versions",()=>{
    Require(SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Play ADHD Techno"));
    Require(SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Play ADHD Techno by Saive"));
    Require(SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Pause ADHD Techno", true));
    Require(!SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Play"));
    Require(!SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Play ADHD Techno Remix"));
    Require(!SpotifyControlLabels.IsPlaybackButton("ADHD Techno", "Pause ADHD Techno"));
    Require(SpotifyControlLabels.SearchArgument("Earth, Wind & Fire") == "--protocol-uri=spotify:search:Earth%2C%20Wind%20%26%20Fire");
});
Console.WriteLine($"{passed} checks passed.");
return 0;

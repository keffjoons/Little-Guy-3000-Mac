using System.Text.Json;

internal static class FakeServer
{
    internal static async Task<int> RunAsync()
    {
        string active = ""; string? previous = null; int turnNumber = 0, walkthroughConfirmations = 0; bool researchThread = false;
        async Task Write(object value) { await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value)); await Console.Out.FlushAsync(); }
        async Task Event(string method, object body) => await Write(new Dictionary<string, object> { ["method"] = method, ["params"] = body });
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using var document = JsonDocument.Parse(line); var message = document.RootElement;
            if (!message.TryGetProperty("id", out var id)) continue;
            var p = message.GetProperty("params");
            object result = new { };
            switch (message.GetProperty("method").GetString())
            {
                case "initialize": result = new { userAgent = "LittleGuy3000 fixture" }; break;
                case "account/read": result = new { account = new { type = "chatgpt", email = "fixture@example.invalid", planType = "fixture" } }; break;
                case "model/list": result = new { data = new[] { new { id = "gpt-6-astra", displayName = "GPT-6 Astra", isDefault = true, supportedReasoningEfforts = new[] { new { reasoningEffort = "low" }, new { reasoningEffort = "medium" }, new { reasoningEffort = "high" } } } } }; break;
                case "thread/start":
                    researchThread = p.TryGetProperty("config", out var config) && config.GetProperty("web_search").GetString() == "live";
                    if (!p.GetProperty("ephemeral").GetBoolean() || p.GetProperty("environments").GetArrayLength() != 0 || p.GetProperty("dynamicTools").GetArrayLength() != 0) return 1;
                    result = new { thread = new { id = "fixture-thread" } }; break;
                case "turn/start":
                    active = "fixture-turn-" + ++turnNumber;
                    await Event("turn/started", new { threadId = "fixture-thread", turn = new { id = active } });
                    await Write(new { id = id.Clone(), result = new { turn = new { id = active } } });
                    if (p.GetRawText().Contains("CANCEL_FIXTURE")) { previous = active; continue; }
                    if (previous is not null) await Event("item/agentMessage/delta", new { threadId = "fixture-thread", turnId = previous, delta = "STALE" });
                    string text = string.Join("\n", p.GetProperty("input").EnumerateArray().Where(x => x.GetProperty("type").GetString() == "text").Select(x => x.GetProperty("text").GetString()));
                    if (text.Contains("Quick bubble response:") && (p.GetProperty("effort").GetString() != "low" || p.GetProperty("model").GetString() != "gpt-6-astra")) return 6;
                    bool walkthrough = text.Contains("Walkthrough objective (retain until ended):");
                    string? capture = System.Text.RegularExpressions.Regex.Match(text, "captureId=([a-zA-Z0-9-]+)") is { Success: true } match ? match.Groups[1].Value : null;
                    if (walkthrough && (capture is null || !p.GetProperty("input").EnumerateArray().Any(x => x.GetProperty("type").GetString() == "image"))) return 4;
                    bool confirming = text.Contains("Action: Confirm\n");
                    if (text.Contains("Action: Start\n")) walkthroughConfirmations = 0;
                    if (walkthrough && confirming) walkthroughConfirmations++;
                    string answer = !walkthrough ? "Synthetic answer." : confirming ? walkthroughConfirmations == 1 ? "Step 2: Enable the sample option." : "The sample option is enabled. Your walkthrough is complete."
                        : "Step 1: Open the sample options panel.\n\n" + string.Join("\n\n", Enumerable.Repeat("Synthetic explanation to exercise scrolling. The step controls must remain visible below this answer.", 12));
                    string? stepStatus = !walkthrough ? null : confirming ? walkthroughConfirmations == 1 ? "verified" : "complete" : "pending";
                    string summary = walkthrough ? confirming ? walkthroughConfirmations == 1 ? "Enable the sample option." : "All done. The sample option is enabled." : "Open the sample options panel." : "This control changes the sample setting.";
                    string json = JsonSerializer.Serialize(new { summary, answer, captureId = capture, annotations = Array.Empty<object>(), stepStatus });
                    if (p.GetProperty("outputSchema").GetProperty("properties").TryGetProperty("replies", out _))
                    {
                        if (capture is null || p.GetProperty("effort").GetString() != "low" || !text.Contains("toneGuidelines")) return 7;
                        bool multiple = text.Contains("MULTIPLE_FIXTURE");
                        var drafts = new List<object> { new { recipient = "Alex", comment = "This looks useful!", text = "Thanks, Alex! Glad it looks useful." } };
                        if (multiple) drafts.Add(new { recipient = "Sam", comment = "Love the smiley.", text = "Thanks, Sam! The smiley is my favourite part too." });
                        json = JsonSerializer.Serialize(new { summary = "Drafts ready.", answer = "Review your drafts.", captureId = capture,
                            replies = new { layout = multiple ? "multiple" : "single", focusedComposerMatches = !multiple, drafts } });
                    }
                    if (text.Contains("INTERFACE_OVERVIEW:"))
                    {
                        if (capture is null || text.Contains("Quick bubble response:")) return 8;
                        json = JsonSerializer.Serialize(new { summary = "This audio interface controls gain, mix and bypass, with an output meter.",
                            answer = "Interface map: input, blend and output.\n\nGain knob: adjusts input level. Mix dial: blends dry and wet signal. Bypass button: disables processing. Output meter: displays signal level.\n\n"
                                + string.Join(" ", Enumerable.Repeat("Synthetic detailed control explanation with practical context.", 22)), captureId = capture, annotations = Array.Empty<object>(), stepStatus = (string?)null });
                    }
                    if (p.GetProperty("outputSchema").GetProperty("properties").TryGetProperty("research", out _))
                    {
                        if (!researchThread) return 9;
                        await Event("item/agentMessage/delta", new { threadId = "fixture-thread", turnId = active, itemId = "progress-" + active, delta = "Looking up current sources." });
                        await Event("item/completed", new { threadId = "fixture-thread", turnId = active, item = new { id = "search-1", type = "webSearch", query = "synthetic" } });
                        json = JsonSerializer.Serialize(new { summary = "Research ready", answer = "Found sources", research = new { title = "Keyboard shortcuts", summary = "Sourced report", findings = new[] { new { topic = "Magnifier", finding = "Windows plus Plus opens Magnifier", sourceTitle = "Microsoft", sourceUrl = "https://support.microsoft.com/windows" } } } });
                    }
                    else if (researchThread) return 10;
                    foreach (string part in new[] { json[..14], json[14..25], json[25..] }) await Event("item/agentMessage/delta", new { threadId = "fixture-thread", turnId = active, itemId = "final-" + active, delta = part });
                    await Event("turn/completed", new { threadId = "fixture-thread", turn = new { id = active, status = "completed" } });
                    continue;
                case "turn/interrupt":
                    await Event("turn/completed", new { threadId = "fixture-thread", turn = new { id = p.GetProperty("turnId").GetString(), status = "interrupted" } }); break;
            }
            await Write(new { id = id.Clone(), result });
        }
        return 0;
    }
}

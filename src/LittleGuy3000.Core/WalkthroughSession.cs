namespace LittleGuy3000.Core;

public enum WalkthroughAction { Start, Continue, Confirm, Explain, Repeat, Back, Skip }
public enum WalkthroughResult { Waiting, Advanced, Complete, NeedsObservation }

/// <summary>Owns step transitions; model text alone never advances a walkthrough.</summary>
public sealed class WalkthroughSession
{
    public string? Objective { get; private set; }
    public int Step { get; private set; }
    public string CurrentInstruction { get; private set; } = "";
    public List<string> Completed { get; } = [];
    public void Start(string objective)
    {
        if (string.IsNullOrWhiteSpace(objective)) throw new ArgumentException("Enter a walkthrough objective.", nameof(objective));
        Objective = objective.Trim(); Step = 1; CurrentInstruction = ""; Completed.Clear();
    }
    public string BuildRequest(WalkthroughAction action, string question)
    {
        if (Objective is null) throw new InvalidOperationException("No walkthrough is active.");
        string directive = action switch
        {
            WalkthroughAction.Start => "Start with ONE actionable step. Set stepStatus=pending; do not verify or complete anything yet.",
            WalkthroughAction.Confirm => "The user says they performed the current step. Inspect the NEW image. If its result is visible, set stepStatus=verified and give ONE next step. Only set complete if the entire objective is visibly achieved. If the action did not succeed or there is no fresh evidence, keep the current step and set uncertain. Never treat the user's confirmation alone as proof.",
            WalkthroughAction.Explain => "Explain the current instruction without advancing. Set stepStatus=pending.",
            WalkthroughAction.Repeat => "Repeat the current instruction without advancing. Set stepStatus=pending.",
            WalkthroughAction.Back => "Revisit the current numbered step. Back only revisits instructions; it does not undo any action in the application. Set stepStatus=pending.",
            WalkthroughAction.Skip => "The user explicitly skipped the current step. Explain its consequence briefly and give ONE instruction for the next step. Do not claim the skipped action succeeded. Set stepStatus=pending.",
            _ => "Answer the follow-up while keeping the current step. Set stepStatus=pending. Do not advance until the user selects I've done this."
        };
        return $"Tutorial guidance: Stay focused on the user's requested outcome. Give one concrete action at a time, name the visible control and describe the expected result. Do not invent controls, values or successful changes. If the goal is ambiguous, ask one concise question before choosing a workflow. If a needed control is not visible, guide navigation to it rather than guessing its position. Explain how to make the requested thing; do not claim to have built or changed it yourself.\nWalkthrough objective (retain until ended): {Objective}\nCurrent step number: {Step}\nCurrent instruction: {CurrentInstruction}\nPrior steps: {string.Join(" | ", Completed.TakeLast(12))}\nAction: {action}\n{directive}\nUser message: {question}";
    }
    public WalkthroughResult Apply(AssistantAnswer answer, WalkthroughAction action, bool freshObservation)
    {
        if (Objective is null) throw new InvalidOperationException("No walkthrough is active.");
        if (action == WalkthroughAction.Confirm && answer.StepStatus is "verified" or "complete")
        {
            if (!freshObservation) return WalkthroughResult.NeedsObservation;
            Completed.Add(CurrentInstruction);
            if (answer.StepStatus == "complete") { Objective = null; CurrentInstruction = answer.Answer; return WalkthroughResult.Complete; }
            Step++; CurrentInstruction = answer.Answer; return WalkthroughResult.Advanced;
        }
        if (action == WalkthroughAction.Skip)
        {
            Completed.Add("Skipped: " + CurrentInstruction); Step++; CurrentInstruction = answer.Answer; return WalkthroughResult.Advanced;
        }
        if (action is not (WalkthroughAction.Explain or WalkthroughAction.Repeat)) CurrentInstruction = answer.Answer;
        return WalkthroughResult.Waiting;
    }
    public void Back()
    {
        if (Objective is null || Step <= 1) return;
        Step--;
        if (Completed.Count > 0) { CurrentInstruction = Completed[^1]; Completed.RemoveAt(Completed.Count - 1); }
    }
    public void Cancel() { Objective = null; Step = 0; CurrentInstruction = ""; Completed.Clear(); }
}

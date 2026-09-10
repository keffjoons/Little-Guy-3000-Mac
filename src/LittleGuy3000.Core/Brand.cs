namespace LittleGuy3000.Core;

public static class Brand
{
    public const string Name = "Little Guy 3000";
    public const string Nickname = "Little Guy";
    public const string Identifier = "LittleGuy3000";
    public const string Tagline = "Point. Ask. Know.";
    public const string Version = "0.1.13";
}

public enum CompanionState { Idle, Listening, Thinking, Speaking, Guiding, Success, Error, Paused }
public enum AnswerMode { Balanced, QuickAnswer, TeachMe, Troubleshoot, Walkthrough }

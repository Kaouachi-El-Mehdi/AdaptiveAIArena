using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public enum Condition { AInitial, ATuned, B, ATuning }

// Who is playing and which condition comes next. Static, so it survives scene loads
// (one scene per brain type).
public static class ParticipantSession
{
    public const string StartScene = "Session_Start";
    public const string BrainAScene = "Session_BrainA";
    public const string BrainBScene = "Session_BrainB";

    public const int RoundsPerCondition = 5;
    public const int RoundsPerTuningBlock = 10;

    // All 6 orders of the 3 conditions. Counterbalancing: participant n gets order (n - 1) mod 6,
    // so with 6, 12, 18... participants every order is used equally often.
    // Participant 0 = test run (uses order 0, excluded from analysis).
    public static readonly Condition[][] Orders =
    {
        new[] { Condition.AInitial, Condition.ATuned, Condition.B },
        new[] { Condition.AInitial, Condition.B, Condition.ATuned },
        new[] { Condition.ATuned, Condition.AInitial, Condition.B },
        new[] { Condition.ATuned, Condition.B, Condition.AInitial },
        new[] { Condition.B, Condition.AInitial, Condition.ATuned },
        new[] { Condition.B, Condition.ATuned, Condition.AInitial },
    };

    // Automated check of the whole flow (no human): launch the session build with -autoSessionTest.
    // The training bot plays as participant P00 at 20x speed and every rating is 3. Test rows only.
    public static readonly bool AutoTest = System.Environment.GetCommandLineArgs().Contains("-autoSessionTest");

    public static bool Active { get; private set; }
    public static string ParticipantId { get; private set; }
    public static Condition[] Order { get; private set; }
    public static int Position { get; private set; } // 0-based index into Order

    public static Condition Current => Order[Position];
    public static bool IsTuningBlock => Current == Condition.ATuning;
    public static string OrderText => string.Join("|", Order.Select(Name));

    public static string Name(Condition c) => c switch
    {
        Condition.AInitial => "A-initial",
        Condition.ATuned => "A-tuned",
        Condition.B => "B",
        _ => "A-tuning",
    };

    public static void BeginParticipant(int number)
    {
        ParticipantId = $"P{number:00}";
        Order = Orders[number == 0 ? 0 : (number - 1) % Orders.Length];
        Begin();
    }

    public static void BeginTuningBlock()
    {
        ParticipantId = "DEV";
        Order = new[] { Condition.ATuning };
        Begin();
    }

    static void Begin()
    {
        Active = true;
        Position = 0;
        LoadCurrent();
    }

    // Returns false when the last condition is finished.
    public static bool Advance()
    {
        Position++;
        if (Position >= Order.Length) return false;
        LoadCurrent();
        return true;
    }

    public static void BackToMenu()
    {
        Active = false;
        SceneManager.LoadScene(StartScene);
    }

    static void LoadCurrent() => SceneManager.LoadScene(Current == Condition.B ? BrainBScene : BrainAScene);
}

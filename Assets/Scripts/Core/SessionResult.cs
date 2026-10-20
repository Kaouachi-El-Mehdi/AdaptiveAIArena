using System;

public enum RoundOutcome { Win, Loss, Timeout }

// Schema for the mémoire's Résultats data. The first 8 fields are the original locked
// schema; the 4 enemy_* stat fields were appended on 2026-10-15 so every row records the
// stat set in force (Brain A's stats change across tuning iterations). The 7 session fields
// (participant, condition, ratings) were appended on 2026-10-20. Only append new fields at
// the end, never rename or reorder.
[Serializable]
public class SessionResult
{
    public string brainType;
    public string sessionId;
    public string timestamp;
    public RoundOutcome outcome;
    public float timeToComplete;
    public float damageDealt;
    public float damageTaken;
    public int tuningIterationNumber; // Brain A only; always 0 for Brain B sessions

    public float enemyMoveSpeed;
    public float enemyAttackCooldown;
    public float enemyAttackDamage;
    public float enemyDetectionRange;

    // Human sessions (SessionRoundLoop). Empty / 0 for rounds played outside the session flow.
    public string participantId;      // "P01", ... ; "P00" = test run, "DEV" = Brain A tuning block
    public string condition;          // "A-initial", "A-tuned", "B" or "A-tuning"
    public string conditionOrder;     // the participant's order, e.g. "B|A-initial|A-tuned"
    public int conditionPosition;     // 1-3: position of this condition in that order
    public int roundInCondition;      // 1-5 (1-10 in a tuning block)
    public int difficultyRating;      // 1 = too easy ... 5 = too hard; 0 = not asked
    public int enjoymentRating;       // 1 = not at all ... 5 = very much; 0 = not asked
}

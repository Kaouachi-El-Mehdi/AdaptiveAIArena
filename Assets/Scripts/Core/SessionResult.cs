using System;

public enum RoundOutcome { Win, Loss, Timeout }

// Schema for the mémoire's Résultats data. The first 8 fields are the original locked
// schema; the 4 enemy_* stat fields were appended on 2026-10-15 so every row records the
// stat set in force (Brain A's stats change across tuning iterations). Only append new
// fields at the end, never rename or reorder.
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
}

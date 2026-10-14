using System;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public Health playerHealth;
    public Health enemyHealth;

    [Header("Session metadata (set per test batch)")]
    public string brainType = "A";
    public int tuningIterationNumber = 0;

    public bool RoundEnded { get; private set; }
    public SessionResult LastResult { get; private set; }

    float roundStartTime;

    void Start()
    {
        roundStartTime = Time.time;
        RoundEnded = false;
    }

    void Update()
    {
        if (RoundEnded) return;

        float elapsed = Time.time - roundStartTime;

        // Outcome is from the enemy brain's perspective (same as the damage columns),
        // since each row scores the brain being tested: enemy kills player = Win.
        if (playerHealth.IsDead) EndRound(RoundOutcome.Win, elapsed);
        else if (enemyHealth.IsDead) EndRound(RoundOutcome.Loss, elapsed);
        else if (elapsed >= GameConstants.RoundTimeoutSeconds) EndRound(RoundOutcome.Timeout, elapsed);
    }

    void EndRound(RoundOutcome outcome, float elapsed)
    {
        RoundEnded = true;

        // All fields are from the enemy brain's perspective, since brainType
        // describes the enemy being tested.
        LastResult = new SessionResult
        {
            brainType = brainType,
            sessionId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow.ToString("o"),
            outcome = outcome,
            timeToComplete = elapsed,
            damageDealt = playerHealth.DamageTaken,
            damageTaken = enemyHealth.DamageTaken,
            tuningIterationNumber = tuningIterationNumber
        };

        Debug.Log($"[Round End] outcome={outcome} time={elapsed:F1}s dmgDealt={LastResult.damageDealt} dmgTaken={LastResult.damageTaken}");
        SessionLogger.Append(LastResult);
    }
}

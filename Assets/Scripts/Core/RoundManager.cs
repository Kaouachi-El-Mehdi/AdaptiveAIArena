using System;
using Unity.MLAgents;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public Health playerHealth;
    public Health enemyHealth;

    [Header("Session metadata (set per test batch)")]
    [Tooltip("Brain A only. Bump after every manual tuning pass and record it in LabLog.md.")]
    public int tuningIterationNumber = 0;

    // Combat Debug.Logs help when playtesting but flood the console (and slow it down) in training.
    public static bool CombatLogs = true;

    public bool RoundEnded { get; private set; }
    public SessionResult LastResult { get; private set; }
    public event Action<SessionResult> OnRoundEnd;

    public float Elapsed => Time.time - roundStartTime;
    public float TimeRemainingFraction => Mathf.Clamp01(1f - Elapsed / GameConstants.RoundTimeoutSeconds);

    // True when mlagents-learn is connected. Training rounds are never written to sessions.csv,
    // and rounds restart automatically instead of waiting for the next Play.
    static bool IsTraining => Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;

    // Set by BotCalibration (Brain A vs bot runs). Same treatment as training: automatic
    // restarts, no sessions.csv rows; BotCalibration writes its own CSV.
    [HideInInspector] public bool calibrationRun;
    bool Simulated => IsTraining || calibrationRun;

    IEnemyBrain brain;
    PlayerProxyBot bot;
    Vector2 playerSpawn, enemySpawn;
    float roundStartTime;

    void Start()
    {
        brain = enemyHealth.GetComponent<IEnemyBrain>();
        if (brain == null) Debug.LogError("[RoundManager] Enemy has no brain (EnemyBrainA or EnemyBrainB).");
        bot = playerHealth.GetComponent<PlayerProxyBot>();

        playerSpawn = playerHealth.transform.position;
        enemySpawn = enemyHealth.transform.position;
        CombatLogs = !Simulated;

        StartRound();
    }

    // Physics tick, like all gameplay logic, so time_to_complete stays precise at 20x time scale.
    void FixedUpdate()
    {
        if (RoundEnded) return;

        float elapsed = Elapsed;

        // Outcome is from the enemy brain's perspective (same as the damage columns),
        // since each row scores the brain being tested: enemy kills player = Win.
        if (playerHealth.IsDead) EndRound(RoundOutcome.Win, elapsed);
        else if (enemyHealth.IsDead) EndRound(RoundOutcome.Loss, elapsed);
        else if (elapsed >= GameConstants.RoundTimeoutSeconds) EndRound(RoundOutcome.Timeout, elapsed);
    }

    void StartRound()
    {
        // Small random spawn offsets in training only, so Brain B doesn't overfit one start position.
        bool training = IsTraining;
        Place(playerHealth, playerSpawn + (training ? UnityEngine.Random.insideUnitCircle * 1.5f : Vector2.zero));
        Place(enemyHealth, enemySpawn + (training ? UnityEngine.Random.insideUnitCircle * 1.5f : Vector2.zero));

        playerHealth.ResetHealth();
        enemyHealth.ResetHealth();
        if (bot != null) bot.ResetForRound();

        roundStartTime = Time.time;
        RoundEnded = false;
    }

    static void Place(Health combatant, Vector2 position)
    {
        var rb = combatant.GetComponent<Rigidbody2D>();
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        combatant.transform.position = position;
    }

    void EndRound(RoundOutcome outcome, float elapsed)
    {
        RoundEnded = true;
        EnemyStats stats = brain.Stats;

        // All fields are from the enemy brain's perspective, since brainType
        // describes the enemy being tested.
        LastResult = new SessionResult
        {
            brainType = brain.BrainType,
            sessionId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow.ToString("o"),
            outcome = outcome,
            timeToComplete = elapsed,
            damageDealt = playerHealth.DamageTaken,
            damageTaken = enemyHealth.DamageTaken,
            tuningIterationNumber = brain.BrainType == "A" ? tuningIterationNumber : 0,
            enemyMoveSpeed = stats.moveSpeed,
            enemyAttackCooldown = stats.attackCooldown,
            enemyAttackDamage = stats.attackDamage,
            enemyDetectionRange = stats.detectionRange
        };

        OnRoundEnd?.Invoke(LastResult);

        if (Simulated)
        {
            StartRound();
            return;
        }

        Debug.Log($"[Round End] brain={LastResult.brainType} outcome={outcome} time={elapsed:F1}s dmgDealt={LastResult.damageDealt} dmgTaken={LastResult.damageTaken}");
        SessionLogger.Append(LastResult);
    }
}

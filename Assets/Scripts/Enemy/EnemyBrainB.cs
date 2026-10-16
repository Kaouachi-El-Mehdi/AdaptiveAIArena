using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// Brain B: ML-Agents PPO agent. Same body as Brain A at its INITIAL stats (move speed,
// cooldown, damage, detection range, fixed attack range, same attack rule); only the
// decisions differ. The reward targets a close match (Dynamic Difficulty Adjustment),
// not winning.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrainB : Agent, IEnemyBrain
{
    public const string BehaviorName = "BrainB";
    public const int ObservationSize = 9;
    public static readonly int[] ActionBranches = { 9, 2 }; // move direction (0 = stay, 1-8 = compass), attack (0/1)

    public Health health;
    public Transform playerTransform;
    public Health playerHealth;
    public RoundManager roundManager;

    // Not serialized on purpose: Brain B's stats must stay at Brain A's initial values.
    public EnemyStats Stats { get; } = EnemyStats.Initial();
    public string BrainType => "B";

    static readonly Vector2[] Directions = BuildDirections();

    Rigidbody2D rb;
    float lastAttackTime = -999f;
    float lastPotential;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        roundManager.OnRoundEnd += HandleRoundEnd;
    }

    public override void OnEpisodeBegin()
    {
        lastAttackTime = -999f;
        lastPotential = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        // Same perception limit as Brain A: the player's position is only known within detectionRange.
        bool visible = toPlayer.magnitude <= Stats.detectionRange;

        sensor.AddObservation(rb.position.x / (GameConstants.ArenaWidth / 2f));
        sensor.AddObservation(rb.position.y / (GameConstants.ArenaHeight / 2f));
        sensor.AddObservation(visible ? 1f : 0f);
        sensor.AddObservation(visible ? toPlayer / Stats.detectionRange : Vector2.zero);
        // Both HP values: a DDA agent has to know how close the match is (Brain A doesn't use them).
        sensor.AddObservation(health.Current / health.maxHealth);
        sensor.AddObservation(playerHealth.Current / playerHealth.maxHealth);
        sensor.AddObservation(Mathf.Clamp01((Time.time - lastAttackTime) / Stats.attackCooldown));
        sensor.AddObservation(roundManager.TimeRemainingFraction);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (roundManager.RoundEnded || health.IsDead || playerHealth.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = Directions[actions.DiscreteActions[0]] * Stats.moveSpeed;
        if (actions.DiscreteActions[1] == 1) TryAttack();

        // Dense shaping: reward shrinking the HP gap, penalise widening it.
        // Telescopes over a round to -0.5 * final gap, so it only speeds up learning.
        float potential = -0.5f * HpGap();
        AddReward(potential - lastPotential);
        lastPotential = potential;
    }

    // Same rule as Brain A: fixed range, cooldown, damage. Out-of-range attempts do nothing.
    void TryAttack()
    {
        if (Time.time - lastAttackTime < Stats.attackCooldown) return;
        if (Vector2.Distance(rb.position, playerTransform.position) > GameConstants.AttackRange) return;

        playerHealth.TakeDamage(Stats.attackDamage);
        lastAttackTime = Time.time;
        if (RoundManager.CombatLogs)
            Debug.Log($"Enemy hit Player for {Stats.attackDamage} (Player HP: {playerHealth.Current}/{playerHealth.maxHealth})");
    }

    void HandleRoundEnd(SessionResult r)
    {
        // Terminal DDA reward in [0, 1]: high when the round was an actual fight (engagement)
        // AND ended with both sides close in HP. Winning or losing by a wide margin scores ~0,
        // and so does avoiding the fight (100 vs 100 at timeout = zero engagement).
        float gap = HpGap();
        float engagement = Mathf.Clamp01((r.damageDealt + r.damageTaken) / GameConstants.StartingHealth);
        AddReward(engagement * (1f - gap));

        var recorder = Academy.Instance.StatsRecorder;
        recorder.Add("DDA/FinalHpGap", gap);
        recorder.Add("DDA/Engagement", engagement);
        recorder.Add("DDA/EnemyWinRate", r.outcome == RoundOutcome.Win ? 1f : 0f);
        recorder.Add("DDA/EnemyLossRate", r.outcome == RoundOutcome.Loss ? 1f : 0f);
        recorder.Add("DDA/TimeoutRate", r.outcome == RoundOutcome.Timeout ? 1f : 0f);

        EndEpisode();
    }

    float HpGap() => Mathf.Abs(health.Current - playerHealth.Current) / GameConstants.StartingHealth;

    // Debug stand-in when no trained model is assigned: chase and attack, like Brain A.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        d[0] = toPlayer.magnitude > GameConstants.AttackRange * 0.9f ? ClosestDirection(toPlayer) : 0;
        d[1] = 1;
    }

    static int ClosestDirection(Vector2 v)
    {
        int sector = Mathf.RoundToInt(Mathf.Atan2(v.y, v.x) / (Mathf.PI / 4f));
        return 1 + ((sector % 8) + 8) % 8;
    }

    static Vector2[] BuildDirections()
    {
        var dirs = new Vector2[9];
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            dirs[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
        return dirs;
    }
}

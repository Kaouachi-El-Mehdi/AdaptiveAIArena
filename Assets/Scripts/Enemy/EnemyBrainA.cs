using UnityEngine;

public enum BrainAState { Idle, Chase, Attack }

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrainA : MonoBehaviour, IEnemyBrain
{
    [Header("Tunable parameters (manual changes here = logged tuning iterations)")]
    public EnemyStats stats = EnemyStats.Initial();

    public Health health;
    public Transform playerTransform;
    public Health playerHealth;

    public BrainAState CurrentState { get; private set; } = BrainAState.Idle;
    public string BrainType => "A";
    public EnemyStats Stats => stats;

    Rigidbody2D rb;
    float lastAttackTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Physics tick, not Update: same behaviour at any time scale (see PlayerController).
    void FixedUpdate()
    {
        if (health.IsDead || playerHealth.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > stats.detectionRange) CurrentState = BrainAState.Idle;
        else if (dist > GameConstants.AttackRange) CurrentState = BrainAState.Chase;
        else CurrentState = BrainAState.Attack;

        switch (CurrentState)
        {
            case BrainAState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;

            case BrainAState.Chase:
                Vector2 dir = ((Vector2)playerTransform.position - rb.position).normalized;
                rb.linearVelocity = dir * stats.moveSpeed;
                break;

            case BrainAState.Attack:
                rb.linearVelocity = Vector2.zero;
                TryAttack();
                break;
        }
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < stats.attackCooldown) return;
        playerHealth.TakeDamage(stats.attackDamage);
        lastAttackTime = Time.time;
        if (RoundManager.CombatLogs)
            Debug.Log($"Enemy hit Player for {stats.attackDamage} (Player HP: {playerHealth.Current}/{playerHealth.maxHealth})");
    }
}

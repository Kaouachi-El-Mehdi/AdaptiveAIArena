using UnityEngine;

public enum BrainAState { Idle, Chase, Attack }

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrainA : MonoBehaviour
{
    [Header("Tunable parameters (manual changes here = logged tuning iterations)")]
    public float moveSpeed = 3f;
    public float attackRange = 1.2f;
    public float attackDamage = 8f;
    public float attackCooldown = 0.8f;
    public float detectionRange = 6f;

    public Health health;
    public Transform playerTransform;
    public Health playerHealth;

    public BrainAState CurrentState { get; private set; } = BrainAState.Idle;

    Rigidbody2D rb;
    float lastAttackTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (health.IsDead || playerHealth.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > detectionRange) CurrentState = BrainAState.Idle;
        else if (dist > attackRange) CurrentState = BrainAState.Chase;
        else CurrentState = BrainAState.Attack;

        switch (CurrentState)
        {
            case BrainAState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;

            case BrainAState.Chase:
                Vector2 dir = ((Vector2)playerTransform.position - rb.position).normalized;
                rb.linearVelocity = dir * moveSpeed;
                break;

            case BrainAState.Attack:
                rb.linearVelocity = Vector2.zero;
                TryAttack();
                break;
        }
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        playerHealth.TakeDamage(attackDamage);
        lastAttackTime = Time.time;
    }
}

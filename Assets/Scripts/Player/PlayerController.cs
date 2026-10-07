using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float attackRange = 1.2f;
    public float attackDamage = 10f;
    public float attackCooldown = 0.6f;

    public Health health;
    public Transform enemyTransform;
    public Health enemyHealth;

    Rigidbody2D rb;
    float lastAttackTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 move = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;

        rb.linearVelocity = move.normalized * moveSpeed;

        if (kb.spaceKey.wasPressedThisFrame)
            TryAttack();
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        if (enemyHealth == null || enemyHealth.IsDead) return;

        float dist = Vector2.Distance(transform.position, enemyTransform.position);
        if (dist <= attackRange)
        {
            enemyHealth.TakeDamage(attackDamage);
            lastAttackTime = Time.time;
        }
    }
}

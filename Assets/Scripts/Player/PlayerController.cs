using UnityEngine;
using UnityEngine.InputSystem;

// Player stats are constants (GameConstants), so they are identical whether a human
// or the training bot is playing.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public Health health;
    public Transform enemyTransform;
    public Health enemyHealth;

    [Tooltip("Empty = keyboard control. Set = scripted bot (Brain B's training opponent).")]
    public PlayerProxyBot bot;

    Rigidbody2D rb;
    float lastAttackTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Keyboard input is read every frame (so no key press is missed) and buffered;
    // movement and attacks are applied on the physics tick. Gameplay logic runs in FixedUpdate
    // so it behaves the same at any time scale (ML-Agents trains at 20x speed, where Update
    // only runs ~3 times per game-second).
    Vector2 keyboardMove;
    bool keyboardAttackQueued;

    void Update()
    {
        if (bot != null) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        keyboardMove = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyboardMove.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyboardMove.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardMove.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardMove.x += 1f;
        if (kb.spaceKey.wasPressedThisFrame) keyboardAttackQueued = true;
    }

    void FixedUpdate()
    {
        Vector2 move;
        bool attack;

        if (bot != null)
        {
            move = bot.Move;
            attack = bot.ConsumeAttack();
        }
        else
        {
            move = keyboardMove;
            attack = keyboardAttackQueued;
            keyboardAttackQueued = false;
        }

        // Same as normalizing for keyboard input, but lets the bot move slower than full speed.
        rb.linearVelocity = Vector2.ClampMagnitude(move, 1f) * GameConstants.PlayerMoveSpeed;

        if (attack)
            TryAttack();
    }

    void TryAttack()
    {
        if (Time.time - lastAttackTime < GameConstants.PlayerAttackCooldown)
        {
            if (RoundManager.CombatLogs) Debug.Log("Player attack on cooldown.");
            return;
        }
        if (enemyHealth == null || enemyHealth.IsDead) return;

        float dist = Vector2.Distance(transform.position, enemyTransform.position);
        if (dist <= GameConstants.AttackRange)
        {
            enemyHealth.TakeDamage(GameConstants.PlayerAttackDamage);
            lastAttackTime = Time.time;
            if (RoundManager.CombatLogs)
                Debug.Log($"Player hit Enemy for {GameConstants.PlayerAttackDamage} (Enemy HP: {enemyHealth.Current}/{enemyHealth.maxHealth})");
        }
        else if (RoundManager.CombatLogs)
        {
            Debug.Log($"Player attack missed — out of range ({dist:F1} > {GameConstants.AttackRange}).");
        }
    }
}

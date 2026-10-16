using System;

// The four tunable enemy stats. Brain A's tuning iterations change these; Brain B always
// uses Initial(), so the A-vs-B comparison differs only in decisions. Attack range is not
// here: it is fixed for everyone (GameConstants.AttackRange).
[Serializable]
public class EnemyStats
{
    public float moveSpeed;
    public float attackCooldown;
    public float attackDamage;
    public float detectionRange;

    // Brain A's untuned values (tuning iteration 0). Don't edit: Brain B and the
    // "A-initial" analysis condition both depend on them.
    public static EnemyStats Initial() => new EnemyStats
    {
        moveSpeed = 3f,
        attackCooldown = 0.8f,
        attackDamage = 8f,
        detectionRange = 6f
    };
}

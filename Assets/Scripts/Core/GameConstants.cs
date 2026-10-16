public static class GameConstants
{
    public const float RoundTimeoutSeconds = 60f;
    public const float ArenaWidth = 16f;
    public const float ArenaHeight = 10f;
    public const float StartingHealth = 100f;

    // Same for player, Brain A and Brain B, and NOT a tunable parameter
    // ("symmetric abilities" = same action set + same attack range).
    public const float AttackRange = 1.2f;

    // Player stats are fixed by design, identical in training and in real sessions.
    // The gap with Brain A's initial stats is intentional: it is what Brain A's
    // manual tuning has to correct.
    public const float PlayerMoveSpeed = 4f;
    public const float PlayerAttackDamage = 10f;
    public const float PlayerAttackCooldown = 0.6f;
}

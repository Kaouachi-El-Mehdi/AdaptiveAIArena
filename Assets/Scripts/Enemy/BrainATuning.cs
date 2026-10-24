// Brain A's CURRENT tuned stats, used by the "A-tuned" session condition and by tuning blocks.
// One tuning iteration = change these values + bump Iteration + write the LabLog.md entry
// (new values, block close-count, minutes spent). Max 8 iterations (decided 2026-10-20).
// Iteration 0 = untuned = EnemyStats.Initial(). Read at runtime, so no scene rebuild is needed
// after editing (only for a new standalone build).
public static class BrainATuning
{
    public const int Iteration = 1;

    public static EnemyStats Stats() => new EnemyStats
    {
        moveSpeed = 3f,
        attackCooldown = 0.8f,
        attackDamage = 10f,
        detectionRange = 6f
    };
}

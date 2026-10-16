using UnityEngine;

// Scripted stand-in for the human player: Brain B's training opponent.
// It drives the same PlayerController (same fixed player stats) as the human; only the
// input source changes. Skill is re-rolled every round so Brain B trains against a range
// of opponents: a DDA agent that only ever met one skill level would have nothing to adapt to.
[DefaultExecutionOrder(-10)] // decide before PlayerController applies Move/attack on the same tick
public class PlayerProxyBot : MonoBehaviour
{
    public Transform enemyTransform;

    [Range(0f, 1f)] public float skill = 0.6f;
    public bool randomizeSkillEachRound = true;
    public float minSkill = 0.2f;
    public float maxSkill = 1f;

    public Vector2 Move { get; private set; }

    bool attackQueued;
    float nextThinkTime;
    float retreatUntil;

    public void ResetForRound()
    {
        if (randomizeSkillEachRound) skill = Random.Range(minSkill, maxSkill);
        Move = Vector2.zero;
        attackQueued = false;
        nextThinkTime = 0f;
        retreatUntil = 0f;
    }

    // Read once per physics tick by PlayerController.
    public bool ConsumeAttack()
    {
        bool a = attackQueued;
        attackQueued = false;
        return a;
    }

    // Physics tick, not Update: same reaction times at any time scale (see PlayerController).
    void FixedUpdate()
    {
        if (Time.time < nextThinkTime) return;

        // Lower skill = slower reactions, more wandering, fewer attack attempts, more hesitation.
        nextThinkTime = Time.time + Mathf.Lerp(0.45f, 0.12f, skill);

        Vector2 toEnemy = enemyTransform.position - transform.position;
        float dist = toEnemy.magnitude;
        Vector2 dir = dist > 0.001f ? toEnemy / dist : Vector2.zero;
        Vector2 noise = Random.insideUnitCircle * (1f - skill);

        if (Time.time < retreatUntil)
        {
            Move = -dir + noise;
        }
        else if (dist > GameConstants.AttackRange * 0.9f)
        {
            Move = dir + noise;
        }
        else
        {
            Move = noise * 0.5f;
            if (Random.value < Mathf.Lerp(0.3f, 1f, skill)) attackQueued = true;
            if (Random.value < (1f - skill) * 0.25f) retreatUntil = Time.time + 0.6f;
        }
    }
}

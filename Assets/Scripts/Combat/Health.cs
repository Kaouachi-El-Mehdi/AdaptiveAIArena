using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = GameConstants.StartingHealth;

    public float Current { get; private set; }
    public float DamageTaken { get; private set; }
    public bool IsDead => Current <= 0f;

    void Awake()
    {
        ResetHealth();
    }

    // Called at the start of every round (training resets rounds without reloading the scene).
    public void ResetHealth()
    {
        Current = maxHealth;
        DamageTaken = 0f;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        float applied = Mathf.Min(amount, Current);
        Current -= applied;
        DamageTaken += applied;
    }
}

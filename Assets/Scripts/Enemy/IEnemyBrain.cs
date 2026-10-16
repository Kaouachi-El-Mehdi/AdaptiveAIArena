// Lets RoundManager log which brain is playing and the stat set in force,
// without knowing whether it is the FSM or the ML-Agents agent.
public interface IEnemyBrain
{
    string BrainType { get; }
    EnemyStats Stats { get; }
}

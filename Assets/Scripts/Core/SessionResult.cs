using System;

public enum RoundOutcome { Win, Loss, Timeout }

// Locked schema for the mémoire's Résultats data — step 2 (CSV logging) writes
// exactly these fields, don't add/rename columns once logging is wired up.
[Serializable]
public class SessionResult
{
    public string brainType;
    public string sessionId;
    public string timestamp;
    public RoundOutcome outcome;
    public float timeToComplete;
    public float damageDealt;
    public float damageTaken;
    public int tuningIterationNumber; // Brain A only; leave 0 for Brain B sessions
}

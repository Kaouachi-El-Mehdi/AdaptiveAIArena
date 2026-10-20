using System.Collections.Generic;
using UnityEngine;

// Runs one condition of a human session (Session_BrainA / Session_BrainB scenes):
// intro screen -> round -> ratings -> ... x RoundsPerCondition -> next condition.
// The game is paused (timeScale 0) on every screen, so nothing moves while the player reads.
// The participant only ever sees "Adversaire k sur 3": no brain name anywhere.
public class SessionRoundLoop : MonoBehaviour
{
    public RoundManager roundManager;
    public Health playerHealth;
    public Health enemyHealth;
    public EnemyBrainA brainA; // null in the Brain B scene

    enum Phase { Intro, Playing, Rating, TuningResult, Finished }
    Phase phase;

    int round;               // 1-based round in this condition
    int roundsTotal;
    SessionResult pending;   // finished round waiting for its ratings
    int difficulty, enjoyment;
    readonly List<float> tuningGaps = new List<float>();
    float blockStartRealtime;

    void Awake()
    {
        // Scene played directly in the Editor: go through the start screen instead.
        if (!ParticipantSession.Active)
        {
            ParticipantSession.BackToMenu();
            return;
        }

        roundManager.sessionMode = true;
        roundManager.OnRoundEnd += HandleRoundEnd;

        Condition c = ParticipantSession.Current;
        if (brainA != null)
        {
            bool tuned = c == Condition.ATuned || c == Condition.ATuning;
            brainA.stats = tuned ? BrainATuning.Stats() : EnemyStats.Initial();
            roundManager.tuningIterationNumber = tuned ? BrainATuning.Iteration : 0;
        }
        roundsTotal = ParticipantSession.IsTuningBlock ? ParticipantSession.RoundsPerTuningBlock : ParticipantSession.RoundsPerCondition;
        round = 1;
        Pause(Phase.Intro);

        if (ParticipantSession.AutoTest)
        {
            var bot = playerHealth.gameObject.AddComponent<PlayerProxyBot>();
            bot.enemyTransform = enemyHealth.transform;
            bot.randomizeSkillEachRound = false;
            playerHealth.GetComponent<PlayerController>().bot = bot;
        }
    }

    // Auto-test only: press the buttons a participant would press (ratings = 3).
    void Update()
    {
        if (!ParticipantSession.AutoTest) return;
        switch (phase)
        {
            case Phase.Intro: StartRound(); break;
            case Phase.Rating: difficulty = enjoyment = 3; SubmitRatings(); break;
            case Phase.Finished: ParticipantSession.BackToMenu(); break;
        }
    }

    void OnDestroy()
    {
        if (roundManager != null) roundManager.OnRoundEnd -= HandleRoundEnd;
    }

    void Pause(Phase p)
    {
        phase = p;
        Time.timeScale = 0f;
    }

    void StartRound()
    {
        if (round == 1) blockStartRealtime = Time.realtimeSinceStartup;
        roundManager.StartNextRound();
        Time.timeScale = ParticipantSession.AutoTest ? 20f : 1f;
        phase = Phase.Playing;
    }

    // Called by RoundManager on the physics tick where the round ends.
    void HandleRoundEnd(SessionResult r)
    {
        r.participantId = ParticipantSession.ParticipantId;
        r.condition = ParticipantSession.Name(ParticipantSession.Current);
        r.conditionOrder = ParticipantSession.OrderText;
        r.conditionPosition = ParticipantSession.Position + 1;
        r.roundInCondition = round;
        pending = r;
        difficulty = enjoyment = 0;

        if (ParticipantSession.IsTuningBlock)
        {
            // No ratings in tuning blocks (developer play): log right away.
            SessionLogger.Append(r);
            tuningGaps.Add(Mathf.Abs(r.damageTaken - r.damageDealt) / GameConstants.StartingHealth);
            NextRoundOrEnd();
        }
        else
        {
            Pause(Phase.Rating);
        }
    }

    void SubmitRatings()
    {
        pending.difficultyRating = difficulty;
        pending.enjoymentRating = enjoyment;
        SessionLogger.Append(pending);
        NextRoundOrEnd();
    }

    void NextRoundOrEnd()
    {
        if (round < roundsTotal)
        {
            round++;
            Pause(Phase.Intro); // player starts the next round when ready
            return;
        }
        if (ParticipantSession.IsTuningBlock) { Pause(Phase.TuningResult); return; }
        if (!ParticipantSession.Advance()) Pause(Phase.Finished);
    }

    void OnGUI()
    {
        SessionGui.Begin();
        switch (phase)
        {
            case Phase.Playing: DrawHud(); break;
            case Phase.Intro: DrawIntro(); break;
            case Phase.Rating: DrawRating(); break;
            case Phase.TuningResult: DrawTuningResult(); break;
            case Phase.Finished: DrawFinished(); break;
        }
    }

    void DrawHud()
    {
        float timeLeft = Mathf.Max(0f, GameConstants.RoundTimeoutSeconds - roundManager.Elapsed);
        GUILayout.BeginArea(new Rect(20, 10, SessionGui.Width - 40, 60));
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Vous : {Mathf.CeilToInt(playerHealth.Current)} PV", SessionGui.Text);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Manche {round}/{roundsTotal}   —   {Mathf.CeilToInt(timeLeft)} s", SessionGui.Text);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Adversaire : {Mathf.CeilToInt(enemyHealth.Current)} PV", SessionGui.Text);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    void DrawIntro()
    {
        GUILayout.BeginArea(SessionGui.Panel(640, 300));
        if (ParticipantSession.IsTuningBlock)
        {
            GUILayout.Label($"Bloc de réglage — itération {BrainATuning.Iteration}", SessionGui.Title);
            GUILayout.Label($"Manche {round}/{roundsTotal}", SessionGui.Text);
        }
        else
        {
            GUILayout.Label($"Adversaire {ParticipantSession.Position + 1} sur {ParticipantSession.Order.Length}", SessionGui.Title);
            GUILayout.Label($"{roundsTotal} manches contre cet adversaire.\n" +
                            "Déplacement : ZQSD (AZERTY) / WASD ou flèches. Attaque : Espace.\n" +
                            "Après chaque manche, deux questions rapides.", SessionGui.Text);
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(round == 1 ? "Commencer" : "Manche suivante", SessionGui.Button, GUILayout.Height(50)))
            StartRound();
        GUILayout.EndArea();
    }

    void DrawRating()
    {
        GUILayout.BeginArea(SessionGui.Panel(760, 400));
        GUILayout.Label($"Manche {round}/{roundsTotal} terminée", SessionGui.Title);
        GUILayout.Space(10);
        difficulty = RatingRow("Difficulté de cette manche :", "1 = beaucoup trop facile   3 = équilibrée   5 = beaucoup trop difficile", difficulty);
        GUILayout.Space(10);
        enjoyment = RatingRow("Plaisir de jeu :", "1 = pas du tout   5 = beaucoup", enjoyment);
        GUILayout.FlexibleSpace();
        GUI.enabled = difficulty > 0 && enjoyment > 0;
        if (GUILayout.Button("Valider", SessionGui.Button, GUILayout.Height(50)))
            SubmitRatings();
        GUI.enabled = true;
        GUILayout.EndArea();
    }

    static int RatingRow(string question, string scale, int value)
    {
        GUILayout.Label(question, SessionGui.Text);
        GUILayout.Label(scale, SessionGui.Small);
        GUILayout.BeginHorizontal();
        for (int v = 1; v <= 5; v++)
        {
            bool on = GUILayout.Toggle(value == v, v.ToString(), SessionGui.Button, GUILayout.Width(70), GUILayout.Height(45));
            if (on && value != v) value = v;
        }
        GUILayout.EndHorizontal();
        return value;
    }

    void DrawTuningResult()
    {
        int close = tuningGaps.FindAll(g => g <= 0.2f).Count;
        float minutes = (Time.realtimeSinceStartup - blockStartRealtime) / 60f;
        GUILayout.BeginArea(SessionGui.Panel(700, 420));
        GUILayout.Label($"Bloc terminé — itération {BrainATuning.Iteration}", SessionGui.Title);
        GUILayout.Label($"Manches serrées (écart ≤ 0.2) : {close}/{tuningGaps.Count}  →  " +
                        (close >= 7 ? "RÉUSSI (≥ 7)" : "ÉCHEC (< 7)"), SessionGui.Text);
        GUILayout.Label("Écarts : " + string.Join("  ", tuningGaps.ConvertAll(g => g.ToString("0.00"))), SessionGui.Small);
        GUILayout.Label($"Durée du bloc : {minutes:0.0} min. Lignes enregistrées dans sessions.csv (participant DEV).", SessionGui.Small);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Retour au menu", SessionGui.Button, GUILayout.Height(50)))
            ParticipantSession.BackToMenu();
        GUILayout.EndArea();
    }

    void DrawFinished()
    {
        GUILayout.BeginArea(SessionGui.Panel(600, 260));
        GUILayout.Label("Session terminée. Merci !", SessionGui.Title);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Retour au menu", SessionGui.Button, GUILayout.Height(50)))
            ParticipantSession.BackToMenu();
        GUILayout.EndArea();
    }
}

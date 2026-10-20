using UnityEngine;

// Start screen (Session_Start scene), filled in by the experimenter. It never names the brains.
public class SessionMenu : MonoBehaviour
{
    string numberText = "";
    string message = "";
    int pendingNumber = -1; // set when the id already has rows: second click confirms
    static bool autoTestStarted; // static: survives the scene loads of the session

    void Start()
    {
        Time.timeScale = 1f;
        if (ParticipantSession.AutoTest)
        {
            if (autoTestStarted) { Application.Quit(); return; } // back here after the session = done
            autoTestStarted = true;
            ParticipantSession.BeginParticipant(0);
        }
    }

    void OnGUI()
    {
        SessionGui.Begin();
        GUILayout.BeginArea(SessionGui.Centre(620, 420));
        GUILayout.Label("Arène — session de test", SessionGui.Title);
        GUILayout.Space(20);
        GUILayout.Label("Numéro de participant (0 = essai, non analysé) :", SessionGui.Text);
        numberText = GUILayout.TextField(numberText, 3, SessionGui.Field, GUILayout.Width(120));
        GUILayout.Space(10);

        if (GUILayout.Button("Commencer la session", SessionGui.Button, GUILayout.Height(50)))
            TryStart();
        GUILayout.Label(message, SessionGui.Text);

        GUILayout.FlexibleSpace();
        GUILayout.Label($"Réservé au développeur — itération de réglage {BrainATuning.Iteration}", SessionGui.Small);
        if (GUILayout.Button($"Bloc de réglage Brain A ({ParticipantSession.RoundsPerTuningBlock} manches)", SessionGui.Button))
            ParticipantSession.BeginTuningBlock();
        GUILayout.EndArea();
    }

    void TryStart()
    {
        if (!int.TryParse(numberText, out int n) || n < 0 || n > 99)
        {
            message = "Entrez un nombre entre 0 et 99.";
            return;
        }
        int existing = SessionLogger.CountRows($"P{n:00}");
        if (existing > 0 && pendingNumber != n)
        {
            pendingNumber = n;
            message = $"P{n:00} a déjà {existing} manches enregistrées. Cliquez encore pour continuer quand même.";
            return;
        }
        ParticipantSession.BeginParticipant(n);
    }
}

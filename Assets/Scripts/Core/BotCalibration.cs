using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Calibrates PlayerProxyBot (Brain B's training opponent) against Brain A at its initial stats.
// Plays a fixed number of rounds at each skill level and writes one row per round to
// bot_calibration.csv (never to sessions.csv: these are not player sessions), then quits.
// Question answered: does the bot's skill range cover opponents both weaker and stronger
// than Brain A-initial, like human players would?
public class BotCalibration : MonoBehaviour
{
    public RoundManager roundManager;
    public PlayerProxyBot bot;

    public float[] skillLevels = { 0.2f, 0.4f, 0.6f, 0.8f, 1f };
    public int roundsPerLevel = 100;
    [Tooltip("Same speed-up as ML-Agents training (time_scale 20).")]
    public float timeScale = 20f;

    const string FileName = "bot_calibration.csv";
    const string Header = "run_id,timestamp,bot_skill,brain_type,outcome,time_to_complete,damage_dealt,damage_taken," +
                          "enemy_move_speed,enemy_attack_cooldown,enemy_attack_damage,enemy_detection_range";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    string runId;
    int levelIndex;
    int roundsDone;

    void Awake()
    {
        roundManager.calibrationRun = true;
        bot.randomizeSkillEachRound = false;
        bot.skill = skillLevels[0];
        Time.timeScale = timeScale;
        Application.targetFrameRate = -1;
        runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        roundManager.OnRoundEnd += HandleRoundEnd;

        // -brainATuned: evaluate Brain A at its final tuned stats (BrainATuning.cs) instead of the initial ones.
        var brainA = FindAnyObjectByType<EnemyBrainA>();
        if (brainA != null && Array.IndexOf(Environment.GetCommandLineArgs(), "-brainATuned") >= 0)
        {
            brainA.stats = BrainATuning.Stats();
            roundManager.tuningIterationNumber = BrainATuning.Iteration;
            Debug.Log($"[BotCalibration] Brain A tuned stats, iteration {BrainATuning.Iteration}");
        }
        Debug.Log($"[BotCalibration] Run {runId}: {skillLevels.Length} levels x {roundsPerLevel} rounds -> {FilePath}");
    }

    // Called before RoundManager restarts the round, so the new skill applies to the next round.
    void HandleRoundEnd(SessionResult r)
    {
        Append(r, bot.skill);
        roundsDone++;
        if (roundsDone < roundsPerLevel) return;

        roundsDone = 0;
        levelIndex++;
        if (levelIndex >= skillLevels.Length)
        {
            Debug.Log($"[BotCalibration] Done. Results in {FilePath}");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            return;
        }
        bot.skill = skillLevels[levelIndex];
        Debug.Log($"[BotCalibration] Skill level {bot.skill}");
    }

    void Append(SessionResult r, float skill)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        string row = string.Join(",",
            runId,
            r.timestamp,
            skill.ToString(inv),
            r.brainType,
            r.outcome.ToString(),
            r.timeToComplete.ToString("F2", inv),
            r.damageDealt.ToString(inv),
            r.damageTaken.ToString(inv),
            r.enemyMoveSpeed.ToString(inv),
            r.enemyAttackCooldown.ToString(inv),
            r.enemyAttackDamage.ToString(inv),
            r.enemyDetectionRange.ToString(inv));

        bool isNew = !File.Exists(FilePath);
        using (var writer = new StreamWriter(FilePath, append: true))
        {
            if (isNew) writer.WriteLine(Header);
            writer.WriteLine(row);
        }
    }
}

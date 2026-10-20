using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Appends one row per finished round to sessions.csv. Column order matches the
// SessionResult schema: don't reorder once data has been collected.
public static class SessionLogger
{
    const string FileName = "sessions.csv";
    const string Header = "brain_type,session_id,timestamp,outcome,time_to_complete,damage_dealt,damage_taken,tuning_iteration_number," +
                          "enemy_move_speed,enemy_attack_cooldown,enemy_attack_damage,enemy_detection_range," +
                          "participant_id,condition,condition_order,condition_position,round_in_condition," +
                          "difficulty_rating,enjoyment_rating";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Append(SessionResult r)
    {
        // InvariantCulture so floats are written "12.5", not "12,5" on a French-locale machine
        // (a comma decimal would break the CSV columns).
        CultureInfo inv = CultureInfo.InvariantCulture;
        string row = string.Join(",",
            r.brainType,
            r.sessionId,
            r.timestamp,
            r.outcome.ToString(),
            r.timeToComplete.ToString("F2", inv),
            r.damageDealt.ToString(inv),
            r.damageTaken.ToString(inv),
            r.tuningIterationNumber.ToString(inv),
            r.enemyMoveSpeed.ToString(inv),
            r.enemyAttackCooldown.ToString(inv),
            r.enemyAttackDamage.ToString(inv),
            r.enemyDetectionRange.ToString(inv),
            r.participantId ?? "",
            r.condition ?? "",
            r.conditionOrder ?? "",
            r.conditionPosition.ToString(inv),
            r.roundInCondition.ToString(inv),
            Rating(r.difficultyRating),
            Rating(r.enjoymentRating));

        ArchiveIfOldSchema();

        bool isNew = !File.Exists(FilePath);
        using (var writer = new StreamWriter(FilePath, append: true))
        {
            if (isNew) writer.WriteLine(Header);
            writer.WriteLine(row);
        }

        Debug.Log($"[SessionLogger] Row written to {FilePath}");
    }

    // 0 = not asked (tuning block): empty cell, so it can't be mistaken for a rating.
    static string Rating(int v) => v == 0 ? "" : v.ToString(CultureInfo.InvariantCulture);

    // Number of rows already logged for a participant id (warns the experimenter about a reused id).
    public static int CountRows(string participantId)
    {
        if (!File.Exists(FilePath)) return 0;
        string[] lines = File.ReadAllLines(FilePath);
        if (lines.Length == 0 || lines[0] != Header) return 0;
        int col = Array.IndexOf(lines[0].Split(','), "participant_id");
        int n = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cells = lines[i].Split(',');
            if (cells.Length > col && cells[col] == participantId) n++;
        }
        return n;
    }

    // A file written with an older header would get rows with a different column count.
    // Move it aside (never delete data) and start a fresh file with the current header.
    static void ArchiveIfOldSchema()
    {
        if (!File.Exists(FilePath)) return;

        string firstLine;
        using (var reader = new StreamReader(FilePath))
            firstLine = reader.ReadLine();
        if (firstLine == Header) return;

        string archive = Path.Combine(Application.persistentDataPath,
            $"sessions_oldschema_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.Move(FilePath, archive);
        Debug.LogWarning($"[SessionLogger] sessions.csv had an older header; moved to {archive}");
    }
}

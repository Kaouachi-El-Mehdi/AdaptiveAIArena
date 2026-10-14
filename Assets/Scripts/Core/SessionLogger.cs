using System.Globalization;
using System.IO;
using UnityEngine;

// Appends one row per finished round to sessions.csv. Column order matches the
// locked SessionResult schema — don't reorder once data has been collected.
public static class SessionLogger
{
    const string FileName = "sessions.csv";
    const string Header = "brain_type,session_id,timestamp,outcome,time_to_complete,damage_dealt,damage_taken,tuning_iteration_number";

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
            r.tuningIterationNumber.ToString(inv));

        bool isNew = !File.Exists(FilePath);
        using (var writer = new StreamWriter(FilePath, append: true))
        {
            if (isNew) writer.WriteLine(Header);
            writer.WriteLine(row);
        }

        Debug.Log($"[SessionLogger] Row written to {FilePath}");
    }
}

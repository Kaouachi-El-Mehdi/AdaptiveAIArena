using UnityEngine;

// Shared IMGUI styles for the session screens. IMGUI = no Canvas/UI setup needed in the scenes.
// Everything is laid out for a 1280x720 screen and scaled to the real resolution.
public static class SessionGui
{
    const float RefHeight = 720f;

    public static GUIStyle Title, Text, Small, Button, Field;

    public static void Begin()
    {
        float s = Screen.height / RefHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        if (Title != null) return;

        Title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, wordWrap = true };
        Text = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
        Small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        Button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        Field = new GUIStyle(GUI.skin.textField) { fontSize = 26 };
    }

    public static float Width => Screen.width * RefHeight / Screen.height;

    public static Rect Centre(float w, float h) => new Rect((Width - w) / 2f, (RefHeight - h) / 2f, w, h);

    // Dark panel behind a centred screen so text stays readable over the arena
    // (the default box is semi-transparent, drawn twice to make it darker).
    public static Rect Panel(float w, float h)
    {
        Rect r = Centre(w, h);
        var back = new Rect(r.x - 20, r.y - 20, r.width + 40, r.height + 40);
        GUI.Box(back, GUIContent.none);
        GUI.Box(back, GUIContent.none);
        return r;
    }
}

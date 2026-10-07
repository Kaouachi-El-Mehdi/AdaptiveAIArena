#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the v1 arena programmatically so the scene setup is a one-click,
// reproducible step instead of hand-wiring GameObjects in the Inspector.
public static class ArenaSetup
{
    [MenuItem("AdaptiveAIArena/Build Arena Scene")]
    public static void BuildArena()
    {
        CreateWall("Wall_Top", new Vector2(0, GameConstants.ArenaHeight / 2f), new Vector2(GameConstants.ArenaWidth, 0.5f));
        CreateWall("Wall_Bottom", new Vector2(0, -GameConstants.ArenaHeight / 2f), new Vector2(GameConstants.ArenaWidth, 0.5f));
        CreateWall("Wall_Left", new Vector2(-GameConstants.ArenaWidth / 2f, 0), new Vector2(0.5f, GameConstants.ArenaHeight));
        CreateWall("Wall_Right", new Vector2(GameConstants.ArenaWidth / 2f, 0), new Vector2(0.5f, GameConstants.ArenaHeight));

        GameObject player = CreateCombatant("Player", new Vector2(-4, 0), new Color(0.2f, 0.5f, 1f));
        var playerHealth = player.AddComponent<Health>();
        var playerController = player.AddComponent<PlayerController>();

        GameObject enemy = CreateCombatant("Enemy", new Vector2(4, 0), new Color(1f, 0.3f, 0.3f));
        var enemyHealth = enemy.AddComponent<Health>();
        var enemyBrainA = enemy.AddComponent<EnemyBrainA>();

        playerController.health = playerHealth;
        playerController.enemyTransform = enemy.transform;
        playerController.enemyHealth = enemyHealth;

        enemyBrainA.health = enemyHealth;
        enemyBrainA.playerTransform = player.transform;
        enemyBrainA.playerHealth = playerHealth;

        GameObject roundManagerGO = new GameObject("RoundManager");
        var roundManager = roundManagerGO.AddComponent<RoundManager>();
        roundManager.playerHealth = playerHealth;
        roundManager.enemyHealth = enemyHealth;
        roundManager.brainType = "A";

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Arena built: 4 walls, Player, Enemy (Brain A), RoundManager. Save the scene (Ctrl+S) to keep it.");
    }

    static GameObject CreateWall(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = Color.gray;

        return go;
    }

    static GameObject CreateCombatant(string name, Vector2 position, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = position;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = color;

        return go;
    }

    static Sprite CreateSquareSprite()
    {
        var tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
    }

    static Sprite CreateCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, dist <= size / 2f ? Color.white : new Color(0, 0, 0, 0));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
#endif

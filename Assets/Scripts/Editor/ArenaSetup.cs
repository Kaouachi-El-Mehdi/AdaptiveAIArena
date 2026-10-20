#if UNITY_EDITOR
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the arena scenes programmatically so scene setup is a one-click,
// reproducible step instead of hand-wiring GameObjects in the Inspector.
// Each menu item creates and saves its own scene in Assets/Scenes/.
public static class ArenaSetup
{
    enum Brain { A, B }

    public const string TrainingScenePath = "Assets/Scenes/Training_BrainB.unity";
    public const string CalibrationScenePath = "Assets/Scenes/Calibration_BotVsBrainA.unity";
    public const string EvaluationBScenePath = "Assets/Scenes/Calibration_BotVsBrainB.unity";
    // Trained Brain B used outside training (real sessions + evaluation). Update after each new run.
    public const string BrainBModelPath = "Assets/Models/BrainB_v1_run1.onnx";

    [MenuItem("AdaptiveAIArena/Build Arena Scene (Brain A)")]
    public static void BuildArenaA() => BuildArena("Assets/Scenes/Arena_BrainA.unity", Brain.A, botPlayer: false);

    [MenuItem("AdaptiveAIArena/Build Arena Scene (Brain B)")]
    public static void BuildArenaB() => BuildArena("Assets/Scenes/Arena_BrainB.unity", Brain.B, botPlayer: false);

    [MenuItem("AdaptiveAIArena/Build Training Scene (Brain B vs bot)")]
    public static void BuildTrainingScene() => BuildArena(TrainingScenePath, Brain.B, botPlayer: true);

    [MenuItem("AdaptiveAIArena/Build Calibration Scene (bot vs Brain A initial)")]
    public static void BuildCalibrationScene() => BuildArena(CalibrationScenePath, Brain.A, botPlayer: true, calibration: true);

    // Standalone training player for mlagents-learn --env (faster than training in the Editor).
    // Batch mode: Unity.exe -batchmode -projectPath . -executeMethod ArenaSetup.BuildTrainingPlayer -quit
    public static void BuildTrainingPlayer() => BuildPlayer(BuildTrainingScene, TrainingScenePath, "Builds/Training/Arena.exe");

    [MenuItem("AdaptiveAIArena/Build Evaluation Scene (bot vs trained Brain B)")]
    public static void BuildEvaluationBScene() => BuildArena(EvaluationBScenePath, Brain.B, botPlayer: true, calibration: true);

    // Batch mode, to evaluate another trained run: add -brainBModel Assets/Models/<file>.onnx
    // (default: BrainBModelPath). Output: Builds/EvaluationB_<model name>/Arena.exe
    public static void BuildEvaluationBPlayer() => BuildPlayer(BuildEvaluationBScene, EvaluationBScenePath,
        $"Builds/EvaluationB_{Path.GetFileNameWithoutExtension(SelectedModelPath)}/Arena.exe");

    static string SelectedModelPath
    {
        get
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-brainBModel");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : BrainBModelPath;
        }
    }

    // Standalone calibration player: plays all skill levels, writes bot_calibration.csv, quits.
    // Run it with: Builds/Calibration/Arena.exe -batchmode -nographics
    public static void BuildCalibrationPlayer() => BuildPlayer(BuildCalibrationScene, CalibrationScenePath, "Builds/Calibration/Arena.exe");

    // Human sessions: start screen (participant number) + one scene per brain type.
    // Brain A's stats per condition are applied at runtime by SessionRoundLoop (BrainATuning.cs),
    // so a tuning iteration doesn't need a scene rebuild.
    static readonly string[] SessionScenePaths =
    {
        $"Assets/Scenes/{ParticipantSession.StartScene}.unity",
        $"Assets/Scenes/{ParticipantSession.BrainAScene}.unity",
        $"Assets/Scenes/{ParticipantSession.BrainBScene}.unity",
    };

    [MenuItem("AdaptiveAIArena/Build Session Scenes (human sessions)")]
    public static void BuildSessionScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var start = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        new GameObject("SessionMenu").AddComponent<SessionMenu>();
        EditorSceneManager.SaveScene(start, SessionScenePaths[0]);

        BuildArena(SessionScenePaths[1], Brain.A, botPlayer: false, session: true);
        BuildArena(SessionScenePaths[2], Brain.B, botPlayer: false, session: true);

        // SceneManager.LoadScene only finds scenes listed in Build Settings (also in Editor Play mode).
        EditorBuildSettings.scenes = System.Array.ConvertAll(SessionScenePaths, p => new EditorBuildSettingsScene(p, true));
        EditorSceneManager.OpenScene(SessionScenePaths[0]);
        Debug.Log("[ArenaSetup] Session scenes built. Press Play in Session_Start.");
    }

    // Output: Builds/Session/Arena.exe
    public static void BuildSessionPlayer() => BuildPlayer(BuildSessionScenes, SessionScenePaths, "Builds/Session/Arena.exe");

    static void BuildPlayer(System.Action buildScene, string scenePath, string exePath) =>
        BuildPlayer(buildScene, new[] { scenePath }, exePath);

    static void BuildPlayer(System.Action buildScene, string[] scenePaths, string exePath)
    {
        buildScene();
        var report = BuildPipeline.BuildPlayer(scenePaths, exePath,
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[ArenaSetup] Player build {exePath}: {report.summary.result}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static void BuildArena(string scenePath, Brain brain, bool botPlayer, bool calibration = false, bool session = false)
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = GameConstants.ArenaHeight / 2f + 1f;
        cam.transform.position = new Vector3(0, 0, -10);

        CreateWall("Wall_Top", new Vector2(0, GameConstants.ArenaHeight / 2f), new Vector2(GameConstants.ArenaWidth, 0.5f));
        CreateWall("Wall_Bottom", new Vector2(0, -GameConstants.ArenaHeight / 2f), new Vector2(GameConstants.ArenaWidth, 0.5f));
        CreateWall("Wall_Left", new Vector2(-GameConstants.ArenaWidth / 2f, 0), new Vector2(0.5f, GameConstants.ArenaHeight));
        CreateWall("Wall_Right", new Vector2(GameConstants.ArenaWidth / 2f, 0), new Vector2(0.5f, GameConstants.ArenaHeight));

        GameObject player = CreateCombatant("Player", new Vector2(-4, 0), new Color(0.2f, 0.5f, 1f));
        var playerHealth = player.AddComponent<Health>();
        var playerController = player.AddComponent<PlayerController>();

        GameObject enemy = CreateCombatant("Enemy", new Vector2(4, 0), new Color(1f, 0.3f, 0.3f));
        var enemyHealth = enemy.AddComponent<Health>();

        playerController.health = playerHealth;
        playerController.enemyTransform = enemy.transform;
        playerController.enemyHealth = enemyHealth;

        if (botPlayer)
        {
            var bot = player.AddComponent<PlayerProxyBot>();
            bot.enemyTransform = enemy.transform;
            playerController.bot = bot;
        }

        GameObject roundManagerGO = new GameObject("RoundManager");
        var roundManager = roundManagerGO.AddComponent<RoundManager>();
        roundManager.playerHealth = playerHealth;
        roundManager.enemyHealth = enemyHealth;

        EnemyBrainA brainA = null;
        if (brain == Brain.A)
        {
            brainA = enemy.AddComponent<EnemyBrainA>();
            brainA.health = enemyHealth;
            brainA.playerTransform = player.transform;
            brainA.playerHealth = playerHealth;
        }
        else
        {
            // BehaviorParameters must be configured before the Agent component is added.
            var bp = enemy.AddComponent<BehaviorParameters>();
            bp.BehaviorName = EnemyBrainB.BehaviorName;
            bp.BrainParameters.VectorObservationSize = EnemyBrainB.ObservationSize;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(EnemyBrainB.ActionBranches);
            // Outside the training scene, B plays with the trained model (no mlagents-learn connected).
            // Training scene: no model, so mlagents-learn drives it.
            if (scenePath != TrainingScenePath)
            {
                var model = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(SelectedModelPath);
                if (model == null) Debug.LogWarning($"[ArenaSetup] No trained model at {SelectedModelPath}: Brain B will use its heuristic.");
                bp.Model = model;
            }

            var brainB = enemy.AddComponent<EnemyBrainB>();
            brainB.MaxStep = 0; // rounds end through RoundManager (death or 60 s timeout)
            brainB.health = enemyHealth;
            brainB.playerTransform = player.transform;
            brainB.playerHealth = playerHealth;
            brainB.roundManager = roundManager;

            var requester = enemy.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5; // 10 decisions/s at the default 50 Hz physics step
            requester.TakeActionsBetweenDecisions = true;
        }

        if (session)
        {
            var loop = roundManagerGO.AddComponent<SessionRoundLoop>();
            loop.roundManager = roundManager;
            loop.playerHealth = playerHealth;
            loop.enemyHealth = enemyHealth;
            loop.brainA = brainA;
        }

        if (calibration)
        {
            var cal = roundManagerGO.AddComponent<BotCalibration>();
            cal.roundManager = roundManager;
            cal.bot = player.GetComponent<PlayerProxyBot>();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[ArenaSetup] Built and saved {scenePath} (Brain {brain}, player = {(botPlayer ? "bot" : "keyboard")}).");
    }

    static GameObject CreateWall(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        go.AddComponent<BoxCollider2D>();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadOrCreateSprite("Square", MakeSquareTexture);
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
        sr.sprite = LoadOrCreateSprite("Circle", MakeCircleTexture);
        sr.color = color;

        return go;
    }

    // Sprites are saved as assets so they survive saving/reloading the scene
    // (a texture created only in memory would be lost on reload).
    static Sprite LoadOrCreateSprite(string name, System.Func<Texture2D> makeTexture)
    {
        string path = $"Assets/Generated/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        Directory.CreateDirectory("Assets/Generated");
        var tex = makeTexture();
        tex.name = name + "Texture";
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        sprite.name = name;

        AssetDatabase.CreateAsset(sprite, path);
        AssetDatabase.AddObjectToAsset(tex, sprite);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Texture2D MakeSquareTexture()
    {
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeCircleTexture()
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
        return tex;
    }
}
#endif

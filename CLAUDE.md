# Adaptive AI Arena — project instructions

## Why this project exists
Supporting evidence for the user's IPSSI Mémoire de Recherche, *« L'intelligence artificielle
dans l'industrie du jeu vidéo »*. The app itself is **not graded**. Its only job is to produce
**real, defensible data** for the mémoire's *Résultats* section.

- Do **not** polish UI or game feel. Spend effort only where it affects data quality.
- Every methodological choice must be explainable in the mémoire. If a shortcut would weaken
  the data, say so.
- The user is a beginner in Unity and not a native English speaker: explain decisions simply.
  The lab log and mémoire are in French; code and comments are in English.

## What is compared
Two enemy AIs on the **same** top-down 2D arena, against the same player:

- **Brain A**: rule-based FSM baseline (`EnemyBrainA.cs`). States: Idle / Chase / Attack.
  No Retreat state in v1 (kept for the mémoire's *Perspectives* section).
- **Brain B**: Unity ML-Agents PPO agent (`EnemyBrainB.cs`). Reward shaped for **Dynamic
  Difficulty Adjustment**: keep the match close, do **not** maximise wins.

**Three analysis conditions:** A-initial (tuning iteration 0), A-tuned (final iteration),
B (at A-initial stats). Comparison axes: combat outcome data (CSV) + development cost
(Brain A tuning iterations vs Brain B training/reward-design effort).

## Arena and symmetry (decided 2026-10-15; mémoire wording must match)
- About 16x10 units, top-down 2D, no internal obstacles in v1. 100 HP each.
  Round timeout 60 s (`GameConstants.RoundTimeoutSeconds`).
- **"Symmetric abilities" = same action set (move, attack) + same attack range.**
  It does **not** mean identical numbers.
- `attackRange` = **1.2 for player, Brain A and Brain B** (`GameConstants.AttackRange`).
  Fixed, **not** a tunable parameter.
- Player stats are fixed constants (`GameConstants.Player*`): speed 4, damage 10, cooldown 0.6.
  Identical in training and in real sessions.
- Brain A initial stats (`EnemyStats.Initial()`): speed 3, damage 8, cooldown 0.8, detection 6.
  The gap with the player is **intentional**: it is what Brain A's manual tuning has to correct.
- Brain B uses **Brain A's initial stats**, which can't be edited in the Inspector, and the same
  attack rule. Its perception is limited the same way: it only sees the player within detectionRange.
  Only its decisions differ. Known information difference to state in the mémoire: B observes
  both HP values (needed for DDA); A doesn't use them.

## Brain A tuning
Exactly **4 tunable parameters**: `moveSpeed`, `attackCooldown`, `attackDamage`, `detectionRange`
(`EnemyBrainA.stats` in the Inspector). Each manual tuning pass = **one tuning iteration**:
bump `RoundManager.tuningIterationNumber` and record the pass in `LabLog.md`
(old → new values, reason).

## Brain B
- Observations (9): own position, player-visible flag, relative player position (masked beyond
  detectionRange), own HP, player HP, attack-ready fraction, time remaining.
- Actions: discrete, branch 0 = 9 moves (stay + 8 directions), branch 1 = attack yes/no.
  Decision every 5 physics steps (10/s).
- Reward v1: terminal `engagement × (1 − final HP gap)` (in [0, 1]; avoiding the fight scores 0)
  + dense potential shaping `Δ(−0.5 × HP gap)`. Every reward change = one reward-design
  iteration (counts toward B's dev cost) and needs a lab log entry.
- **Training opponent: `PlayerProxyBot`**, a scripted bot driving the same `PlayerController`
  (same player stats). Its skill is re-rolled uniformly in [0.2, 1] every round.
- Training stats in TensorBoard under `DDA/*` (FinalHpGap, Engagement, Win/Loss/Timeout rates).
- **Reward v1 is final** (decided 2026-10-16). Reward v2 (e.g. penalising the narrow-loss lean) is a
  mémoire *Perspective*, not work to do.
- **Known limits of B v1 (state them in the mémoire):**
  - *Narrow-loss lean:* the v1 reward scores a narrow win and a narrow loss the same; B learned to
    lose by a small margin more and more often as the opponent gets stronger (57% → 88% losses vs
    bot skill 0.2 → 0.6, final HP gap ≈ 0.09 throughout).
  - *Can only make the game easier, not harder:* at Brain A's initial stats (slower, weaker than the
    player), B cannot raise the difficulty: vs strong bots (skill 0.8–1.0) it loses heavily like A.
    Direct consequence of the "B at A-initial stats" design choice.

### Training runs and model selection (rule fixed 2026-10-16, BEFORE seeing runs 2–3)
- Reward v1 is trained **3 times** (independent runs, same config, different seeds):
  `brainB_v1` (seed −1 = random), `brainB_v1_s2` (`--seed 2`), `brainB_v1_s3` (`--seed 3`) →
  `Assets/Models/BrainB_v1_run1.onnx`, `_run2.onnx`, `_run3.onnx` (final model of each run).
- Each is evaluated with the same protocol: bot at skill 0.2/0.4/0.6/0.8/1.0, 100 rounds per level,
  20x time scale, fixed spawns (`Calibration_BotVsBrainB`).
- **Close-rate** = share of rounds with final HP gap ≤ 0.2; **average close-rate** = unweighted mean
  of the 5 per-level close-rates.
- **Model for human sessions = the run with the median average close-rate** (the middle one of the 3,
  not the best one: no cherry-picking). Tie on close-rate → median on mean final HP gap.
  All 3 runs are reported in the mémoire (spread across seeds = training variability).

## Logging schema (append-only: never rename/reorder)
`brain_type, session_id, timestamp, outcome, time_to_complete, damage_dealt, damage_taken, tuning_iteration_number, enemy_move_speed, enemy_attack_cooldown, enemy_attack_damage, enemy_detection_range`

- One row per round, appended to `Application.persistentDataPath/sessions.csv` (`SessionLogger.cs`).
- The 4 `enemy_*` columns (added 2026-10-15) record the stat set in force for each session.
- All fields are from the **enemy brain's perspective**: enemy kills player = `Win`.
- Floats written with `InvariantCulture` (French locale would otherwise break the CSV).
- `tuning_iteration_number` is meaningful for Brain A only; forced to 0 for Brain B.
- Brain type is read from the enemy component, not typed by hand.
- **Training rounds are never logged to sessions.csv** (RoundManager detects mlagents-learn).
- A `sessions.csv` with an older header is renamed `sessions_oldschema_<date>.csv`, never deleted.

## Build order
1. Arena + Brain A playable: **done** (2026-10-07, playtest validated 2026-10-14)
2. Logging (CSV): **done** (2026-10-14; stat columns added 2026-10-15)
3. Brain B: **code done + smoke-tested** (2026-10-15); needs an in-Editor check by the user
4. Training (PPO): **done** (2026-10-16). Bot calibrated (skill range [0.2, 1] kept). Run `brainB_v1`
   (reward v1, 2 M steps) → `Assets/Models/BrainB_v1_run1.onnx`; 2 more seeds for the median rule below. Per-skill evaluation of A-initial and B in
   `data/bot_calibration_20261016.csv`: B keeps matches close vs weak/medium bots, can't vs strong
   ones (stat ceiling), and leans toward narrow losses. Reward v1 kept.
5. Repeated sessions (data collection, 3 conditions)
6. Python analysis script + figures for the mémoire

## Scenes and tools
- Menu `AdaptiveAIArena >` *Build Arena Scene (Brain A)* / *(Brain B)* / *Build Training Scene
  (Brain B vs bot)*: each creates and saves its own scene in `Assets/Scenes/`
  (`Assets/Scripts/Editor/ArenaSetup.cs`). The old `SampleScene` is kept as-is.
- *Build Calibration Scene (bot vs Brain A initial)* + `ArenaSetup.BuildCalibrationPlayer` →
  `Builds/Calibration/Arena.exe -batchmode -nographics`: 5 skill levels × 100 rounds, writes
  `bot_calibration.csv` (persistentDataPath), never sessions.csv. Same for trained B:
  *Build Evaluation Scene (bot vs trained Brain B)* / `ArenaSetup.BuildEvaluationBPlayer`.
- Non-training Brain B scenes load `ArenaSetup.BrainBModelPath` (rebuild the scene after a new model).
- **All gameplay logic runs in `FixedUpdate`**, never `Update` (training runs at 20× time scale,
  where Update fires only ~3×/game-second). Only raw keyboard reading stays in `Update`.
- Environment: Unity 6000.6.4f1, URP 17.6, new Input System only (`Keyboard.current`).
- ML-Agents: Unity package `com.unity.ml-agents` 4.1.0. Python side: `ml-venv/` (Python 3.10 via
  `uv`, gitignored), packages pinned in `ml-requirements.txt` (`mlagents==1.1.0`, `torch==2.2.2`,
  `setuptools<81`). Recreate it with:
  `python -m uv venv --python 3.10 ml-venv` then `python -m uv pip install --python ml-venv/Scripts/python.exe -r ml-requirements.txt`
- Training (standalone build, faster than the Editor):
  1. Build: `Unity.exe -batchmode -projectPath . -executeMethod ArenaSetup.BuildTrainingPlayer` → `Builds/Training/Arena.exe`
     (the Editor must be closed on this project, or use the menu + File > Build).
  2. Train: `ml-venv/Scripts/mlagents-learn config/brainB_ppo.yaml --env=Builds/Training/Arena.exe --run-id=<id> --no-graphics`
  - Smoke-test speed ≈ 350 steps/s → 2 M steps ≈ 1 h 40.
  - Results go to `results/` (gitignored): copy final `.onnx` models into the repo.

## Working rules
- Keep a **timestamped** lab log of every run in `LabLog.md` (date + time, what was run,
  parameters, result). This feeds the mémoire's methodology section.
- **Flag problems early** and propose a simpler fallback instead of stalling.
- **Never change the Unity version without asking the user.**
- Long scratchpad paths exceed Windows' 260-char limit during Unity imports; build test copies
  in a short path if those errors matter.

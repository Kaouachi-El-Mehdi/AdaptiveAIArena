# MEMOIRE_FACTS — exact values for the methodology section

Facts only, each with its source. Compiled 2026-10-20 from the code, configs, run logs, data files,
`CLAUDE.md` and `LabLog.md`. Lines marked *(derived)* are plain arithmetic on the values above them.
Distances are in Unity world units (u), times in seconds of game time (s).

---

## 1. Arena and combat rules

Source: `Assets/Scripts/Core/GameConstants.cs`, `Assets/Scripts/Editor/ArenaSetup.cs`,
`Assets/Scripts/Combat/Health.cs`, `ProjectSettings/TimeManager.asset`.

| Item | Value |
|---|---|
| View | Top-down 2D, no internal obstacles (v1) |
| Arena size (`ArenaWidth` × `ArenaHeight`) | 16 × 10 u |
| Walls | 4 box colliders, 0.5 u thick, centred at x = ±8 and y = ±5 |
| Free interior between wall faces *(derived)* | 15.5 × 9.5 u |
| Combatant body | Circle collider, radius 0.5 u; Rigidbody2D, gravity 0, rotation frozen |
| Spawn positions | Player (−4, 0), enemy (+4, 0). Spawn distance 8 u |
| Spawn randomisation | Training only: each spawn + random offset inside a circle of radius 1.5 u. Calibration, evaluation and human sessions: fixed spawns |
| Starting HP (`StartingHealth`) | 100 for each combatant |
| Attack range (`AttackRange`) | 1.2 u, centre-to-centre distance; same for player, Brain A, Brain B; not tunable |
| Round end | Player HP ≤ 0 → enemy `Win`; enemy HP ≤ 0 → enemy `Loss`; elapsed ≥ 60 s → `Timeout` |
| Round timeout (`RoundTimeoutSeconds`) | 60 s |
| Physics step (Fixed Timestep) | 0.02 s (50 Hz). All gameplay logic runs in `FixedUpdate` |
| Damage applied | `min(damage, current HP)` (HP never goes below 0; overkill not counted) |

### Combatant stats

| Stat | Player (fixed, `GameConstants.Player*`) | Brain A initial = Brain B (`EnemyStats.Initial()`) |
|---|---|---|
| Move speed | 4 u/s | 3 u/s |
| Attack damage | 10 | 8 |
| Attack cooldown | 0.6 s | 0.8 s |
| Detection range | — (player sees whole arena) | 6 u |
| Attack range | 1.2 u | 1.2 u |
| Hits to kill opponent from 100 HP *(derived)* | 10 | 13 |
| Max damage per second *(derived)* | 16.7 | 10.0 |

Player stats are identical in training, calibration and human sessions (the bot drives the same
`PlayerController`). Human controls: WASD / arrow keys to move (input clamped to magnitude 1),
Space to attack (out-of-range attacks do nothing and do not start the cooldown).

---

## 2. Brain A (rule-based FSM)

Source: `Assets/Scripts/Enemy/EnemyBrainA.cs`, `EnemyStats.cs`.

**States:** `Idle`, `Chase`, `Attack` (no `Retreat` state in v1). Evaluated every physics step
(50 Hz), `dist` = distance enemy → player:

| Condition (checked in this order) | State | Behaviour |
|---|---|---|
| `dist > detectionRange` | Idle | Velocity 0 |
| `dist > AttackRange` (1.2) | Chase | Move straight toward the player at `moveSpeed` |
| otherwise (`dist ≤ 1.2`) | Attack | Velocity 0; attack if `cooldown` has elapsed since last hit |

- The FSM has no memory and no randomness: the state depends only on the current distance.
- Brain A does not use either HP value.
- If either combatant is dead: velocity 0, no action.
- At spawn, `dist` = 8 > 6, so Brain A starts in Idle until the player comes within 6 u.

**The 4 tunable parameters** (`EnemyBrainA.stats`, Inspector), initial values = tuning iteration 0:

| Parameter | Initial value |
|---|---|
| `moveSpeed` | 3 |
| `attackCooldown` | 0.8 |
| `attackDamage` | 8 |
| `detectionRange` | 6 |

Each manual tuning pass = one tuning iteration (`RoundManager.tuningIterationNumber`, logged in the CSV).
Tuning iterations done so far: **0**.

---

## 3. Brain B (ML-Agents PPO agent)

Source: `Assets/Scripts/Enemy/EnemyBrainB.cs`, `ArenaSetup.cs`.

**Body:** Brain A's initial stats (not editable), same attack rule (attack only if cooldown elapsed
**and** `dist ≤ 1.2`; out-of-range attempts do nothing).

### Observation vector (size 9)

| # | Observation | Formula in code | Range / unit |
|---|---|---|---|
| 1 | Own x position | `x / 8` | [−1, 1], normalised by half arena width |
| 2 | Own y position | `y / 5` | [−1, 1], normalised by half arena height |
| 3 | Player visible flag | `1` if `dist ≤ 6` else `0` | {0, 1} |
| 4 | Relative player x | `(player.x − own.x) / 6` if visible, else `0` | [−1, 1], normalised by detection range |
| 5 | Relative player y | `(player.y − own.y) / 6` if visible, else `0` | [−1, 1], normalised by detection range |
| 6 | Own HP | `HP / 100` | [0, 1] |
| 7 | Player HP | `playerHP / 100` | [0, 1] |
| 8 | Attack-ready fraction | `clamp01((t − lastAttackTime) / 0.8)` | [0, 1]; 1 = attack available |
| 9 | Time remaining | `clamp01(1 − elapsed / 60)` | [0, 1] |

Information difference with Brain A: B observes both HP values (needed for DDA); A doesn't use them.
Both have the same perception limit (player position only within 6 u).

### Action space

- Discrete, 2 branches: **branch 0 = 9 moves** (0 = stay; 1–8 = unit vectors at angles 0°, 45°, …, 315°,
  speed 3 u/s); **branch 1 = attack** (0 = no, 1 = try to attack).
- `DecisionRequester`: `DecisionPeriod` = 5 physics steps → **10 decisions/s**;
  `TakeActionsBetweenDecisions` = true (the last action is repeated on the 4 steps in between).
- `MaxStep` = 0 (episodes end only through the round end: death or 60 s timeout). 1 episode = 1 round.

### Reward v1 (exactly as coded; final, only reward-design iteration)

Let `gap = |enemyHP − playerHP| / 100` (in [0, 1]).

1. **Dense shaping, every agent step** (each `OnActionReceived`, 50/s):
   `potential = −0.5 × gap`; reward += `potential − lastPotential`; `lastPotential = potential`
   (`lastPotential` reset to 0 at episode start).
   The sum over a round equals `−0.5 × gap` at the last agent step of the round (telescoping sum).
2. **Terminal, at round end:**
   `engagement = clamp01((damageDealt + damageTaken) / 100)`;
   reward += `engagement × (1 − gap)` (in [0, 1]).
   Avoiding the fight (100 vs 100 at timeout) → engagement 0 → terminal reward 0.

Reward-design iterations: **1** (v1). Logged to TensorBoard: `DDA/FinalHpGap`, `DDA/Engagement`,
`DDA/EnemyWinRate`, `DDA/EnemyLossRate`, `DDA/TimeoutRate`.

### Software versions

| Component | Version |
|---|---|
| Unity Editor | 6000.6.4f1 (Unity 6.6) |
| URP | 17.6.0 |
| Input System | 1.20.0 (new Input System only, `activeInputHandler: 1`) |
| Unity ML-Agents package `com.unity.ml-agents` | 4.1.0 |
| Unity inference package `com.unity.ai.inference` | 2.6.1 (dependency of ML-Agents) |
| Python | 3.10.22 (`ml-venv/`, created with `uv`) |
| `mlagents` / `mlagents_envs` (Python) | 1.1.0 / 1.1.0 |
| PyTorch | 2.2.2+cpu (CUDA not available → training on CPU) |

---

## 4. Training

Source: `config/brainB_ppo.yaml`, `results/<run>/configuration.yaml`, `results/<run>/run_logs/timers.json`,
`results/<run>.console.log`, `LabLog.md`.

**Algorithm:** PPO (`trainer_type: ppo`), ML-Agents 1.1.0.

| Hyperparameter | Value |
|---|---|
| `batch_size` | 512 |
| `buffer_size` | 10240 |
| `learning_rate` | 3.0e-4, schedule `linear` |
| `beta` (entropy) | 5.0e-3 |
| `epsilon` (clip) | 0.2 |
| `lambd` (GAE λ) | 0.95 |
| `num_epoch` | 3 |
| Network | `hidden_units` 128, `num_layers` 2, `normalize` true |
| Reward signal | extrinsic, `gamma` 0.99, `strength` 1.0 |
| `max_steps` | 2 000 000 |
| `time_horizon` | 128 |
| `summary_freq` / `checkpoint_interval` | 10 000 / 100 000 |
| Engine | `time_scale` 20, `no_graphics` true, `num_envs` 1, `num_areas` 1 |
| Environment | Standalone build (`Builds/Training/Arena.exe`), Brain B vs `PlayerProxyBot` |

**Runs** (same config, same build; reward v1):

| Run id | Seed | Start → end (local time, `timers.json`) | Wall-clock | Final step | Model file |
|---|---|---|---|---|---|
| `brainB_v1` | −1 (random) | 2026-10-16 13:23:56 → 14:52:27 | 5 311 s (88.5 min), alone | 2 000 041 | `Assets/Models/BrainB_v1_run1.onnx` |
| `brainB_v1_s2` | 2 | 2026-10-16 15:09:20 → 17:16:23 | 7 623 s (127.1 min), in parallel with s3 | 2 000 062 | `Assets/Models/BrainB_v1_run2.onnx` |
| `brainB_v1_s3` | 3 | 2026-10-16 15:09:20 → 17:16:28 | 7 628 s (127.1 min), in parallel with s2 | 2 000 038 | `Assets/Models/BrainB_v1_run3.onnx` |

Final mean reward (console log, last summary at 2 M steps): run 1 0.77, run 2 0.777, run 3 0.711.

Training curve of run 1 (TensorBoard, `LabLog.md` 2026-10-16 14:52):

| Steps | 10 k | 200 k | 500 k | 1 M | 1.5 M | 2 M |
|---|---|---|---|---|---|---|
| Mean reward | 0.66 | 0.66 | 0.72 | 0.74 | 0.77 | 0.77 |
| Final HP gap | 0.24 | 0.25 | 0.21 | 0.19 | 0.17 | 0.18 |
| B win rate | 40 % | 48 % | 40 % | 19 % | 28 % | 13 % |
| Episode length (decisions) | 124 | 115 | 112 | 105 | 103 | 100 |

Engagement 1.0 and timeout rate 0 % throughout run 1.

**Hardware** (all runs, same machine):

| Component | Value |
|---|---|
| CPU | Intel Core i9-12900K (12th Gen), 16 cores / 24 logical processors |
| RAM | 32 GB (31.7 GB reported by Windows) |
| GPU | AMD Radeon RX 9070 XT (not used for training: PyTorch CPU build, Unity env run with `--no-graphics`) |
| OS | Windows 10 Pro (10.0.19045) |

---

## 5. PlayerProxyBot (training and calibration opponent)

Source: `Assets/Scripts/Player/PlayerProxyBot.cs`.

Drives the same `PlayerController` as the human (same speed 4, damage 10, cooldown 0.6, range 1.2).
Skill `s` ∈ [0, 1]. The bot "thinks" only at intervals; between thinks it keeps its last move.

| Behaviour | Formula (`Lerp(a, b, s)` = a + (b − a)·s) | s = 0.2 | s = 1.0 |
|---|---|---|---|
| Reaction interval (time between thinks) | `Lerp(0.45, 0.12, s)` s | 0.384 s | 0.12 s |
| Movement noise | random vector in a circle of radius `1 − s` | ≤ 0.8 | 0 |
| Attack probability per think, when in range | `Lerp(0.3, 1, s)` | 0.44 | 1.0 |
| Retreat probability per think, when in range | `(1 − s) × 0.25`, retreat lasts 0.6 s | 0.20 | 0 |

At each think, with `dist` = distance to the enemy, `dir` = unit vector toward it:
- if retreating: move = `−dir + noise`;
- else if `dist > 1.08` (= 0.9 × attack range): move = `dir + noise`;
- else (in range): move = `0.5 × noise`, then roll attack and retreat probabilities above.

Move vector is clamped to magnitude 1, then × 4 u/s. The bot can see the enemy at any distance.

- **Training:** skill re-rolled each round with `Random.Range(0.2, 1)` (uniform; Unity's float range includes both ends).
- **Calibration / evaluation:** skill fixed per level (`randomizeSkillEachRound = false`).

---

## 6. Definition of a "close round"

Source: `analysis/select_brainB_model.py`, `CLAUDE.md`.

- **Final HP gap** = `|damage_taken − damage_dealt| / 100`, from the CSV columns (enemy's perspective).
  Since both start at 100 HP and HP never goes below 0, this equals `|enemyHP − playerHP| / 100` at round end.
  Normalised by the starting HP (100) → in [0, 1].
- **Close round:** final HP gap **≤ 0.2** (i.e. HP difference ≤ 20 points).
- **Close-rate** = share of close rounds at one bot skill level.
- **Average close-rate** = unweighted mean of the 5 per-level close-rates.
- Note: the training reward uses the same gap formula (`|enemyHP − playerHP| / 100`) but no threshold.

---

## 7. Calibration / evaluation protocol

Source: `Assets/Scripts/Core/BotCalibration.cs`, `LabLog.md`.

| Item | Value |
|---|---|
| Opponent | `PlayerProxyBot`, skill fixed per level |
| Skill levels | 5: 0.2, 0.4, 0.6, 0.8, 1.0 |
| Rounds per level | 100 → 500 rounds per run |
| Time scale | 20× (same as training) |
| Spawns | Fixed: player (−4, 0), enemy (4, 0) |
| Build | Standalone, `-batchmode -nographics` |
| Output | `bot_calibration.csv` (one row per round, never `sessions.csv`) |
| Scenes | `Calibration_BotVsBrainA` (A-initial), `Calibration_BotVsBrainB` (trained B) |
| Model selection rule (fixed 2026-10-16 before runs 2–3) | Run with the **median** average close-rate of the 3 seeds (tie → median on mean final HP gap) |

---

## 8. Calibration tables

### 8a. A-initial vs B v1 (run 1), 2026-10-16

Source: `data/bot_calibration_20261016.csv` (recomputed 2026-10-20). A = run `20261016_131708`,
B = run `20261016_145411` (`BrainB_v1_run1.onnx`). 100 rounds per cell. Timeouts: 0 % everywhere.

| Bot skill | A win | A loss | A mean gap | A close | A mean time | B win | B loss | B mean gap | B close | B mean time |
|---|---|---|---|---|---|---|---|---|---|---|
| 0.2 | 99 % | 1 % | 0.460 | 7 % | 11.61 s | 43 % | 57 % | 0.089 | 100 % | 16.32 s |
| 0.4 | 76 % | 24 % | 0.188 | 69 % | 11.07 s | 29 % | 71 % | 0.093 | 97 % | 10.97 s |
| 0.6 | 54 % | 46 % | 0.118 | 97 % | 10.99 s | 12 % | 88 % | 0.090 | 100 % | 10.54 s |
| 0.8 | 0 % | 100 % | 0.230 | 53 % | 8.81 s | 0 % | 100 % | 0.270 | 33 % | 8.17 s |
| 1.0 | 0 % | 100 % | 0.410 | 0 % | 6.91 s | 0 % | 100 % | 0.366 | 0 % | 6.82 s |
| **Average close-rate** | | | | **45.2 %** | | | | | **66.0 %** | |

### 8b. Fresh evaluation of the 3 B v1 runs (model selection), 2026-10-16 / 2026-10-20

Source: `data/eval_BrainB_v1_run{1,2,3}.csv`, output of `analysis/select_brainB_model.py`.
Identical builds (`BuildEvaluationBPlayer -brainBModel <onnx>`). Timeouts: 0 % everywhere.

| Run | Metric | 0.2 | 0.4 | 0.6 | 0.8 | 1.0 | Average close-rate | Mean gap (mean of levels) |
|---|---|---|---|---|---|---|---|---|
| run 1 | win / loss | 37 / 63 % | 23 / 77 % | 13 / 87 % | 0 / 100 % | 0 / 100 % | **0.650** | 0.189 |
| | gap / close | 0.09 / 98 % | 0.10 / 99 % | 0.10 / 100 % | 0.27 / 28 % | 0.38 / 0 % | | |
| run 2 | win / loss | 63 / 37 % | 21 / 79 % | 19 / 81 % | 0 / 100 % | 0 / 100 % | **0.656** | 0.184 |
| | gap / close | 0.10 / 99 % | 0.10 / 96 % | 0.09 / 100 % | 0.26 / 33 % | 0.37 / 0 % | | |
| run 3 | win / loss | 98 / 2 % | 51 / 49 % | 34 / 66 % | 0 / 100 % | 0 / 100 % | **0.514** | 0.241 |
| | gap / close | 0.34 / 26 % | 0.15 / 88 % | 0.11 / 98 % | 0.24 / 45 % | 0.36 / 0 % | | |

Order: run 3 (0.514) < run 1 (0.650) < run 2 (0.656) → **selected model: `BrainB_v1_run1.onnx`** (median).

---

## 9. Known limitations (as recorded in `CLAUDE.md` and `LabLog.md`)

**Brain B v1 behaviour**
1. **Narrow-loss lean.** Reward v1 scores a narrow win and a narrow loss the same. B loses by a small
   margin more often as the bot gets stronger: 57 % → 88 % losses from skill 0.2 to 0.6, final HP gap
   ≈ 0.09 throughout (run 1, 16/10). Also visible in run 2 (79–81 % losses at 0.4–0.6). Fixing it =
   reward v2, left to *Perspectives*.
2. **DDA works only downward.** At Brain A's initial stats (slower, weaker than the player), B cannot raise
   the difficulty: vs bot skill 0.8–1.0 it loses heavily like A (100 % losses, gap 0.27–0.37). Direct
   consequence of the "B at A-initial stats" design.
3. **Information difference.** B observes both HP values; A doesn't use them.

**Training and evaluation**

4. **Seed variability.** Same reward and config, only the seed differs: run 3 did not learn to hold back
   vs weak bots (98 % wins, gap 0.34 at skill 0.2, close to A-initial's 99 % / 0.46).
5. **Runs 1 and 2 are a near-tie** (0.650 vs 0.656). The difference is within noise; the median rule was
   applied as fixed.
6. **Evaluation is not deterministic.** Run 1 evaluated twice (16/10 vs 20/10): close-rates
   100/97/100/33/0 % vs 98/99/100/28/0 % (average 0.66 vs 0.65); wins 43/29/12 % vs 37/23/13 %.
   Differences up to 6 points per 100 rounds, same conclusions.
7. **The bot is a substitute for the human player.** Its skill range [0.2, 1] was calibrated against
   Brain A-initial only; it has not been compared with real players.
8. **Training conditions differ slightly from sessions:** random spawn offsets (radius 1.5) in training only;
   the training opponent is the bot, not a human.
9. **Wall-clock times are not comparable between runs:** run 1 ran alone, runs 2–3 ran in parallel.

**Bugs and data hygiene**

10. **Time-scale bug fixed on 2026-10-16.** Before the fix, player, bot and Brain A ran in `Update()`, so
    at 20× they acted only ~3 times per game-second (B: 50). All logic moved to `FixedUpdate`. Results
    from before the fix (15/10 smoke test: B wins 69–75 %) are invalid; after the fix: 36–41 %.
11. **2 rows in `sessions.csv` (15/10, tests, one A and one B) predate the fix → exclude from analysis.**

**Scope and setup**

12. **No Retreat state in Brain A v1** (kept for *Perspectives*).
13. **Unity 6.6 instead of the originally planned 2022 LTS.** ML-Agents 4.1.0 was not necessarily
    officially tested on it; a compatibility test (2026-10-15) passed (compile, build, training, inference).
14. **Pinned Python stack:** `torch==2.2.2` (version tested with mlagents 1.1.0, not the latest),
    `setuptools<81`, Python 3.10 (mlagents 1.1.0 requirement).

**Still open (status on 2026-10-20)**

15. Brain B has not yet had the in-Editor check by the user (step 3).
16. Brain A tuning iterations: 0 so far → the "A-tuned" condition does not exist yet.
17. No human session data collected yet (step 5).

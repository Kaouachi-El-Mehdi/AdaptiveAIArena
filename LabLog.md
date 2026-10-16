# Lab Log — Adaptive AI Arena

Journal brut des sessions de dev/entraînement/playtest. Alimente la section méthodologie du mémoire.

## 2026-10-07 — Setup initial + Brain A (étape 1/6)

**Environnement :** Unity 6.6 (le plan initial visait 2022 LTS — pas disponible/installé,
on part sur 6.6 ; à surveiller à l'étape 4 pour la compatibilité du package ML-Agents,
qui n'a pas forcément été testé officiellement sur une version aussi récente). Template 2D,
URP 17.6.0, nouveau Input System (1.20.0) actif en mode exclusif (`activeInputHandler: 1`)
→ le code utilise `Keyboard.current` (package Input System), pas l'ancien `Input.GetKey`.

**Décisions de scope (confirmées avec l'utilisateur) :**
- Vue top-down 2D (pas 3D) — isole la variable IA, plus simple à construire/observer/défendre.
- Arène 16x10 unités, sans obstacles internes en v1.
- Brain A FSM à 3 états (Idle/Chase/Attack) — état Retreat volontairement exclu de la v1
  (candidat pour la section Perspectives du mémoire, à ajouter seulement s'il reste du temps
  après logging + Brain B + dashboard).
- Timeout de round : 60s, en constante nommée (`GameConstants.RoundTimeoutSeconds`).
- Schéma de logging verrouillé avant implémentation (étape 2) : brain_type, session_id,
  timestamp, outcome, time_to_complete, damage_dealt, damage_taken, tuning_iteration_number
  (Brain A uniquement). Implémenté dès maintenant comme classe `SessionResult` (Core/)
  pour que rien ne bouge une fois le CSV branché.

**Construit :**
- `Health.cs` — composant partagé joueur/ennemi (vie, dégâts cumulés).
- `PlayerController.cs` — déplacement WASD/flèches + attaque (espace), portée/cooldown fixes.
- `EnemyBrainA.cs` — FSM Idle/Chase/Attack, 4 paramètres réglables à la main
  (moveSpeed, attackDamage, attackCooldown, detectionRange) — ce sont ces
  paramètres qui seront comptés comme "itérations de tuning" pour la comparaison dev-cost.
  *[Corrigé le 2026-10-15 : cette entrée indiquait à l'origine 5 paramètres, attackRange
  inclus. Décision : attackRange est fixé à 1.2 pour tous (joueur, Brain A, Brain B) et
  n'est pas un paramètre réglable. Aucune itération de tuning n'avait encore eu lieu.]*
- `RoundManager.cs` — détecte fin de round (victoire/défaite/timeout), construit un
  `SessionResult` et l'affiche dans la Console (pas encore écrit sur disque — étape 2).
- `ArenaSetup.cs` (Editor) — menu `AdaptiveAIArena > Build Arena Scene` qui construit la
  scène (4 murs, joueur, ennemi, RoundManager, références câblées) en un clic plutôt qu'à
  la main dans l'Inspector. Choix délibéré : setup reproductible/scripté, bon point pour
  la méthodologie, et évite l'apprentissage de l'Editor Unity pour cette partie.

**Prochaine étape :** playtest manuel dans l'Editor pour valider que Brain A se comporte
correctement (idle/chase/attack) et que le round se termine proprement, puis étape 2
(écriture CSV réelle).

## 2026-10-14 — Playtest manuel Brain A (validation étape 1)

**Ajouts avant playtest :** logs Console pour rendre le combat observable
(`Enemy hit Player…`, `Player hit Enemy…`, attaque en cooldown, attaque hors portée).
Scène `SampleScene` reconstruite via `AdaptiveAIArena > Build Arena Scene`.

**Résultats (tous validés par l'utilisateur) :**
- Idle tant que le joueur est hors `detectionRange`, passe en Chase quand il s'approche. ✅
- Attaque au contact, dégâts visibles dans la Console. ✅
- Le joueur peut toucher l'ennemi, dégâts visibles dans la Console. ✅
- Fin de round correcte (mort d'un des deux ou timeout 60s) avec `SessionResult`
  affiché dans la Console. ✅

**Statut :** étape 1/6 terminée. Brain A tuning_iteration_number = 0 (valeurs par défaut,
aucun réglage manuel encore).

**Prochaine étape :** étape 2 — écriture réelle des `SessionResult` en CSV.

## 2026-10-14 — Étape 2 : écriture CSV des SessionResult

**Construit :** `SessionLogger.cs` (Core/) — ajoute une ligne par round dans
`Application.persistentDataPath/sessions.csv` (en-tête écrit à la création du fichier).
Floats écrits en `CultureInfo.InvariantCulture` (sinon virgule décimale sur une machine
en locale FR → colonnes CSV cassées).

**Décision méthodologique :** `outcome` est désormais exprimé du point de vue de
l'ennemi (le cerveau testé), comme `damage_dealt`/`damage_taken`. Avant correction,
`outcome` était du point de vue du joueur → une ligne mélangeait deux perspectives
(ex. "A, Loss, damage_dealt=100" alors que Brain A avait gagné). Enemy tue le joueur = Win.
Les 2 lignes de test écrites avant la correction ont été supprimées.

**Validé :** fichier créé, en-tête + 1 ligne par round, colonnes correctes.

## 2026-10-15 14:07 — CLAUDE.md + rappel du plan

**Fait :** cahier des charges complet écrit dans `CLAUDE.md` (racine du projet) pour ne plus
perdre le plan. Ordre officiel des étapes (remplace la reconstruction du 2026-10-14) :
1. Arène + Brain A ✅ 2. Logging CSV ✅ 3. Brain B (ML-Agents, récompense DDA)
4. Entraînement PPO 5. Sessions répétées 6. Script d'analyse Python + figures.

**Nouveau :** la récompense de Brain B vise la difficulté adaptative (DDA) : garder le match
serré, pas maximiser les victoires.

**Écarts relevés entre le plan et le code (à trancher) :**
- 4 paramètres réglables prévus (moveSpeed, attackCooldown, attackDamage, detectionRange),
  mais le code expose aussi `attackRange`.
- Stats joueur ≠ stats Brain A par défaut (vitesse 4 vs 3, dégâts 10 vs 8, cooldown 0.6 vs 0.8).

**Règle :** à partir d'ici, chaque entrée porte date **et heure**.

## 2026-10-15 14:20 — Décisions de conception (symétrie, paramètres, conditions)

- **Paramètres réglables de Brain A : 4** (moveSpeed, attackCooldown, attackDamage,
  detectionRange). `attackRange` = 1.2 fixe pour tous, non réglable (`GameConstants.AttackRange`).
  L'entrée du 2026-10-07 qui disait 5 a été corrigée (annotée).
- **"Symétrique"** = même ensemble d'actions (se déplacer, attaquer) + même portée d'attaque.
  Pas les mêmes valeurs : joueur 4 / 10 / 0.6 (vitesse / dégâts / cooldown) contre
  Brain A initial 3 / 8 / 0.8. Écart voulu : c'est ce que le tuning manuel de Brain A doit corriger.
  Stats joueur passées en constantes (`GameConstants.Player*`) → identiques en entraînement et en session.
- **Brain B** utilise les stats **initiales** (non réglées) de Brain A ; seules ses décisions diffèrent.
- **Trois conditions** pour l'analyse : A-initial, A-réglé, B (aux stats A-initial).
- **Logging :** 4 colonnes ajoutées *en fin* de CSV pour enregistrer les stats en vigueur à
  chaque session : enemy_move_speed, enemy_attack_cooldown, enemy_attack_damage,
  enemy_detection_range. Un ancien `sessions.csv` (ancien en-tête) est renommé
  `sessions_oldschema_<date>.csv`, jamais supprimé.

## 2026-10-15 14:10–14:30 — Test de compatibilité ML-Agents / Unity 6.6 ✅

Durée limitée à ~1 h ; terminé en ~20 min. Fait dans un projet jetable (l'Editor du vrai projet était ouvert).
- **Unity :** `com.unity.ml-agents` 4.1.0 (dépend de `com.unity.ai.inference` 2.6.1) sur Unity 6000.6.4f1.
  Compile et build sans erreur (avertissements de shaders seulement).
- **Python :** `mlagents` 1.1.0 exige Python 3.10 (machine en 3.13) → environnement isolé
  `ml-venv/` créé avec `uv` (CPython 3.10.22), sans toucher au Python système.
  Deux corrections : `setuptools<81` (sinon `pkg_resources` absent) et `torch==2.2.2`
  (version testée avec mlagents 1.1.0 ; la dernière, 2.14.1, n'est pas testée). Versions figées dans `ml-requirements.txt`.
- **Entraînement :** agent jouet 1D, PPO, 30 000 steps en 85 s ; récompense moyenne −0.03 → 0.905. Export `.onnx` OK.
- **Inférence :** le `.onnx` chargé dans un build autonome a atteint la cible **200/200** épisodes.
- **Conclusion :** pas besoin de changer de version d'Unity.

## 2026-10-15 14:30–14:45 — Étape 3 : Brain B (implémentation + smoke test)

**Construit :**
- `EnemyStats` (4 stats réglables, `Initial()` = valeurs non réglées de Brain A) et
  `IEnemyBrain` (RoundManager lit le type de cerveau + les stats en vigueur, plus de champ `brainType` à remplir à la main).
- `EnemyBrainB` (Agent ML-Agents) — **observations (9)** : position propre, joueur visible ?,
  position relative du joueur (seulement si dans detectionRange, même limite de perception que Brain A),
  PV propres, PV joueur, cooldown prêt, temps restant. **Actions** : 9 directions (dont rester sur place)
  + attaquer oui/non ; même règle d'attaque que Brain A. 10 décisions/s.
- **Récompense DDA :** terminale = engagement × (1 − écart de PV final), dans [0, 1]
  (engagement = dégâts totaux échangés / 100, plafonné à 1 → fuir le combat rapporte 0) ;
  + façonnage dense = variation de −0.5 × écart de PV (se télescope, n'ajoute rien sur un round complet).
  Version v1 de la récompense ; chaque modification future = une "itération de conception de récompense" (coût de dev de B).
- **Différence d'information à mentionner dans le mémoire :** Brain B voit les PV des deux combattants
  (nécessaire pour viser un match serré) ; Brain A ne les utilise pas.
- `PlayerProxyBot` — **adversaire d'entraînement de Brain B** : bot scripté qui pilote le même
  `PlayerController` (mêmes stats joueur). Compétence tirée au hasard entre 0.2 et 1 à chaque round
  (temps de réaction, bruit de déplacement, probabilité d'attaque, hésitations).
- `RoundManager` — en entraînement (mlagents-learn connecté) : rounds relancés automatiquement,
  petites variations des positions de départ, **aucune écriture dans sessions.csv**, logs de combat coupés.
- `ArenaSetup` — 3 menus qui créent et enregistrent chacun une scène : Arena_BrainA, Arena_BrainB,
  Training_BrainB (B contre le bot) ; + `BuildTrainingPlayer` (build autonome pour l'entraînement).
  Sprites enregistrés comme assets (`Assets/Generated/`) pour survivre au rechargement de la scène.
- `config/brainB_ppo.yaml` — hyperparamètres PPO (2 M steps max).

**Smoke test** (copie du projet, build autonome, 60 000 steps, 3 min) : compile, build OK, rounds
relancés automatiquement, stats DDA visibles dans TensorBoard. Politique encore ~aléatoire (normal) :
récompense moyenne ~0.51, écart PV final 0.33, B gagne 69–75 %, engagement 1.0, aucun timeout,
rounds ≈ 14 s. Vitesse ≈ 350 steps/s → 2 M steps ≈ 1 h 40.

**Point d'attention :** un Brain B quasi aléatoire bat le bot ~70 % du temps → le bot est
probablement trop faible. À calibrer au début de l'étape 4 (ex. bot contre Brain A initial,
comparé aux résultats de l'utilisateur contre Brain A).

## 2026-10-16 13:00–13:25 — Étape 4 (début) : bug d'échelle de temps corrigé + calibration du bot

**Bug trouvé (important pour la validité de l'entraînement) :** ML-Agents accélère le jeu ×20 pendant
l'entraînement (`time_scale` 20, `capture_frame_rate` 60 par défaut). Chaque image avance alors le temps
de jeu de 1/3 s. Brain B agit au pas physique (50 Hz), mais le bot, `PlayerController` et Brain A
tournaient dans `Update()`, donc **seulement ~3 fois par seconde de jeu** en entraînement. Le bot
réagissait 3 fois par seconde contre 50 pour B : adversaire handicapé, et conditions d'entraînement
différentes du jeu réel.

**Correction :** toute la logique de jeu passe dans `FixedUpdate` (pas physique, 50 Hz, indépendant de la
vitesse) : `PlayerController` (clavier lu dans `Update` puis mis en tampon), `EnemyBrainA`,
`PlayerProxyBot`, détection de fin de round dans `RoundManager`. À vitesse normale, le comportement de
Brain A est quasi inchangé (granularité 20 ms au lieu d'une image). Les 2 lignes actuelles de
`sessions.csv` (15/10, tests) datent d'avant la correction → **à exclure de l'analyse**.

**Vérification (smoke test refait, 60 000 steps, B quasi aléatoire) :**
| | avant correction (15/10) | après correction |
|---|---|---|
| B gagne | 69–75 % | 36–41 % |
| B perd | ~25–30 % | 59–64 % |
| Écart PV final | 0.33 | 0.25 |
| Timeouts | 0 % | 0 % |

**Calibration du bot** (nouveau : scène `Calibration_BotVsBrainA`, composant `BotCalibration`, build
autonome, ×20, 100 rounds par niveau, écrit `bot_calibration.csv`, jamais `sessions.csv`).
Brain A **initial** contre le bot à compétence fixe, positions de départ fixes comme en session réelle.
Données : `data/bot_calibration_20261016.csv` (run 20261016_131708, 2 min 36).

| compétence bot | A gagne | A perd | timeout | durée moy. | écart PV final |
|---|---|---|---|---|---|
| 0.2 | 99 % | 1 % | 0 % | 11.6 s | 0.46 |
| 0.4 | 76 % | 24 % | 0 % | 11.1 s | 0.19 |
| 0.6 | 54 % | 46 % | 0 % | 11.0 s | 0.12 |
| 0.8 | 0 % | 100 % | 0 % | 8.8 s | 0.23 |
| 1.0 | 0 % | 100 % | 0 % | 6.9 s | 0.41 |

**Conclusion :** la plage [0.2, 1] couvre des adversaires nettement plus faibles que Brain A-initial,
des matchs serrés (~0.6) et des adversaires nettement plus forts → plage **conservée** pour
l'entraînement (pas de modification du bot). Limite à écrire dans le mémoire : le bot est un
substitut du joueur humain ; sa plage n'a pas encore été comparée à des joueurs réels.

## 2026-10-16 13:27 — Étape 4 : lancement de l'entraînement complet `brainB_v1`

- Récompense **v1** (itération de conception de récompense n° 1, inchangée depuis le 15/10).
- Config `config/brainB_ppo.yaml` (PPO, 2 M steps max), build autonome avec la correction FixedUpdate,
  adversaire `PlayerProxyBot` (compétence uniforme [0.2, 1], calibrée ci-dessus).
- Commande : `mlagents-learn config/brainB_ppo.yaml --env=<build Training> --run-id=brainB_v1 --no-graphics`
- Durée prévue ≈ 1 h 40 ; durée réelle 88 min (fin 14:52). Résultats : voir entrée suivante.

## 2026-10-16 14:52–15:00 — Résultats `brainB_v1` + évaluation par niveau de compétence

**Courbes d'entraînement** (TensorBoard, `results/brainB_v1`) :
| steps | 10 k | 200 k | 500 k | 1 M | 1.5 M | 2 M |
|---|---|---|---|---|---|---|
| récompense moyenne | 0.66 | 0.66 | 0.72 | 0.74 | 0.77 | 0.77 |
| écart PV final | 0.24 | 0.25 | 0.21 | 0.19 | 0.17 | 0.18 |
| B gagne | 40 % | 48 % | 40 % | 19 % | 28 % | 13 % |
| durée (décisions) | 124 | 115 | 112 | 105 | 103 | 100 |
Engagement 1.0 et 0 % de timeout pendant tout l'entraînement (B n'a pas appris à fuir le combat).

**Modèle :** `Assets/Models/BrainB_v1_run1.onnx` (renommé, ex-`BrainB_v1.onnx`) (copie de `results/brainB_v1/BrainB.onnx`, step 2 000 041).
Utilisé automatiquement par les scènes hors entraînement (`ArenaSetup.BrainBModelPath`).

**Évaluation** (même protocole que la calibration : bot à compétence fixe, 100 rounds par niveau,
×20, positions fixes ; nouvelle scène `Calibration_BotVsBrainB`, run 20261016_145411).
Données dans `data/bot_calibration_20261016.csv` (contient A et B, colonne `brain_type`).
"Serré" = écart de PV final ≤ 0.2.

| bot | A-initial : gagne / perd | écart | serrés | B v1 : gagne / perd | écart | serrés | durée A / B |
|---|---|---|---|---|---|---|---|
| 0.2 | 99 % / 1 % | 0.46 | 7 % | 43 % / 57 % | 0.09 | 100 % | 11.6 / 16.3 s |
| 0.4 | 76 % / 24 % | 0.19 | 69 % | 29 % / 71 % | 0.09 | 97 % | 11.1 / 11.0 s |
| 0.6 | 54 % / 46 % | 0.12 | 97 % | 12 % / 88 % | 0.09 | 100 % | 11.0 / 10.5 s |
| 0.8 | 0 % / 100 % | 0.23 | 53 % | 0 % / 100 % | 0.27 | 33 % | 8.8 / 8.2 s |
| 1.0 | 0 % / 100 % | 0.41 | 0 % | 0 % / 100 % | 0.37 | 0 % | 6.9 / 6.8 s |

**Lecture :**
- **B s'adapte** : contre un bot faible (0.2), A écrase (écart 0.46) alors que B garde le match serré
  (écart 0.09, 100 % de rounds serrés) en ralentissant (rounds plus longs : 16.3 s contre 11.6 s).
  De 0.2 à 0.6, B produit un match serré quasiment à chaque round.
- **Limite haute :** contre les bots forts (0.8–1.0), B perd largement comme A. Avec les stats initiales
  de A (plus lent, moins de dégâts que le joueur), il ne peut pas "monter" en difficulté : la DDA ne
  fonctionne que vers le bas. À écrire dans le mémoire (conséquence directe du choix "B aux stats A-initial").
- **Biais vers la défaite serrée :** plus le bot est fort, plus B perd (57 % → 88 %), toujours de peu.
  La récompense v1 est symétrique (gagner ou perdre de peu rapportent pareil) ; laisser le joueur gagner
  de justesse est plus facile à contrôler pour B. Comportement de DDA classique ("le joueur gagne de
  justesse"), mais à signaler. Changer cela = nouvelle itération de récompense (non faite).

## 2026-10-16 15:20 — Décisions après `brainB_v1`

- **Récompense v1 conservée** (définitive). Une récompense v2 (corriger le biais vers la défaite serrée)
  ira dans les *Perspectives* du mémoire, pas de travail maintenant. Limites de B v1 écrites dans `CLAUDE.md`.
- **3 entraînements de v1** (graines différentes) pour mesurer la variabilité de l'entraînement :
  `brainB_v1` (graine aléatoire, déjà fait), `brainB_v1_s2` (graine 2), `brainB_v1_s3` (graine 3).
- **Règle de choix du modèle pour les sessions humaines, fixée AVANT de voir les runs 2 et 3 :**
  le run dont le *taux de rounds serrés moyen* (écart PV final ≤ 0.2, moyenne des 5 niveaux de bot,
  100 rounds par niveau) est la **médiane** des 3, pas le meilleur → pas de sélection favorable.
  Égalité → médiane sur l'écart PV final moyen.
- Modèle renommé `BrainB_v1_run1.onnx` (les suivants : `_run2`, `_run3`).
- Dossier `Assets/ML-Agents/Timers` (fichiers d'exécution d'ML-Agents) ajouté au `.gitignore`.

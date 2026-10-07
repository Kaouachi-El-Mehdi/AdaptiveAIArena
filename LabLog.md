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
- `EnemyBrainA.cs` — FSM Idle/Chase/Attack, 5 paramètres réglables à la main
  (moveSpeed, attackRange, attackDamage, attackCooldown, detectionRange) — ce sont ces
  paramètres qui seront comptés comme "itérations de tuning" pour la comparaison dev-cost.
- `RoundManager.cs` — détecte fin de round (victoire/défaite/timeout), construit un
  `SessionResult` et l'affiche dans la Console (pas encore écrit sur disque — étape 2).
- `ArenaSetup.cs` (Editor) — menu `AdaptiveAIArena > Build Arena Scene` qui construit la
  scène (4 murs, joueur, ennemi, RoundManager, références câblées) en un clic plutôt qu'à
  la main dans l'Inspector. Choix délibéré : setup reproductible/scripté, bon point pour
  la méthodologie, et évite l'apprentissage de l'Editor Unity pour cette partie.

**Prochaine étape :** playtest manuel dans l'Editor pour valider que Brain A se comporte
correctement (idle/chase/attack) et que le round se termine proprement, puis étape 2
(écriture CSV réelle).

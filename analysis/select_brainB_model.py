"""Pick the Brain B model used in human sessions, by the rule fixed in CLAUDE.md on 2026-10-16
(written before runs 2 and 3 were evaluated):

    close-rate          = share of rounds with final HP gap <= 0.2
    average close-rate  = unweighted mean of the 5 per-bot-skill close-rates
    selected model      = run with the MEDIAN average close-rate (tie -> median on mean gap)

Usage: python analysis/select_brainB_model.py data/eval_BrainB_v1_run1.csv data/eval_BrainB_v1_run2.csv ...
Standard library only.
"""
import csv
import statistics
import sys
from collections import defaultdict

CLOSE_GAP = 0.2


def summarize(path):
    by_skill = defaultdict(list)
    with open(path, newline="") as f:
        for r in csv.DictReader(f):
            gap = abs(float(r["damage_taken"]) - float(r["damage_dealt"])) / 100.0
            by_skill[float(r["bot_skill"])].append((r["outcome"], gap))

    levels = {}
    for skill in sorted(by_skill):
        rounds = by_skill[skill]
        n = len(rounds)
        levels[skill] = {
            "n": n,
            "win": sum(o == "Win" for o, _ in rounds) / n,
            "loss": sum(o == "Loss" for o, _ in rounds) / n,
            "timeout": sum(o == "Timeout" for o, _ in rounds) / n,
            "gap": statistics.mean(g for _, g in rounds),
            "close": sum(g <= CLOSE_GAP for _, g in rounds) / n,
        }
    return {
        "path": path,
        "levels": levels,
        "avg_close": statistics.mean(l["close"] for l in levels.values()),
        "mean_gap": statistics.mean(l["gap"] for l in levels.values()),
    }


def main(paths):
    runs = [summarize(p) for p in paths]
    for run in runs:
        print(f"\n{run['path']}  average close-rate {run['avg_close']:.3f}  mean gap {run['mean_gap']:.3f}")
        print("  skill    n   win  loss  t/o   gap  close")
        for skill, l in run["levels"].items():
            print(f"  {skill:4.1f}  {l['n']:4d}  {l['win']:.2f}  {l['loss']:.2f}  {l['timeout']:.2f}  {l['gap']:.2f}  {l['close']:.2f}")

    if len(runs) % 2 == 0:
        sys.exit("\nNeed an odd number of runs for a median.")
    # Sort by close-rate; ties broken by mean gap (median of the tied group, per the rule).
    ordered = sorted(runs, key=lambda r: (r["avg_close"], -r["mean_gap"]))
    selected = ordered[len(ordered) // 2]
    print("\nOrder by average close-rate:", " < ".join(f"{r['path']} ({r['avg_close']:.3f})" for r in ordered))
    print(f"SELECTED (median): {selected['path']}")


if __name__ == "__main__":
    main(sys.argv[1:])

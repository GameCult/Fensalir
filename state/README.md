# Persistent State

This directory is Fensalir's repo-local memory.

- `map.yaml` is the current ownership and pipeline map.
- `memory.json` is durable operating memory for future agents.
- `evidence.jsonl` is the historical lesson ledger. It includes pre-split
  entries from Aquarium Engine history; file paths may point to code that now
  lives in the Epiphany Aquarium client repo. Treat it as lineage evidence, not
  the current repo map.
- `scratch.md` is disposable active-slice context.

When the engine learns something durable, update `memory.json`, add a compact
evidence row, and revise `map.yaml` if ownership changed.

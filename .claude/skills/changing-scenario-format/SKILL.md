---
name: changing-scenario-format
description: Use when adding, renaming or removing a scenario field, template or validation rule in LoadKit, or when scenario JSON, schema and docs disagree.
---

# Changing the scenario format

## Overview

The scenario format is a public contract read by people, IDEs and AI agents in other repos.
It lives in six places that must change together. Architecture: `docs/architecture/SCENARIOS.md`.

## Checklist (all in one PR)

1. **Model**: `src/LoadKit.Core/Scenarios/Model/*`.
2. **Validator** (source of truth): structural and semantic checks in `ScenarioValidator`, error message
   says how to fix and includes the JSON path. No JSON Schema library — see `docs/architecture/SCENARIOS.md`.
3. **Schema** for IDEs: `schemas/scenario.schema.json` (`additionalProperties: false` stays on);
   the schema/model consistency test must pass.
4. **Reference**: `ai/skills/loadtest/SCENARIO_REFERENCE.md` — table row and, if useful, an example.
5. **Skill**: `ai/skills/loadtest/SKILL.md` — only if agent behavior should change (defaults, red flags).
6. **Samples**: `samples/scenarios/*.json` still valid; add one if the feature is user-visible.
7. **Tests**: model/validator unit tests; docs tests (`Category=Docs`) pass.

## Breaking vs non-breaking

- New optional field with default → non-breaking, `version` stays.
- Rename/remove field, change meaning or default → breaking: bump `version`,
  keep reading the old version with a clear migration error, bump
  `scenario-format-version` in SKILL.md metadata, add an ADR in `docs/decisions/`.

## Red flags

- Field added to the model but not to the schema → IDE users get false errors (consistency test catches it).
- Auth fragment examples in the reference must keep the `<!-- fragment:auth -->` marker.
- Example in reference not validated → docs test is skipped or deleted. Never delete it.
- Skill metadata version not bumped after a breaking change → old skills in user repos generate invalid scenarios.

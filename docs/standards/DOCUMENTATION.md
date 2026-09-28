# Documentation rules

> Status: ready

## Layers

1. **Navigation** — `AGENTS.md` (+ `CLAUDE.md` as a thin layer on top for Claude Code).
2. **Rules and design** — `docs/`.
3. **Product artifacts for users** — `ai/skills/loadtest/`, `docs/user/`.

On conflict, use the order from `AGENTS.md`. Code describes the implementation, documentation describes intent.
If the code has moved ahead, the documentation is updated in the same PR.

## Language and names

- All documentation is written in English.
- File names are English, `UPPER_SNAKE.md`. ADRs: `ADR-NNN-kebab-title.md`.

## Status

At the top of every document: `> Status: draft` or `> Status: ready`.
`draft` means the document is ahead of the code or still under discussion. `ready` means it matches the code.

## Where to put it

| What | Where |
|---|---|
| A rule mandatory for all code | `docs/standards/` |
| How a component works | `docs/architecture/<COMPONENT>.md` |
| Why a decision was made, alternatives | `docs/decisions/ADR-NNN-*.md` |
| Instructions for users of the tool | `docs/user/` |
| Instructions for AI in someone else's repository | `ai/skills/loadtest/` |
| A repeatable development procedure for an agent | `.claude/skills/<verb-ing-name>/` |
| A term | `docs/glossary/TERMS.md` |

A new document is added to the `docs/README.md` index in the same PR.

## Component document template

1. Purpose (1–3 sentences)
2. Files (paths)
3. Contracts (interfaces, formats)
4. Execution flow
5. Edge cases
6. How to extend
7. Tests (what is covered and where)

## Skills (SKILL.md)

- Frontmatter uses only Agent Skills spec fields: `name`, `description`, optionally `metadata`,
  `license`, `compatibility`, `allowed-tools`.
- `description` starts with "Use when…" and describes only when to apply the skill, without restating the steps.
- The body is short (up to ~500 words); heavy references go into separate files next to it.

## Verifiable examples

JSON blocks in `SCENARIO_REFERENCE.md` and `docs/user/*` are checked by `Category=Docs` tests:
- a block without a marker is a full scenario;
- `<!-- fragment:auth -->` before a block means an `auth` object, checked as a fragment;
- `<!-- no-validate -->` means not checked (use as a last resort, with a reason next to it).

---
name: adding-auth-provider
description: Use when adding a new auth type to LoadKit, changing how tokens are acquired or refreshed, or fixing 401/403 behavior during load runs.
---

# Adding an auth provider

## Overview

An auth type touches five places. Missing one gives a provider that compiles but cannot be
configured, is not validated, or is unknown to user agents. Architecture: `docs/architecture/AUTH.md`.

## Checklist (all in one PR)

1. **Provider** in `src/LoadKit.Core/Auth/Providers/<Name>AuthProvider.cs`
   - Static credentials → implement `IAuthProvider` directly.
   - Expiring tokens → inherit `TokenAuthProviderBase`, implement only `AcquireTokenAsync`.
2. **Config model** `<Name>AuthOptions` (record) + mapping in `AuthOptionsParser`.
3. **Registration** in `AuthProviderFactory`: `"<type>" → provider`.
4. **Schema**: new `oneOf` branch for `auth` in `schemas/scenario.schema.json`.
5. **User docs**: example in `ai/skills/loadtest/SCENARIO_REFERENCE.md` (Authentication) and a row
   in the "Choosing auth" table of `ai/skills/loadtest/SKILL.md`; section in `docs/user/GETTING_STARTED.md`.
6. **Init**: add the option to the interactive `loadtest init` auth question and `--auth` flag.
7. **Tests**:
   - unit: `InitializeAsync` success/failure, `ApplyAsync` sets header/query, secrets are masked;
   - for token providers: refresh before expiry, single refresh under concurrency, 401 marks token stale;
   - integration: `samples/TargetApi` `/secure` accepts the new auth. If tokens come from an external
     identity provider, add a fake issuer endpoint to TargetApi (like `/oauth2/token`) or use a fake
     `TokenCredential`; never call real identity providers in tests.

## Rules

- `ApplyAsync` is hot path: read cached value only, never await network.
- All secret fields accept `${env:...}`; validator rejects literal secret-like values.
- Add every new secret-carrying header/field to `SecretMasker` and to the `secret-literal` validation rule.
- Static providers (no expiry) implement `MarkStale` as a counter only.
- No retries on 401 during the run — they distort metrics.

## Common mistakes

| Mistake | Symptom |
|---|---|
| Token fetched in `ApplyAsync` | p99 spikes every token lifetime |
| Missing masker entry | secret visible in `report.md` |
| Schema branch missing | IDE shows "unknown property", validate fails |
| Skill table not updated | user agents never pick the new type |

# Quality Strategy

This document defines how quality is measured and enforced for the repository.

## 1) Test Pyramid

1. `Integration tests` validate real end-to-end behavior (API + EF provider + query parsing + repository behavior).
2. `Unit tests` validate pure logic branches that are expensive/impractical to hit through end-to-end flows.
3. `Regression tests` are mandatory for every production bug fix.

## 2) Provider Coverage Policy

For repository quality confidence, tests are split by data provider:

1. PostgreSQL integration suite (primary confidence suite).
2. SQLite integration suite (compatibility/smoke provider suite).
3. SQL Server integration suite (compatibility provider suite).

`PostgreSQL` coverage alone is not enough to claim complete library confidence if uncovered code belongs to provider-specific modules.

## 3) Coverage Gates

Quality gates are used to prevent regressions in CI.

Current enforced baselines (via `scripts/library-quality.sh`):

1. Overall line coverage: `>= 68%`
2. Overall branch coverage: `>= 64%`
3. `KyrolusSous.Repositories.EF.Runtime`: line `>= 95%`, branch `>= 85%`
4. `KyrolusSous.Repositories.EF.Abstractions`: line `>= 65%`, branch `>= 55%`
5. `KyrolusSous.Caching.Abstractions`: line `>= 10%`, branch `>= 10%`

These thresholds are baseline gates (anti-regression), not final targets.

## 4) Local Commands

Run PostgreSQL coverage + gates:

```bash
bash scripts/library-quality.sh
```

Run PostgreSQL coverage + gates and SQLite tests:

```bash
bash scripts/library-quality.sh --with-sqlite
```

Run SQL Server tests too:

```bash
bash scripts/library-quality.sh --with-sqlserver
```

Run only provider tests and skip coverage:

```bash
bash scripts/library-quality.sh --skip-coverage --with-sqlite
```

## 5) CI Policy

A PR is considered quality-safe only when:

1. Integration tests pass for required provider suites.
2. Coverage gates pass.
3. No known flaky test is introduced.

## 6) Raising Trust Over Time

To increase consumer confidence:

1. Increase coverage thresholds gradually every sprint.
2. Add contract test cases shared across providers for key operations.
3. Track and reduce flaky tests to near-zero.
4. Keep runtime core at very high coverage and stable pass rate.

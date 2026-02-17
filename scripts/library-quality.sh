#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash scripts/library-quality.sh [options]

Options:
  -t, --target <path>           Target test project/solution for coverage run.
                                Default: PostgreSQL integration tests project.
      --with-sqlite             Run SQLite integration tests after coverage gates.
      --with-sqlserver          Run SQL Server integration tests after coverage gates.
      --skip-coverage           Skip coverage run/gates (only provider test runs).
      --min-line <num>          Overall min line coverage percent (default: 68).
      --min-branch <num>        Overall min branch coverage percent (default: 64).
      --python <exe>            Python executable (default: auto python3/python).
  -h, --help                    Show this help.

Notes:
  - This script depends on scripts/dotnet-coverage.sh output at:
      TestResults/coverage-run/Coverage/coverage.cobertura.xml
  - Default quality gates are tuned for current baseline and intended to prevent regressions.
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DEFAULT_TARGET="Tests/KyrolusSous.Repositories.EF.Runtime/IntegrationTests/IntegrationTests.postgressql/KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.csproj"
SQLITE_PROJECT="Tests/KyrolusSous.Repositories.EF.Runtime/IntegrationTests/IntegrationTests.SQLlite/KyrolusSous.Repositories.EF.Runtime.IntegrationTests.SQLlite.csproj"
SQLSERVER_PROJECT="Tests/KyrolusSous.Repositories.EF.Runtime/IntegrationTests/IntegrationTests.SQLSever/KyrolusSous.Repositories.EF.Runtime.IntegrationTests.SQLSever.csproj"
REPORT_PATH="$REPO_ROOT/TestResults/coverage-run/Coverage/coverage.cobertura.xml"

TARGET="$DEFAULT_TARGET"
WITH_SQLITE="false"
WITH_SQLSERVER="false"
SKIP_COVERAGE="false"
MIN_LINE="68"
MIN_BRANCH="64"
PYTHON_EXE=""
PYTHON_CMD=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -t|--target)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      TARGET="$2"
      shift 2
      ;;
    --with-sqlite)
      WITH_SQLITE="true"
      shift
      ;;
    --with-sqlserver)
      WITH_SQLSERVER="true"
      shift
      ;;
    --skip-coverage)
      SKIP_COVERAGE="true"
      shift
      ;;
    --min-line)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      MIN_LINE="$2"
      shift 2
      ;;
    --min-branch)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      MIN_BRANCH="$2"
      shift 2
      ;;
    --python)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      PYTHON_EXE="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
done

if [[ -n "$PYTHON_EXE" ]]; then
  if ! "$PYTHON_EXE" -V >/dev/null 2>&1; then
    echo "ERROR: Provided python executable is not runnable: $PYTHON_EXE"
    exit 1
  fi
  PYTHON_CMD=("$PYTHON_EXE")
else
  if command -v python3 >/dev/null 2>&1 && python3 -V >/dev/null 2>&1; then
    PYTHON_CMD=(python3)
  elif command -v python >/dev/null 2>&1 && python -V >/dev/null 2>&1; then
    PYTHON_CMD=(python)
  elif command -v py >/dev/null 2>&1 && py -3 -V >/dev/null 2>&1; then
    PYTHON_CMD=(py -3)
  else
    echo "ERROR: Python is required (python3/python/py not found)."
    exit 1
  fi
fi

run_provider_tests() {
  local project="$1"
  local label="$2"
  if [[ ! -f "$REPO_ROOT/$project" ]]; then
    echo "WARN: $label project not found at '$project'. Skipping."
    return 0
  fi

  echo "Running $label integration tests..."
  dotnet test "$REPO_ROOT/$project" -c Debug --no-restore
}

if [[ "$SKIP_COVERAGE" != "true" ]]; then
  echo "Running coverage + report for target: $TARGET"
  bash "$SCRIPT_DIR/dotnet-coverage.sh" -t "$TARGET"

  if [[ ! -f "$REPORT_PATH" ]]; then
    echo "ERROR: Coverage report not found: $REPORT_PATH"
    exit 1
  fi

  echo "Applying coverage quality gates..."
  "${PYTHON_CMD[@]}" "$SCRIPT_DIR/coverage-gate.py" \
    --report "$REPORT_PATH" \
    --min-line "$MIN_LINE" \
    --min-branch "$MIN_BRANCH" \
    --package "KyrolusSous.Repositories.EF.Runtime:95:85" \
    --package "KyrolusSous.Repositories.EF.Abstractions:65:55" \
    --package "KyrolusSous.Caching.Abstractions:10:10"
fi

if [[ "$WITH_SQLITE" == "true" ]]; then
  run_provider_tests "$SQLITE_PROJECT" "SQLite"
fi

if [[ "$WITH_SQLSERVER" == "true" ]]; then
  run_provider_tests "$SQLSERVER_PROJECT" "SQL Server"
fi

echo "Library quality checks completed successfully."

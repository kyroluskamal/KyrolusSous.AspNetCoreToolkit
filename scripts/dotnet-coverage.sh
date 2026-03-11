#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash scripts/dotnet-coverage.sh [options]

Options:
  -t, --target <path>        Test target (.sln or .csproj).
                             If omitted, script auto-uses the single .sln at repo root.
  -f, --filter <expr>        Optional dotnet test filter expression.
  -c, --configuration <cfg>  Build configuration (default: Debug).
      --no-restore           Skip restore.
  -h, --help                 Show this help.

Examples:
  bash scripts/dotnet-coverage.sh
  bash scripts/dotnet-coverage.sh -t Tests/Project.Tests/Project.Tests.csproj
  bash scripts/dotnet-coverage.sh -f "FullyQualifiedName~GetAllAsyncTests"
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

to_tool_path() {
  local path="$1"
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$path"
  else
    printf '%s' "$path"
  fi
}

TARGET=""
FILTER=""
CONFIGURATION="Debug"
NO_RESTORE="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -t|--target)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      TARGET="$2"
      shift 2
      ;;
    -f|--filter)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      FILTER="$2"
      shift 2
      ;;
    -c|--configuration)
      [[ $# -ge 2 ]] || { echo "Missing value for $1"; exit 1; }
      CONFIGURATION="$2"
      shift 2
      ;;
    --no-restore)
      NO_RESTORE="true"
      shift
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

if [[ -z "$TARGET" ]]; then
  mapfile -t SLN_CANDIDATES < <(find "$REPO_ROOT" -maxdepth 1 -type f -name "*.sln" | sort)
  if [[ ${#SLN_CANDIDATES[@]} -eq 1 ]]; then
    TARGET="${SLN_CANDIDATES[0]}"
  elif [[ ${#SLN_CANDIDATES[@]} -eq 0 ]]; then
    echo "ERROR: No .sln file found at repo root. Pass --target explicitly."
    exit 1
  else
    echo "ERROR: Multiple .sln files found at repo root. Pass --target explicitly."
    printf ' - %s\n' "${SLN_CANDIDATES[@]}"
    exit 1
  fi
fi

if [[ ! -f "$TARGET" ]]; then
  if [[ -f "$REPO_ROOT/$TARGET" ]]; then
    TARGET="$REPO_ROOT/$TARGET"
  fi
fi

if [[ ! -f "$TARGET" ]]; then
  echo "ERROR: Target not found: $TARGET"
  exit 1
fi

cd "$REPO_ROOT"

RESULTS_ROOT="$REPO_ROOT/TestResults/coverage-run"
NORMALIZED_DIR="$RESULTS_ROOT/Coverage"
COBERTURA_FILE="$NORMALIZED_DIR/coverage.cobertura.xml"
RAW_COBERTURA_FILE="$RESULTS_ROOT/coverage.raw.cobertura.xml"
TRX_FILE="$NORMALIZED_DIR/test-results.trx"
REPORT_INDEX_HTML="$NORMALIZED_DIR/index.html"
REPORT_INDEX_HTM="$NORMALIZED_DIR/index.htm"
RUNNER_CMD_FILE="$RESULTS_ROOT/run-tests.cmd"
RUNNER_CMD_CMD="TestResults\\coverage-run\\run-tests.cmd"
RUNNER_PS1_FILE="$RESULTS_ROOT/run-coverage.ps1"
RUNNER_PS1_CMD="TestResults\\coverage-run\\run-coverage.ps1"

echo "Cleaning old coverage output..."
rm -rf "$RESULTS_ROOT"
mkdir -p "$RESULTS_ROOT" "$NORMALIZED_DIR"

echo "Running tests with coverage..."
echo "Target: $TARGET"

TARGET_WIN="$(to_tool_path "$TARGET")"
RESULTS_ROOT_WIN="$(to_tool_path "$RESULTS_ROOT")"
RAW_COBERTURA_WIN="$(to_tool_path "$RAW_COBERTURA_FILE")"
COVERAGE_TOOL_WIN=""

dotnet_test_args=(
  "$TARGET_WIN"
  -c "$CONFIGURATION"
  --results-directory "$RESULTS_ROOT_WIN"
  --logger "trx;LogFileName=test-results.trx"
  -m:1
)

if [[ "$NO_RESTORE" == "true" ]]; then
  dotnet_test_args+=(--no-restore)
fi

if [[ -n "$FILTER" ]]; then
  dotnet_test_args+=(--filter "$FILTER")
fi

coverage_tool=""
if [[ -x "$HOME/.dotnet/tools/dotnet-coverage.exe" ]]; then
  coverage_tool="$HOME/.dotnet/tools/dotnet-coverage.exe"
elif [[ -x "$HOME/.dotnet/tools/dotnet-coverage" ]]; then
  coverage_tool="$HOME/.dotnet/tools/dotnet-coverage"
elif [[ -x "$REPO_ROOT/.tools/dotnet-coverage" ]]; then
  coverage_tool="$REPO_ROOT/.tools/dotnet-coverage"
elif [[ -x "$REPO_ROOT/.tools/dotnet-coverage.exe" ]]; then
  coverage_tool="$REPO_ROOT/.tools/dotnet-coverage.exe"
elif command -v dotnet-coverage >/dev/null 2>&1; then
  coverage_tool="dotnet-coverage"
fi

if [[ -n "$coverage_tool" ]]; then
  echo "Using dotnet-coverage: $coverage_tool"
  COVERAGE_TOOL_WIN="$(to_tool_path "$coverage_tool")"
  {
    printf '@echo off\r\n'
    printf 'dotnet test'
    for arg in "${dotnet_test_args[@]}"; do
      printf ' "%s"' "$arg"
    done
    printf '\r\n'
  } > "$RUNNER_CMD_FILE"

  {
    printf '$ErrorActionPreference = '"'"'Stop'"'"'\r\n'
    printf '& "%s" collect cmd /c "%s" -o "%s" -f cobertura\r\n' \
      "$COVERAGE_TOOL_WIN" \
      "$RUNNER_CMD_CMD" \
      "$RAW_COBERTURA_WIN"
  } > "$RUNNER_PS1_FILE"

  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$RUNNER_PS1_CMD"
else
  test_cmd=(
    dotnet test "$TARGET"
    -c "$CONFIGURATION"
    --results-directory "$RESULTS_ROOT"
    --logger "trx;LogFileName=test-results.trx"
    --collect:"XPlat Code Coverage"
    -m:1
  )

  if [[ "$NO_RESTORE" == "true" ]]; then
    test_cmd+=(--no-restore)
  fi

  if [[ -n "$FILTER" ]]; then
    test_cmd+=(--filter "$FILTER")
  fi

  "${test_cmd[@]}"
fi

TRX_SOURCE="$(find "$RESULTS_ROOT" -type f -name "*.trx" | head -n 1 || true)"

declare -a COVERAGE_FILES=()
if [[ -f "$RAW_COBERTURA_FILE" ]]; then
  COVERAGE_FILES=("$RAW_COBERTURA_FILE")
else
  mapfile -t COVERAGE_FILES < <(find "$RESULTS_ROOT" -type f -name "coverage.cobertura.xml" | sort)
  if [[ ${#COVERAGE_FILES[@]} -eq 0 ]]; then
    echo "ERROR: coverage.cobertura.xml was not generated."
    exit 1
  fi
fi

if [[ -n "$TRX_SOURCE" ]]; then
  cp "$TRX_SOURCE" "$TRX_FILE"
fi

if command -v reportgenerator >/dev/null 2>&1; then
  reports_arg=""
  for coverage_file in "${COVERAGE_FILES[@]}"; do
    tool_path="$(to_tool_path "$coverage_file")"
    reports_arg+="${tool_path};"
  done
  reports_arg="${reports_arg%;}"

  targetdir_arg="$(to_tool_path "$NORMALIZED_DIR")"

  echo "Generating merged Cobertura + HTML report..."
  reportgenerator \
    "-reports:$reports_arg" \
    "-targetdir:$targetdir_arg" \
    "-reporttypes:Cobertura;HtmlInline"

  if [[ -f "$NORMALIZED_DIR/Cobertura.xml" ]]; then
    mv -f "$NORMALIZED_DIR/Cobertura.xml" "$COBERTURA_FILE"
  fi
else
  if [[ ${#COVERAGE_FILES[@]} -gt 1 ]]; then
    echo "WARNING: reportgenerator not installed. Multiple coverage files found; using first only."
  fi
  cp "${COVERAGE_FILES[0]}" "$COBERTURA_FILE"
fi

echo "Coverage XML: $COBERTURA_FILE"
if [[ -f "$TRX_FILE" ]]; then
  echo "TRX: $TRX_FILE"
fi

open_path=""
if [[ -f "$REPORT_INDEX_HTML" ]]; then
  open_path="$REPORT_INDEX_HTML"
elif [[ -f "$REPORT_INDEX_HTM" ]]; then
  open_path="$REPORT_INDEX_HTM"
fi

if [[ -n "$open_path" ]]; then
  echo "HTML report: $open_path"
  if command -v cygpath >/dev/null 2>&1; then
    win_path="$(cygpath -w "$open_path")"
    cmd.exe /c start "" "$win_path" >/dev/null 2>&1 || true
  else
    explorer.exe "$open_path" >/dev/null 2>&1 || true
  fi
else
  echo "HTML report not generated."
  if ! command -v reportgenerator >/dev/null 2>&1; then
    echo "Install once: dotnet tool install -g dotnet-reportgenerator-globaltool"
  fi
fi

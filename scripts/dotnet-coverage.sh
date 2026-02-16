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

RESULTS_ROOT="$REPO_ROOT/TestResults/coverage-run"
NORMALIZED_DIR="$RESULTS_ROOT/Coverage"
COBERTURA_FILE="$NORMALIZED_DIR/coverage.cobertura.xml"
TRX_FILE="$NORMALIZED_DIR/test-results.trx"
REPORT_INDEX_HTML="$NORMALIZED_DIR/index.html"
REPORT_INDEX_HTM="$NORMALIZED_DIR/index.htm"

echo "Cleaning old coverage output..."
rm -rf "$RESULTS_ROOT"
mkdir -p "$RESULTS_ROOT" "$NORMALIZED_DIR"

echo "Running tests with coverage..."
echo "Target: $TARGET"

test_cmd=(
  dotnet test "$TARGET"
  -c "$CONFIGURATION"
  --results-directory "$RESULTS_ROOT"
  --logger "trx;LogFileName=test-results.trx"
  --collect:"XPlat Code Coverage"
)

if [[ "$NO_RESTORE" == "true" ]]; then
  test_cmd+=(--no-restore)
fi

if [[ -n "$FILTER" ]]; then
  test_cmd+=(--filter "$FILTER")
fi

"${test_cmd[@]}"

mapfile -t COVERAGE_FILES < <(find "$RESULTS_ROOT" -type f -name "coverage.cobertura.xml" | sort)
TRX_SOURCE="$(find "$RESULTS_ROOT" -type f -name "*.trx" | head -n 1 || true)"

if [[ ${#COVERAGE_FILES[@]} -eq 0 ]]; then
  echo "ERROR: coverage.cobertura.xml was not generated."
  exit 1
fi

if [[ -n "$TRX_SOURCE" ]]; then
  cp "$TRX_SOURCE" "$TRX_FILE"
fi

if command -v reportgenerator >/dev/null 2>&1; then
  reports_arg="$(printf '%s;' "${COVERAGE_FILES[@]}")"
  reports_arg="${reports_arg%;}"

  echo "Generating merged Cobertura + HTML report..."
  reportgenerator \
    "-reports:$reports_arg" \
    "-targetdir:$NORMALIZED_DIR" \
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

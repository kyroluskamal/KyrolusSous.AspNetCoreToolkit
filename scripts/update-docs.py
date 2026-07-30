#!/usr/bin/env python3
"""
Regenerate the machine-derivable parts of README.md and docs/QUALITY_STRATEGY.md.

Only the regions between `<!-- BEGIN:GENERATED:id -->` and `<!-- END:GENERATED:id -->`
are touched, so hand-written prose around them is preserved.

Sources of truth:
  - quality-gates.json                  thresholds, test suite inventory
  - Src/**/*.csproj                     project list and sizes
  - Tests/** [Fact]/[Theory] counts     test case counts
  - the Cobertura report (if present)    measured coverage

Usage:
  python scripts/update-docs.py           # rewrite the generated blocks
  python scripts/update-docs.py --check   # exit 1 if any block is stale (for CI)
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List

REPO_ROOT = Path(__file__).resolve().parent.parent
CONFIG_PATH = REPO_ROOT / "quality-gates.json"
README_PATH = REPO_ROOT / "README.md"
QUALITY_DOC_PATH = REPO_ROOT / "docs" / "QUALITY_STRATEGY.md"

BLOCK_PATTERN = "<!-- BEGIN:GENERATED:{id} -->"
END_PATTERN = "<!-- END:GENERATED:{id} -->"
TEST_ATTRIBUTE = re.compile(r"^\s*\[\s*(Fact|Theory)\b", re.MULTILINE)
SKIPPED_DIRS = {"bin", "obj", "TestResults"}


# --------------------------------------------------------------------------- #
# Repository facts
# --------------------------------------------------------------------------- #


@dataclass(frozen=True)
class ProjectInfo:
    name: str
    files: int
    lines: int
    references: List[str]


def read_text(path: Path) -> str:
    """Read a source file tolerantly - the repo mixes BOM and non-BOM files."""
    return path.read_text(encoding="utf-8-sig", errors="replace")


def source_files(root: Path) -> Iterable[Path]:
    for path in root.rglob("*.cs"):
        if SKIPPED_DIRS.isdisjoint(path.parts):
            yield path


def scan_projects() -> List[ProjectInfo]:
    projects: List[ProjectInfo] = []
    for csproj in sorted((REPO_ROOT / "Src").rglob("*.csproj")):
        if not SKIPPED_DIRS.isdisjoint(csproj.parts):
            continue

        directory = csproj.parent
        files = list(source_files(directory))
        lines = sum(len(read_text(f).splitlines()) for f in files)
        references = sorted(
            Path(match.replace("\\", "/")).stem
            for match in re.findall(r'ProjectReference\s+Include="([^"]+)"', read_text(csproj))
        )
        projects.append(ProjectInfo(csproj.stem, len(files), lines, references))

    return projects


def count_test_cases(project_path: Path) -> int:
    directory = project_path.parent
    if not directory.is_dir():
        return 0
    return sum(len(TEST_ATTRIBUTE.findall(read_text(f))) for f in source_files(directory))


def load_coverage(report_path: Path) -> tuple[Dict[str, tuple[float, float]], tuple[float, float]] | None:
    if not report_path.exists():
        return None

    root = ET.parse(report_path).getroot()

    def pct(value: str | None) -> float:
        try:
            return float(value) * 100.0 if value is not None else 0.0
        except ValueError:
            return 0.0

    packages = {
        (pkg.attrib.get("name") or "").strip(): (pct(pkg.attrib.get("line-rate")), pct(pkg.attrib.get("branch-rate")))
        for pkg in root.findall("./packages/package")
        if (pkg.attrib.get("name") or "").strip()
    }
    return packages, (pct(root.attrib.get("line-rate")), pct(root.attrib.get("branch-rate")))


# --------------------------------------------------------------------------- #
# Block renderers
# --------------------------------------------------------------------------- #


def family_for(project_name: str, families: List[dict]) -> str:
    """Longest matching prefix wins, so Repositories.EF beats a bare Repositories."""
    suffix = project_name.removeprefix("KyrolusSous.")
    best = ""
    title = "Other"
    for family in families:
        prefix = family["prefix"]
        if (suffix == prefix or suffix.startswith(prefix + ".")) and len(prefix) > len(best):
            best, title = prefix, family["title"]
    return title


def render_projects(projects: List[ProjectInfo], families: List[dict]) -> str:
    grouped: Dict[str, List[ProjectInfo]] = {}
    for project in projects:
        grouped.setdefault(family_for(project.name, families), []).append(project)

    # Keep the config's family order, with anything unmatched last.
    ordered_titles: List[str] = []
    for family in families:
        if family["title"] in grouped and family["title"] not in ordered_titles:
            ordered_titles.append(family["title"])
    ordered_titles += sorted(t for t in grouped if t not in ordered_titles)

    total_lines = sum(p.lines for p in projects)
    lines = [
        f"{len(projects)} projects, {total_lines:,} lines of C#. Grouped by area; "
        "`Abstractions` packages hold contracts only and carry no runtime dependency.",
        "",
    ]

    for title in ordered_titles:
        members = sorted(grouped[title], key=lambda p: p.name)
        lines.append(f"**{title}**")
        lines.append("")
        lines.append("| Project | Files | Lines | Depends on |")
        lines.append("| --- | ---: | ---: | --- |")
        for project in members:
            deps = ", ".join(d.removeprefix("KyrolusSous.") for d in project.references) or "-"
            lines.append(f"| `{project.name}` | {project.files} | {project.lines:,} | {deps} |")
        lines.append("")

    return "\n".join(lines).rstrip()


def render_thresholds(config: dict) -> str:
    coverage = config.get("coverage", {})
    overall = coverage.get("overall", {})

    lines = [
        "These are anti-regression baselines, not final targets. They live in",
        "[`quality-gates.json`](../quality-gates.json) and are the only place they are defined -",
        "this table is generated from it by `scripts/update-docs.py`.",
        "",
        "| Scope | Min line | Min branch |",
        "| --- | ---: | ---: |",
        f"| **Overall** | {overall.get('line', 0)}% | {overall.get('branch', 0)}% |",
    ]
    for name, threshold in coverage.get("packages", {}).items():
        lines.append(f"| `{name}` | {threshold['line']}% | {threshold['branch']}% |")

    return "\n".join(lines)


def render_coverage(config: dict) -> str:
    report_path = REPO_ROOT / config.get("coverage", {}).get("report", "")
    measured = load_coverage(report_path)
    if measured is None:
        return (
            "No coverage report found. Run `bash scripts/library-quality.sh` and then\n"
            "`python scripts/update-docs.py` to populate this table."
        )

    packages, (overall_line, overall_branch) = measured
    thresholds = config.get("coverage", {}).get("packages", {})
    overall_threshold = config.get("coverage", {}).get("overall", {})

    def status(actual: float, required: float) -> str:
        return "pass" if actual >= required else "**FAIL**"

    def row(label: str, line_pct: float, branch_pct: float, required: dict) -> str:
        gate = f"{status(line_pct, required['line'])} / {status(branch_pct, required['branch'])}"
        return f"| {label} | {line_pct:.2f}% | {branch_pct:.2f}% | {gate} |"

    lines = [
        "Measured on the last local/CI coverage run:",
        "",
        "| Scope | Line | Branch | Gate (line / branch) |",
        "| --- | ---: | ---: | :--- |",
        row(
            "**Overall**",
            overall_line,
            overall_branch,
            {"line": overall_threshold.get("line", 0), "branch": overall_threshold.get("branch", 0)},
        ),
    ]

    for name, threshold in thresholds.items():
        actual = packages.get(name)
        if actual is None:
            lines.append(f"| `{name}` | - | - | not in report |")
            continue
        lines.append(row(f"`{name}`", actual[0], actual[1], threshold))

    return "\n".join(lines)


def render_test_suites(config: dict) -> str:
    suites = config.get("testSuites", [])
    lines = [
        "Generated from `testSuites` in [`quality-gates.json`](../quality-gates.json), which is also",
        "what the CI workflow builds its job matrix from - so a suite cannot silently drop out of CI.",
        "",
        "| Suite | Kind | Test cases | Services | In CI |",
        "| --- | --- | ---: | --- | :---: |",
    ]

    total = 0
    for suite in suites:
        count = count_test_cases(REPO_ROOT / suite["project"])
        total += count
        services = ", ".join(suite.get("services", [])) or "none"
        in_ci = "yes" if suite.get("ci") else "no"
        label = suite["name"]
        if suite.get("notes"):
            label += f"<br><sub>{suite['notes']}</sub>"
        lines.append(f"| {label} | {suite['kind']} | {count} | {services} | {in_ci} |")

    lines.append(f"| **Total** | | **{total}** | | |")
    return "\n".join(lines)


def render_badges(config: dict) -> str:
    coverage = config.get("coverage", {}).get("overall", {})
    return (
        "[![Quality Gates](https://github.com/kyroluskamal/KyrolusSous.AspNetCoreToolkit/actions/workflows/quality-gates.yml/badge.svg)]"
        "(https://github.com/kyroluskamal/KyrolusSous.AspNetCoreToolkit/actions/workflows/quality-gates.yml)\n"
        f"![Coverage gate](https://img.shields.io/badge/coverage%20gate-line%20%E2%89%A5%20{coverage.get('line', 0)}%25%20%7C%20branch%20%E2%89%A5%20{coverage.get('branch', 0)}%25-blue)"
    )


# --------------------------------------------------------------------------- #
# Block substitution
# --------------------------------------------------------------------------- #


def replace_block(text: str, block_id: str, body: str, path: Path) -> str:
    begin = BLOCK_PATTERN.format(id=block_id)
    end = END_PATTERN.format(id=block_id)

    start_index = text.find(begin)
    end_index = text.find(end)
    if start_index == -1 or end_index == -1:
        raise ValueError(f"Missing '{begin}' / '{end}' markers in {path.relative_to(REPO_ROOT)}")
    if end_index < start_index:
        raise ValueError(f"'{end}' appears before '{begin}' in {path.relative_to(REPO_ROOT)}")

    head = text[: start_index + len(begin)]
    tail = text[end_index:]
    return f"{head}\n<!-- Generated by scripts/update-docs.py - do not edit by hand. -->\n\n{body}\n\n{tail}"


def main() -> int:
    parser = argparse.ArgumentParser(description="Regenerate generated documentation blocks.")
    parser.add_argument("--check", action="store_true", help="Fail instead of writing when docs are stale.")
    parser.add_argument("--config", default=str(CONFIG_PATH), help="Path to quality-gates.json.")
    args = parser.parse_args()

    with open(args.config, encoding="utf-8") as handle:
        config = json.load(handle)

    families = config.get("docs", {}).get("projectFamilies", [])
    projects = scan_projects()

    updates = {
        README_PATH: {
            "badges": render_badges(config),
            "projects": render_projects(projects, families),
        },
        QUALITY_DOC_PATH: {
            "thresholds": render_thresholds(config),
            "test-suites": render_test_suites(config),
        },
    }

    # Measured coverage is deliberately NOT written into the docs: it moves on every run,
    # which would make --check fail on unrelated PRs. Only deterministic facts are
    # committed. The live numbers go to the CI job summary and are printed here.
    print(render_coverage(config))
    print()

    stale: List[str] = []
    for path, blocks in updates.items():
        original = path.read_text(encoding="utf-8")
        updated = original
        for block_id, body in blocks.items():
            updated = replace_block(updated, block_id, body, path)

        relative = path.relative_to(REPO_ROOT).as_posix()
        if updated == original:
            print(f"[update-docs] up to date: {relative}")
            continue

        if args.check:
            stale.append(relative)
            print(f"[update-docs] STALE: {relative}")
        else:
            path.write_text(updated, encoding="utf-8", newline="\n")
            print(f"[update-docs] updated: {relative}")

    if stale:
        print("\n[update-docs] FAILED - generated docs are out of date.")
        print("Run `python scripts/update-docs.py` locally and commit the result.")
        return 1

    print("[update-docs] OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())

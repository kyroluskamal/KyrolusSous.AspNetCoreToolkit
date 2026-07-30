#!/usr/bin/env python3
"""
Coverage gate checker for Cobertura reports.

Thresholds live in quality-gates.json (the single source of truth). The
--report/--min-line/--min-branch/--package flags override it when given, which
keeps ad-hoc local runs possible without editing the config.

Examples:
  python scripts/coverage-gate.py
  python scripts/coverage-gate.py --config quality-gates.json
  python scripts/coverage-gate.py --min-line 80 --package KyrolusSous.Swagger:50:40
"""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_CONFIG = REPO_ROOT / "quality-gates.json"


@dataclass(frozen=True)
class Threshold:
    line: float
    branch: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate Cobertura coverage against quality gates.")
    parser.add_argument(
        "--config",
        default=str(DEFAULT_CONFIG),
        help="Path to quality-gates.json. Defaults to the repo-root file.",
    )
    parser.add_argument("--report", help="Path to cobertura xml report. Overrides config.")
    parser.add_argument("--min-line", type=float, help="Minimum overall line coverage percentage. Overrides config.")
    parser.add_argument("--min-branch", type=float, help="Minimum overall branch coverage percentage. Overrides config.")
    parser.add_argument(
        "--package",
        action="append",
        default=[],
        help="Package threshold in format: PackageName:LinePercent:BranchPercent. Overrides config entry.",
    )
    return parser.parse_args()


def load_config(config_path: Path) -> dict:
    """Read quality-gates.json. A missing file is only fatal if nothing overrides it."""
    if not config_path.exists():
        return {}
    with config_path.open(encoding="utf-8") as handle:
        return json.load(handle)


def config_thresholds(config: dict) -> Dict[str, Threshold]:
    packages = config.get("coverage", {}).get("packages", {})
    thresholds: Dict[str, Threshold] = {}
    for name, values in packages.items():
        try:
            thresholds[name] = Threshold(line=float(values["line"]), branch=float(values["branch"]))
        except (KeyError, TypeError, ValueError) as exc:
            raise ValueError(f"Invalid coverage.packages entry for '{name}' in config: {values!r}") from exc
    return thresholds


def parse_package_thresholds(raw_values: List[str]) -> Dict[str, Threshold]:
    thresholds: Dict[str, Threshold] = {}
    for raw in raw_values:
        parts = raw.split(":")
        if len(parts) != 3:
            raise ValueError(
                f"Invalid --package value '{raw}'. Expected format: PackageName:LinePercent:BranchPercent"
            )

        package_name = parts[0].strip()
        if not package_name:
            raise ValueError(f"Invalid --package value '{raw}': package name is required.")

        try:
            line = float(parts[1])
            branch = float(parts[2])
        except ValueError as exc:
            raise ValueError(f"Invalid --package value '{raw}': line/branch must be numeric.") from exc

        thresholds[package_name] = Threshold(line=line, branch=branch)

    return thresholds


def as_percent(rate_attr: str | None) -> float:
    if rate_attr is None:
        return 0.0
    try:
        return float(rate_attr) * 100.0
    except ValueError:
        return 0.0


def load_report(report_path: Path) -> tuple[ET.Element, Dict[str, tuple[float, float]]]:
    if not report_path.exists():
        raise FileNotFoundError(f"Coverage report not found: {report_path}")

    root = ET.parse(report_path).getroot()
    packages: Dict[str, tuple[float, float]] = {}

    for pkg in root.findall("./packages/package"):
        name = (pkg.attrib.get("name") or "").strip()
        if not name:
            continue
        line = as_percent(pkg.attrib.get("line-rate"))
        branch = as_percent(pkg.attrib.get("branch-rate"))
        packages[name] = (line, branch)

    return root, packages


def fmt(value: float) -> str:
    return f"{value:.2f}%"


@dataclass(frozen=True)
class Settings:
    report: Path
    overall: Threshold
    packages: Dict[str, Threshold]


def resolve_settings(args: argparse.Namespace) -> Settings:
    """Merge quality-gates.json with the CLI overrides. CLI flags always win."""
    config = load_config(Path(args.config))
    coverage_cfg = config.get("coverage", {})
    overall_cfg = coverage_cfg.get("overall", {})

    packages = config_thresholds(config)
    packages.update(parse_package_thresholds(args.package))

    report_raw = args.report or coverage_cfg.get("report")
    if not report_raw:
        raise ValueError("No coverage report path given (pass --report or set coverage.report in the config).")

    report_path = Path(report_raw)
    if not report_path.is_absolute():
        report_path = REPO_ROOT / report_path

    return Settings(
        report=report_path,
        overall=Threshold(
            line=args.min_line if args.min_line is not None else float(overall_cfg.get("line", 0.0)),
            branch=args.min_branch if args.min_branch is not None else float(overall_cfg.get("branch", 0.0)),
        ),
        packages=packages,
    )


def main() -> int:
    try:
        args = parse_args()
        settings = resolve_settings(args)
        package_thresholds = settings.packages
        min_line = settings.overall.line
        min_branch = settings.overall.branch
        root, packages = load_report(settings.report)
    except Exception as exc:  # pragma: no cover - defensive CLI guard
        print(f"[coverage-gate] ERROR: {exc}")
        return 2

    overall_line = as_percent(root.attrib.get("line-rate"))
    overall_branch = as_percent(root.attrib.get("branch-rate"))

    failures: List[str] = []

    print("[coverage-gate] Overall")
    print(f"  line   : {fmt(overall_line)} (required >= {fmt(min_line)})")
    print(f"  branch : {fmt(overall_branch)} (required >= {fmt(min_branch)})")

    if overall_line < min_line:
        failures.append(
            f"Overall line coverage {fmt(overall_line)} is below required {fmt(min_line)}"
        )
    if overall_branch < min_branch:
        failures.append(
            f"Overall branch coverage {fmt(overall_branch)} is below required {fmt(min_branch)}"
        )

    if package_thresholds:
        print("[coverage-gate] Packages")
    for package_name, threshold in package_thresholds.items():
        measured = packages.get(package_name)
        if measured is None:
            failures.append(f"Package '{package_name}' was not found in report.")
            print(
                f"  {package_name}: not found (required line>={fmt(threshold.line)}, branch>={fmt(threshold.branch)})"
            )
            continue

        line, branch = measured
        print(
            f"  {package_name}: line={fmt(line)} (>= {fmt(threshold.line)}), "
            f"branch={fmt(branch)} (>= {fmt(threshold.branch)})"
        )

        if line < threshold.line:
            failures.append(
                f"Package '{package_name}' line coverage {fmt(line)} is below required {fmt(threshold.line)}"
            )
        if branch < threshold.branch:
            failures.append(
                f"Package '{package_name}' branch coverage {fmt(branch)} is below required {fmt(threshold.branch)}"
            )

    if failures:
        print("[coverage-gate] FAILED")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("[coverage-gate] PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())


#!/usr/bin/env python3
"""
Coverage gate checker for Cobertura reports.

Examples:
  python scripts/coverage-gate.py \
    --report TestResults/coverage-run/Coverage/coverage.cobertura.xml \
    --min-line 68 \
    --min-branch 64 \
    --package KyrolusSous.Repositories.EF.Runtime:95:85 \
    --package KyrolusSous.Repositories.EF.Abstractions:65:55
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List


@dataclass(frozen=True)
class Threshold:
    line: float
    branch: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate Cobertura coverage against quality gates.")
    parser.add_argument("--report", required=True, help="Path to cobertura xml report.")
    parser.add_argument("--min-line", type=float, default=0.0, help="Minimum overall line coverage percentage.")
    parser.add_argument("--min-branch", type=float, default=0.0, help="Minimum overall branch coverage percentage.")
    parser.add_argument(
        "--package",
        action="append",
        default=[],
        help="Package threshold in format: PackageName:LinePercent:BranchPercent",
    )
    return parser.parse_args()


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


def main() -> int:
    try:
        args = parse_args()
        package_thresholds = parse_package_thresholds(args.package)
        report_path = Path(args.report)
        root, packages = load_report(report_path)
    except Exception as exc:  # pragma: no cover - defensive CLI guard
        print(f"[coverage-gate] ERROR: {exc}")
        return 2

    overall_line = as_percent(root.attrib.get("line-rate"))
    overall_branch = as_percent(root.attrib.get("branch-rate"))

    failures: List[str] = []

    print("[coverage-gate] Overall")
    print(f"  line   : {fmt(overall_line)} (required >= {fmt(args.min_line)})")
    print(f"  branch : {fmt(overall_branch)} (required >= {fmt(args.min_branch)})")

    if overall_line < args.min_line:
        failures.append(
            f"Overall line coverage {fmt(overall_line)} is below required {fmt(args.min_line)}"
        )
    if overall_branch < args.min_branch:
        failures.append(
            f"Overall branch coverage {fmt(overall_branch)} is below required {fmt(args.min_branch)}"
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


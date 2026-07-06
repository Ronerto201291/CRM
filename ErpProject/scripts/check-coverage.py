#!/usr/bin/env python3
"""Coverage gate: merge cobertura XML (unit + integration) and fail if below thresholds."""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path


@dataclass
class CoverageStats:
    lines_covered: int
    lines_valid: int

    @property
    def line_rate(self) -> float:
        if self.lines_valid == 0:
            return 0.0
        return 100.0 * self.lines_covered / self.lines_valid


def parse_cobertura(path: Path) -> tuple[CoverageStats, dict[str, CoverageStats]]:
    root = ET.parse(path).getroot()
    total = CoverageStats(
        lines_covered=int(float(root.attrib.get("lines-covered", 0))),
        lines_valid=int(float(root.attrib.get("lines-valid", 0))),
    )
    packages: dict[str, CoverageStats] = {}
    for package in root.findall(".//package"):
        name = package.attrib.get("name", "")
        packages[name] = CoverageStats(
            lines_covered=int(float(package.attrib.get("line-rate", 0)) * int(package.attrib.get("lines-valid", 0) or 0))
            if "lines-valid" in package.attrib
            else 0,
            lines_valid=sum(
                1
                for cls in package.findall("classes/class")
                for line in cls.findall("lines/line")
            ),
        )
        # Recompute from line-rate attribute (cobertura stores rate, not counts per package)
        line_rate = float(package.attrib.get("line-rate", 0))
        lines_valid = sum(
            1 for cls in package.findall("classes/class") for line in cls.findall("lines/line")
        )
        lines_covered = round(line_rate * lines_valid)
        packages[name] = CoverageStats(lines_covered=lines_covered, lines_valid=lines_valid)
    return total, packages


def merge_cobertura(paths: list[Path]) -> CoverageStats:
    """Union line hits across reports (line covered if hit in any report)."""
    line_hits: dict[tuple[str, str], int] = {}

    for path in paths:
        root = ET.parse(path).getroot()
        for cls in root.findall(".//class"):
            filename = cls.attrib.get("filename", "")
            for line in cls.findall("lines/line"):
                key = (filename, line.attrib["number"])
                hits = int(line.attrib.get("hits", 0))
                line_hits[key] = max(line_hits.get(key, 0), hits)

    if not line_hits:
        return CoverageStats(0, 0)

    covered = sum(1 for h in line_hits.values() if h > 0)
    return CoverageStats(lines_covered=covered, lines_valid=len(line_hits))


def load_thresholds(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def check(name: str, actual: float, minimum: float, failures: list[str]) -> None:
    status = "PASS" if actual >= minimum else "FAIL"
    print(f"  [{status}] {name}: {actual:.2f}% (min {minimum:.2f}%)")
    if actual < minimum:
        failures.append(f"{name}: {actual:.2f}% < {minimum:.2f}%")


def main() -> int:
    parser = argparse.ArgumentParser(description="ERP coverage gate")
    parser.add_argument("--unit", required=True, help="Unit test cobertura.xml")
    parser.add_argument("--integration", required=True, help="Integration test cobertura.xml")
    parser.add_argument(
        "--thresholds",
        default=str(Path(__file__).parent / "coverage-thresholds.json"),
        help="Thresholds JSON",
    )
    args = parser.parse_args()

    unit_path = Path(args.unit)
    int_path = Path(args.integration)
    thresholds = load_thresholds(Path(args.thresholds))
    backend = thresholds["backend"]

    if not unit_path.exists():
        print(f"ERROR: unit coverage not found: {unit_path}", file=sys.stderr)
        return 1
    if not int_path.exists():
        print(f"ERROR: integration coverage not found: {int_path}", file=sys.stderr)
        return 1

    unit_total, unit_packages = parse_cobertura(unit_path)
    int_total, int_packages = parse_cobertura(int_path)
    merged = merge_cobertura([unit_path, int_path])

    failures: list[str] = []

    print("Backend coverage gate")
    print("─" * 50)
    check("Unit XPlat (line)", unit_total.line_rate, backend["unit_line_min"], failures)
    check(
        "Integration XPlat (line)",
        int_total.line_rate,
        backend["integration_line_min"],
        failures,
    )
    check("Merged unit+integration (line)", merged.line_rate, backend["merged_line_min"], failures)

    print("\nCritical modules (integration scope):")
    all_packages = {**unit_packages, **int_packages}
    for module, minimum in backend.get("modules", {}).items():
        pkg = int_packages.get(module) or unit_packages.get(module)
        if pkg and pkg.lines_valid > 0:
            check(module, pkg.line_rate, minimum, failures)
        else:
            print(f"  [SKIP] {module}: not found in reports")

    print("─" * 50)
    if failures:
        print(f"\nCOVERAGE GATE FAILED ({len(failures)} check(s)):")
        for f in failures:
            print(f"  ✗ {f}")
        print("\nDeploy blocked. Add tests or fix regressions before merging.")
        return 1

    print("\nCOVERAGE GATE PASSED — deploy may proceed.")
    print(
        f"  Merged: {merged.line_rate:.2f}% | Unit: {unit_total.line_rate:.2f}% | "
        f"Integration: {int_total.line_rate:.2f}%"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

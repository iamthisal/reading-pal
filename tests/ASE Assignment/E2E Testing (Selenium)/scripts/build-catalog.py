#!/usr/bin/env python3
"""Builds TEST-CATALOG.md: one row per test case, joining the test code, TEST-PLAN.md and the latest TRX results.

Run from this folder after the suites have run with a TRX logger (see README.md):
    python3 scripts/build-catalog.py
"""
import glob
import os
import re
import xml.etree.ElementTree as ET
from collections import OrderedDict, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SERVICES = OrderedDict([
    ("UserService.E2ETests", "User"),
    ("InventoryService.E2ETests", "Inventory"),
    ("LendingService.E2ETests", "Lending"),
    ("NotificationService.E2ETests", "Notification"),
])
TEST = re.compile(
    r'\[(Fact|Theory), Trait\("Id", "([A-Z]+-\d+)"\), Trait\("Priority", "(P\d)"\), Trait\("Type", "(Happy|Unhappy)"\)\]'
    r'(?:\s*\[InlineData\([^\]]*\)\])*\s*public (?:async Task|void) (\w+)\(')


def plan_rows():
    """ID -> (scenario, expected) from the tables in TEST-PLAN.md."""
    rows = {}
    with open(os.path.join(ROOT, "TEST-PLAN.md"), encoding="utf-8") as plan:
        for line in plan:
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if len(cells) >= 5 and re.fullmatch(r"[A-Z]+-\d+", cells[0]):
                rows[cells[0]] = (cells[3], cells[-1])
    return rows


def results():
    """Method name -> list of outcomes (a Theory has one per data row), from every TRX file."""
    outcomes = defaultdict(list)
    for path in glob.glob(os.path.join(ROOT, "*", "TestResults", "*.trx")):
        tree = ET.parse(path)
        for result in tree.iter():
            if result.tag.endswith("UnitTestResult"):
                name = result.get("testName", "")
                method = re.sub(r"\(.*\)$", "", name).split(".")[-1]
                outcomes[method].append(result.get("outcome", "NotExecuted"))
    return outcomes


def outcome_text(values):
    if not values:
        return "Not run"
    if all(value == "Passed" for value in values):
        return "Pass" if len(values) == 1 else f"Pass ({len(values)}/{len(values)})"
    failed = sum(1 for value in values if value != "Passed")
    return "**Fail**" if len(values) == 1 else f"**Fail** ({failed}/{len(values)} failed)"


def main():
    plan = plan_rows()
    outcomes = results()
    lines = [
        "# Complete test catalogue",
        "",
        "One row per test case in TEST-PLAN.md. A Theory runs once per data row; its result shows how many rows passed.",
        "Regenerate with `python3 scripts/build-catalog.py` after a run.",
        "",
    ]
    totals = defaultdict(int)
    for project, service in SERVICES.items():
        cases = []
        for path in sorted(glob.glob(os.path.join(ROOT, project, "*Tests.cs"))):
            text = open(path, encoding="utf-8").read()
            cls = os.path.splitext(os.path.basename(path))[0]
            for kind, case_id, priority, case_type, method in TEST.findall(text):
                cases.append((case_id, case_type, priority, cls, method))
        cases.sort(key=lambda case: (case[0].split("-")[0], int(case[0].split("-")[1])))
        lines += [f"## {service} Service ({project})", "",
                  "| ID | Type | Priority | Test class | Test name | Scenario | Expected result | Result |",
                  "| --- | --- | --- | --- | --- | --- | --- | --- |"]
        for case_id, case_type, priority, cls, method in cases:
            scenario, expected = plan.get(case_id, ("", ""))
            result = outcome_text(outcomes.get(method, []))
            totals[case_type] += 1
            totals["fail" if result.startswith("**Fail") else "pass" if result.startswith("Pass") else "notrun"] += 1
            lines.append(f"| {case_id} | {case_type} | {priority} | {cls} | `{method}` | {scenario} | {expected} | {result} |")
        lines.append("")
    lines[4:4] = [f"**{totals['Happy'] + totals['Unhappy']} cases: {totals['Happy']} happy, {totals['Unhappy']} unhappy. "
                  f"{totals['pass']} pass, {totals['fail']} fail, {totals['notrun']} not run.**", ""]
    with open(os.path.join(ROOT, "TEST-CATALOG.md"), "w", encoding="utf-8") as catalog:
        catalog.write("\n".join(lines))
    print("\n".join(lines[:7]))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Builds TEST-CATALOG.md: one row per test case, joining the test code, TEST-PLAN.md, every recorded run and
the notes on failures and fixes.

Record a run (reads each project's TestResults/*.trx, or console logs given with --logs), then rebuild:
    python3 scripts/build-catalog.py --record run-3 --label "Run 3" --date 2026-10-20 --frontend "develop @ abc1234"
    python3 scripts/build-catalog.py

Recorded runs live in results/*.json; notes on failures and fixes live in scripts/catalog-notes.json.
"""
import argparse
import glob
import json
import os
import re
import xml.etree.ElementTree as ET
from collections import OrderedDict, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESULTS = os.path.join(ROOT, "results")
NOTES = os.path.join(ROOT, "scripts", "catalog-notes.json")
SERVICES = OrderedDict([
    ("UserService.E2ETests", "User"),
    ("InventoryService.E2ETests", "Inventory"),
    ("LendingService.E2ETests", "Lending"),
    ("NotificationService.E2ETests", "Notification"),
])
TEST = re.compile(
    r'\[(Fact|Theory), Trait\("Id", "([A-Z]+-\d+)"\), Trait\("Priority", "(P\d)"\), Trait\("Type", "(Happy|Unhappy)"\)\]'
    r'(?:\s*\[InlineData\([^\]]*\)\])*\s*public (?:async Task|void) (\w+)\(')
LOG_LINE = re.compile(r"^\s+(Passed|Failed|Skipped) [\w.]+\.(\w+)(?:\(.*\))? \[")


def plan_rows():
    """ID -> (scenario, expected) from the tables in TEST-PLAN.md."""
    rows = {}
    with open(os.path.join(ROOT, "TEST-PLAN.md"), encoding="utf-8") as plan:
        for line in plan:
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if len(cells) >= 5 and re.fullmatch(r"[A-Z]+-\d+", cells[0]):
                rows[cells[0]] = (cells[3], cells[-1])
    return rows


def read_outcomes(logs):
    """Method name -> outcomes (a Theory has one per data row), from TRX files or console logs."""
    outcomes = defaultdict(list)
    if logs:
        for path in logs:
            for line in open(path, encoding="utf-8", errors="replace"):
                match = LOG_LINE.match(line)
                if match:
                    outcomes[match.group(2)].append(match.group(1))
        return outcomes
    for path in glob.glob(os.path.join(ROOT, "*", "TestResults", "*.trx")):
        for result in ET.parse(path).iter():
            if result.tag.endswith("UnitTestResult"):
                method = re.sub(r"\(.*\)$", "", result.get("testName", "")).split(".")[-1]
                outcomes[method].append(result.get("outcome", "NotExecuted"))
    return outcomes


def outcome_text(values):
    if not values:
        return "Not run"
    failed = sum(1 for value in values if value != "Passed")
    if failed == 0:
        return "Pass" if len(values) == 1 else f"Pass ({len(values)}/{len(values)})"
    return "**Fail**" if len(values) == 1 else f"**Fail** ({failed}/{len(values)} failed)"


def load_runs():
    runs = []
    for path in sorted(glob.glob(os.path.join(RESULTS, "*.json"))):
        with open(path, encoding="utf-8") as run:
            runs.append(json.load(run))
    return runs


def record(args):
    outcomes = read_outcomes(args.logs)
    if not outcomes:
        raise SystemExit("No results found: run the suites with a TRX logger first, or pass --logs.")
    os.makedirs(RESULTS, exist_ok=True)
    run = {"name": args.record, "label": args.label or args.record, "date": args.date, "frontend": args.frontend,
           "results": dict(sorted(outcomes.items()))}
    with open(os.path.join(RESULTS, f"{args.record}.json"), "w", encoding="utf-8") as out:
        json.dump(run, out, indent=2)
    print(f"Recorded {args.record}: {sum(len(v) for v in outcomes.values())} executed runs.")


def build():
    plan = plan_rows()
    runs = load_runs()
    notes = json.load(open(NOTES, encoding="utf-8")) if os.path.exists(NOTES) else {"history": [], "fixes": [], "cases": {}}
    cases = OrderedDict()
    for project in SERVICES:
        found = []
        for path in sorted(glob.glob(os.path.join(ROOT, project, "*Tests.cs"))):
            cls = os.path.splitext(os.path.basename(path))[0]
            for _, case_id, priority, case_type, method in TEST.findall(open(path, encoding="utf-8").read()):
                found.append((case_id, case_type, priority, cls, method))
        found.sort(key=lambda case: (case[0].split("-")[0], int(case[0].split("-")[1])))
        cases[project] = found

    every = [case for project_cases in cases.values() for case in project_cases]
    happy = sum(1 for case in every if case[1] == "Happy")
    lines = ["# Complete test catalogue", "",
             f"**{len(every)} test cases: {happy} happy paths, {len(every) - happy} unhappy paths**, in four xUnit projects. "
             "Each row is one case from TEST-PLAN.md; a Theory runs once per data row, and its result shows how many rows passed.", ""]

    lines += ["## Run history", "",
              "| Run | Date | Frontend under test | Executed | Passed | Failed | Notes |", "| --- | --- | --- | ---: | ---: | ---: | --- |"]
    history = {entry["name"]: entry.get("notes", "") for entry in notes.get("history", [])}
    for run in runs:
        values = [value for outcome in run["results"].values() for value in outcome]
        passed = sum(1 for value in values if value == "Passed")
        lines.append(f"| {run['label']} | {run.get('date') or ''} | {run.get('frontend') or ''} | {len(values)} | {passed} | "
                     f"{len(values) - passed} | {history.get(run['name'], '')} |")
    lines.append("")

    if notes.get("fixes"):
        lines += ["## Failures and fixes", "",
                  "Every failure seen while building and running the suites, why it happened, and what was changed. "
                  "**Application** means a defect in Reading Pal itself; **Test** means a problem in the test code.", "",
                  "| Case | Where | What failed | Why | What was fixed |", "| --- | --- | --- | --- | --- |"]
        for fix in notes["fixes"]:
            lines.append(f"| {fix['case']} | {fix['where']} | {fix['failed']} | {fix['why']} | {fix['fixed']} |")
        lines.append("")

    run_headers = " | ".join(run["label"] for run in runs)
    run_rule = " | ".join("---" for _ in runs)
    for project, service in SERVICES.items():
        lines += [f"## {service} Service ({project})", "",
                  f"| ID | Type | Priority | Test class | Test name | Scenario | Expected result | {run_headers} | Notes |",
                  f"| --- | --- | --- | --- | --- | --- | --- | {run_rule} | --- |"]
        for case_id, case_type, priority, cls, method in cases[project]:
            scenario, expected = plan.get(case_id, ("", ""))
            results = " | ".join(outcome_text(run["results"].get(method, [])) for run in runs)
            lines.append(f"| {case_id} | {case_type} | {priority} | {cls} | `{method}` | {scenario} | {expected} | {results} | "
                         f"{notes.get('cases', {}).get(case_id, '')} |")
        lines.append("")

    with open(os.path.join(ROOT, "TEST-CATALOG.md"), "w", encoding="utf-8") as catalog:
        catalog.write("\n".join(lines))
    print("\n".join(lines[:4 + 4 + len(runs)]))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--record", help="save the current results as results/<name>.json")
    parser.add_argument("--label", help="column heading for the recorded run")
    parser.add_argument("--date")
    parser.add_argument("--frontend", help="which frontend code the run tested, e.g. 'develop @ 812af30'")
    parser.add_argument("--logs", nargs="*", help="console logs (verbosity=normal) to read instead of TRX files")
    args = parser.parse_args()
    if args.record:
        record(args)
    build()


if __name__ == "__main__":
    main()

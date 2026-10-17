"""Finds the Unity test modes the package has tests for, so the test workflow runs only those.

A test assembly is an .asmdef that references nunit.framework.dll or is constrained to UNITY_INCLUDE_TESTS. It runs in
editmode when it is limited to the Editor platform and in playmode otherwise. A mode counts only when one of its
assemblies holds at least one test method, so a mode without tests is skipped instead of failing the workflow.

Usage: python3 detect-test-modes.py <package-root>
Prints the modes as a JSON list and writes `modes` to $GITHUB_OUTPUT when it is set.
"""

import json
import os
import re
import sys

EXCLUDED_ROOT_ENTRIES = {".git", ".github"}
MODES = ("editmode", "playmode")
TEST_ATTRIBUTE_PATTERN = re.compile(r"\[\s*(?:NUnit\.Framework\.)?(?:Test|UnityTest|TestCase|TestCaseSource)\s*[\](,]")


def fail(message):
    print(f"::error::{message}")
    sys.exit(1)


def visible_folders(root, directory, folders):
    is_root = os.path.relpath(directory, root) == "."
    return sorted(
        name for name in folders
        if not name.startswith(".") and not (is_root and name in EXCLUDED_ROOT_ENTRIES)
    )


def find_asmdefs(root):
    asmdefs = []
    for directory, folders, files in os.walk(root):
        folders[:] = visible_folders(root, directory, folders)
        asmdefs.extend(os.path.join(directory, name) for name in sorted(files) if name.endswith(".asmdef"))

    return asmdefs


def read_asmdef(path):
    try:
        with open(path, encoding="utf-8-sig") as asmdef:
            return json.load(asmdef)
    except (OSError, ValueError) as error:
        fail(f"Could not read {path}: {error}")


def is_test_assembly(asmdef):
    return ("nunit.framework.dll" in asmdef.get("precompiledReferences", [])
            or "UNITY_INCLUDE_TESTS" in asmdef.get("defineConstraints", []))


def test_mode(asmdef):
    return "editmode" if asmdef.get("includePlatforms", []) == ["Editor"] else "playmode"


def has_tests(root, assembly_directory):
    for directory, folders, files in os.walk(assembly_directory):
        folders[:] = [
            name for name in visible_folders(root, directory, folders)
            if not any(entry.endswith(".asmdef") for entry in os.listdir(os.path.join(directory, name)))
        ]
        for name in files:
            if not name.endswith(".cs"):
                continue
            with open(os.path.join(directory, name), encoding="utf-8-sig", errors="replace") as source:
                if TEST_ATTRIBUTE_PATTERN.search(source.read()):
                    return True

    return False


def main():
    if len(sys.argv) != 2:
        fail("Usage: detect-test-modes.py <package-root>")

    root = os.path.abspath(sys.argv[1])
    found = set()
    for path in find_asmdefs(root):
        asmdef = read_asmdef(path)
        if not is_test_assembly(asmdef):
            continue

        mode = test_mode(asmdef)
        tested = has_tests(root, os.path.dirname(path))
        print(f"{os.path.relpath(path, root)}: {mode}, {'has tests' if tested else 'no tests, skipped'}")
        if tested:
            found.add(mode)

    modes = json.dumps([mode for mode in MODES if mode in found])
    print(f"modes={modes}")
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as github_output:
            github_output.write(f"modes={modes}\n")


if __name__ == "__main__":
    main()

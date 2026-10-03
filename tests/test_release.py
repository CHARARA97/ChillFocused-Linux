"""Shape checks for this repository's scripts and its release artifact.

The release is one DLL and the installer has to keep it that way, keep the old
layouts cleaned up, and never install something it could not verify.  These checks
are cheap; the failures they catch are not (two copies of a mod means it runs
twice, and nothing in the game says so).
"""

import os
import shutil
import subprocess
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(REPO, "scripts")


def read(name):
    path = os.path.join(SCRIPTS, name)
    with open(path, encoding="utf-8") as handle:
        return handle.read()


class ScriptSyntaxTests(unittest.TestCase):
    def test_every_shell_script_parses(self):
        bash = shutil.which("bash")
        if bash is None:
            self.skipTest("bash is not installed")

        failures = []
        for name in sorted(os.listdir(SCRIPTS)):
            if not name.endswith(".sh"):
                continue
            result = subprocess.run(
                [bash, "-n", os.path.join(SCRIPTS, name)], capture_output=True, text=True, timeout=60
            )
            if result.returncode != 0:
                failures.append("%s: %s" % (name, result.stderr.strip()))

        self.assertEqual([], failures)


class SingleDllReleaseTests(unittest.TestCase):
    def test_the_packaging_script_exists_and_is_runnable(self):
        path = os.path.join(SCRIPTS, "package-mod.sh")

        self.assertTrue(os.path.exists(path))
        self.assertTrue(os.access(path, os.X_OK), "package-mod.sh is not executable")

    def test_packaging_produces_one_dll_and_a_checksum(self):
        script = read("package-mod.sh")

        self.assertIn("ChillFocused.dll", script)
        self.assertIn("sha256sum", script)
        # A zip would reintroduce the "unpacked one level wrong" failure.
        self.assertNotIn("zip -", script)

    def test_the_installer_installs_the_flat_file(self):
        self.assertIn("$PLUGINS/ChillFocused.dll", read("install-mod.sh"))

    def test_the_installer_cleans_up_every_older_layout(self):
        script = read("install-mod.sh")

        for stale in ('"$PLUGINS/ChillFocus"', '"$PLUGINS/ChillFocused"',
                      '"$PLUGINS/ChillFocus.dll"', "ChillFocused-old.dll"):
            self.assertIn(stale, script, stale)

    def test_the_installer_can_take_a_downloaded_dll_and_verify_it(self):
        script = read("install-mod.sh")

        self.assertIn("--dll", script)
        self.assertIn("--sha256", script)
        self.assertIn("checksum mismatch", script)

    def test_the_installer_finds_the_game_in_any_steam_library(self):
        script = read("install-mod.sh")

        self.assertIn("libraryfolders.vdf", script)
        self.assertIn("--game-dir", script)

    def test_the_artifact_is_not_older_than_its_sources(self):
        artifact = os.path.join(REPO, "dist", "mod", "ChillFocused.dll")
        if not os.path.exists(artifact):
            self.skipTest("no packaged DLL; run scripts/package-mod.sh")

        newest, newest_file = 0.0, ""
        for root, dirs, files in os.walk(os.path.join(REPO, "ChillFocused")):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
            for name in files:
                if name.endswith((".cs", ".csproj")):
                    path = os.path.join(root, name)
                    if os.path.getmtime(path) > newest:
                        newest, newest_file = os.path.getmtime(path), path

        self.assertGreaterEqual(
            os.path.getmtime(artifact), newest,
            "dist/mod/ChillFocused.dll is older than %s -- run scripts/package-mod.sh"
            % os.path.relpath(newest_file, REPO),
        )


class ModRepositoryTests(unittest.TestCase):
    def test_the_release_metadata_matches_the_project(self):
        import json
        import re

        with open(os.path.join(REPO, "packaging", "thunderstore", "manifest.json"), encoding="utf-8") as handle:
            manifest = json.load(handle)
        with open(os.path.join(REPO, "ChillFocused", "ChillFocused.csproj"), encoding="utf-8") as handle:
            project = handle.read()

        version = re.search(r"<Version>([^<]+)</Version>", project).group(1)
        self.assertEqual(version, manifest["version_number"])

    def test_the_readme_tells_users_the_two_things_they_get_wrong(self):
        with open(os.path.join(REPO, "README.md"), encoding="utf-8") as handle:
            readme = handle.read()

        self.assertIn("BepInEx 5", readme)          # not 6.0
        self.assertIn("winhttp", readme)            # the override everyone misses
        self.assertIn("两个", readme)                # do not keep two copies of the DLL


if __name__ == "__main__":
    unittest.main()

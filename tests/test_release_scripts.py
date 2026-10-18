"""Shape checks for the release scripts.

These scripts are the part of the project a user runs *first*, and a syntax error in
one of them is a wall of bash noise instead of an install.  They also encode two
decisions that are easy to undo by accident: the release is a single DLL, and the
installer cleans up the layouts it replaced.
"""

import os
import shutil
import subprocess
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(REPO, "scripts")


def read(name):
    with open(os.path.join(SCRIPTS, name), encoding="utf-8") as handle:
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
                [bash, "-n", os.path.join(SCRIPTS, name)],
                capture_output=True, text=True, timeout=60,
            )
            if result.returncode != 0:
                failures.append("%s: %s" % (name, result.stderr.strip()))

        self.assertEqual([], failures)


class SingleDllReleaseTests(unittest.TestCase):
    """The release is one file; the installer has to keep it that way."""

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
        self.assertNotIn(".zip", script)

    def test_the_installer_installs_the_flat_file(self):
        script = read("install-bepinex.sh")

        self.assertIn('$PLUGINS/ChillFocused.dll', script)

    def test_the_installer_cleans_up_every_older_layout(self):
        script = read("install-bepinex.sh")

        for stale in ('"$PLUGINS/ChillFocus"', '"$PLUGINS/ChillFocused"',
                      '"$PLUGINS/ChillFocus.dll"', 'ChillFocused-old.dll'):
            self.assertIn(stale, script, stale)

    def test_the_installer_can_take_a_downloaded_dll_and_verify_it(self):
        script = read("install-bepinex.sh")

        self.assertIn("--dll", script)
        self.assertIn("--sha256", script)
        self.assertIn("checksum mismatch", script)

    def test_the_installer_finds_the_game_in_any_steam_library(self):
        script = read("install-bepinex.sh")

        self.assertIn("libraryfolders.vdf", script)
        self.assertIn("--game-dir", script)


class ReleaseArtifactTests(unittest.TestCase):
    """The packaged DLL has to be the current code.

    This is not hypothetical: the language and DI code was written *after* the first
    packaging run, and the stale artifact was installed into the game.  Nothing
    failed -- the DLL was simply missing the new features.  Comparing timestamps and
    looking for a marker string inside the DLL catches both halves of that.
    """

    def setUp(self):
        self.dll = os.path.join(REPO, "dist", "mod", "ChillFocused.dll")
        if not os.path.exists(self.dll):
            self.skipTest("no packaged DLL; run scripts/package-mod.sh")

    def test_the_artifact_is_not_older_than_its_sources(self):
        newest = 0.0
        newest_file = ""
        for root, dirs, files in os.walk(os.path.join(REPO, "ChillFocused")):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
            for name in files:
                if not name.endswith((".cs", ".csproj")):
                    continue
                path = os.path.join(root, name)
                stamp = os.path.getmtime(path)
                if stamp > newest:
                    newest, newest_file = stamp, path

        artifact = os.path.getmtime(self.dll)
        self.assertGreaterEqual(
            artifact, newest,
            "dist/mod/ChillFocused.dll is older than %s -- run scripts/package-mod.sh"
            % os.path.relpath(newest_file, REPO),
        )

    def test_the_artifact_contains_the_current_code(self):
        # .NET stores string literals as UTF-16, so a plain ASCII grep misses them.
        with open(self.dll, "rb") as handle:
            data = handle.read()

        for marker in ("ProjectLifetimeScope", "another copy of this plugin",
                       "panel language", "Bulbul.LanguageSupplier"):
            self.assertIn(marker.encode("utf-16-le"), data,
                          "the packaged DLL predates %s" % marker)


class DoctorTests(unittest.TestCase):
    def test_the_doctor_exists_and_checks_the_whole_chain(self):
        path = os.path.join(SCRIPTS, "doctor-focused.sh")

        self.assertTrue(os.path.exists(path))
        self.assertTrue(os.access(path, os.X_OK), "doctor-focused.sh is not executable")

        script = read("doctor-focused.sh")
        for check in ("BepInEx is installed", "winhttp", "Focused is not answering",
                      "more than one copy", "the tokens differ", "LogOutput.log"):
            self.assertIn(check, script, check)

    def test_the_installer_runs_the_doctor_at_the_end(self):
        self.assertIn("doctor-focused.sh", read("install-bepinex.sh"))


if __name__ == "__main__":
    unittest.main()

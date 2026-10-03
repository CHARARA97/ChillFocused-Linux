"""The installer, run for real against a throwaway game directory.

The single-DLL change moved where the mod lives, and an upgrade that forgets the
old file leaves two copies loaded at once.  That is exactly the kind of thing that
is easy to get wrong and invisible when it is: the mod merely runs twice.

So this test builds a fake game directory that looks like an install from *before*
the change, runs the real script, and checks what is left behind.  The user's actual
game directory is never touched.
"""

import hashlib
import os
import shutil
import subprocess
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INSTALLER = os.path.join(REPO, "scripts", "install-mod.sh")
RELEASE_DLL = os.path.join(REPO, "dist", "mod", "ChillFocused.dll")


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


class InstallerLayoutTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.game = os.path.join(self._tmp.name, "Chill with You Lo-Fi Story")

        # Just enough structure for the installer's own sanity checks.
        managed = os.path.join(self.game, "Chill With You_Data", "Managed")
        os.makedirs(managed)
        open(os.path.join(managed, "Assembly-CSharp.dll"), "wb").close()
        os.makedirs(os.path.join(self.game, "BepInEx", "core"))
        open(os.path.join(self.game, "BepInEx", "core", "BepInEx.dll"), "wb").close()
        open(os.path.join(self.game, "winhttp.dll"), "wb").close()
        self.plugins = os.path.join(self.game, "BepInEx", "plugins")
        os.makedirs(self.plugins)

        # A DLL to install.  Built by scripts/package-mod.sh; built here if missing
        # so the test does not depend on a previous packaging run.
        self.release = RELEASE_DLL
        if not os.path.exists(self.release):
            result = subprocess.run(
                ["bash", os.path.join(REPO, "scripts", "package-mod.sh"), "--offline"],
                capture_output=True, text=True, timeout=900,
            )
            if not os.path.exists(self.release):
                self.skipTest("no release DLL and packaging failed: " + result.stderr[-300:])

    def install(self, *extra):
        env = dict(os.environ)
        # Keep the test hermetic: no real Proton prefix, no real Steam library.
        env["CHILLFOCUS_PFX"] = os.path.join(self._tmp.name, "pfx")
        return subprocess.run(
            ["bash", INSTALLER, "--game-dir", self.game, "--dll", self.release,
             "--no-doctor", *extra],
            capture_output=True, text=True, timeout=300, env=env,
        )

    def test_a_fresh_install_lands_in_one_flat_file(self):
        result = self.install()

        self.assertEqual(0, result.returncode, result.stderr)
        target = os.path.join(self.plugins, "ChillFocused.dll")
        self.assertTrue(os.path.exists(target))
        self.assertFalse(os.path.isdir(os.path.join(self.plugins, "ChillFocused")))
        self.assertEqual(sha256(self.release), sha256(target))

    def test_the_old_subfolder_layout_is_removed(self):
        old_dir = os.path.join(self.plugins, "ChillFocused")
        os.makedirs(old_dir)
        old_dll = os.path.join(old_dir, "ChillFocused.dll")
        open(old_dll, "wb").close()

        result = self.install()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(os.path.exists(old_dir), "the old folder is still there")
        self.assertTrue(os.path.exists(os.path.join(self.plugins, "ChillFocused.dll")))

    def test_the_pre_rename_layout_is_removed(self):
        old_dir = os.path.join(self.plugins, "ChillFocus")
        os.makedirs(old_dir)
        open(os.path.join(old_dir, "ChillFocus.dll"), "wb").close()
        open(os.path.join(self.plugins, "ChillFocus.dll"), "wb").close()

        result = self.install()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(os.path.exists(old_dir))
        self.assertFalse(os.path.exists(os.path.join(self.plugins, "ChillFocus.dll")))

    def test_exactly_one_dll_is_left_after_upgrading(self):
        # The whole point: BepInEx loads every DLL under plugins/, so an upgrade must
        # not leave a second copy behind.
        for stale in ("ChillFocus", "ChillFocused"):
            os.makedirs(os.path.join(self.plugins, stale))
            open(os.path.join(self.plugins, stale, stale + ".dll"), "wb").close()
        open(os.path.join(self.plugins, "ChillFocused-old.dll"), "wb").close()

        self.install()

        found = []
        for root, _dirs, files in os.walk(self.plugins):
            found += [f for f in files if f.endswith(".dll")]
        self.assertEqual(["ChillFocused.dll"], sorted(found))

    def test_a_wrong_checksum_is_refused(self):
        result = self.install("--sha256", "0" * 64)

        self.assertNotEqual(0, result.returncode)
        self.assertIn("checksum mismatch", result.stderr)
        self.assertFalse(os.path.exists(os.path.join(self.plugins, "ChillFocused.dll")))

    def test_a_matching_checksum_is_accepted(self):
        result = self.install("--sha256", sha256(self.release))

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(os.path.exists(os.path.join(self.plugins, "ChillFocused.dll")))

    def test_dry_run_changes_nothing(self):
        os.makedirs(os.path.join(self.plugins, "ChillFocused"))

        result = self.install("--dry-run")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(os.path.exists(os.path.join(self.plugins, "ChillFocused.dll")))
        self.assertTrue(os.path.isdir(os.path.join(self.plugins, "ChillFocused")))


if __name__ == "__main__":
    unittest.main()

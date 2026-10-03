"""The wording guard for user-visible text.

The application, the browser extension and this plugin share one vocabulary, on
purpose: two words for the same thing is how a user ends up unsure whether
"白名单" and "保护名单" are the same list.  The application settled on it in
docs/ui-design.md (the term table), and the extension has its own guard --
this is the plugin's half.

Two rules:
  * retired words must not appear anywhere a player can read them: not in the
    READMEs, not in the docs, not in the strings the panel and the config screen
    show;
  * the shared words must actually be there, so a rewrite cannot quietly drop
    them either.

Cheap to run, and it fails on the thing that is otherwise invisible: wording.
"""

import os
import re
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

#: Left over from earlier designs.  "冻结模式" was replaced by 屏蔽模式 because the
#: application and the extension call the two lists 屏蔽名单 / 保护名单; "演练模式" was
#: replaced by plain "只记录，不冻结"; "守护进程" is an implementation detail the player
#: does not need.
RETIRED_CHINESE = [
    "黑名单模式", "白名单模式", "黑名单", "白名单",
    "演练模式", "冻结模式", "守护进程", "命令行子串",
]

#: The English equivalents, checked in the English README only (code identifiers are
#: ASCII and legitimately still say "blacklist").
#: "freeze mode" is deliberately absent: the English interface does say "Freeze mode",
#: and the retired term is the Chinese one (冻结模式 -> 屏蔽模式).
RETIRED_ENGLISH = ["rehearsal mode", "blacklist", "whitelist", "daemon"]

#: What the shared vocabulary looks like in each language.
REQUIRED_CHINESE = ["屏蔽名单", "保护名单", "只记录，不冻结"]
REQUIRED_ENGLISH = ["block list", "protect list", "record only"]


def read(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


#: This test file serves two layouts: the published repository (the plugin projects
#: at the root) and the working tree it is generated from (plugin/ChillFocused/).
#: Resolving both keeps one test file instead of two that drift apart.
PLUGIN_DOCS = ["docs/protocol.md", "docs/awesome-entry.md", "docs/publishing.md"]


def first_existing(*candidates):
    for candidate in candidates:
        if os.path.exists(candidate):
            return candidate
    return candidates[0]


def in_split_layout():
    """The published repository has the plugin project at its root."""
    return os.path.isdir(os.path.join(REPO, "ChillFocused"))


def readme(name):
    # The working tree has a README of its own (the project overview), which is a
    # different document: pick by layout, not by what happens to exist.
    if in_split_layout():
        return os.path.join(REPO, name)
    return os.path.join(REPO, "plugin", "ChillFocused", name)


def markdown_files():
    files = [readme("README.md"), readme("README.en.md")]
    for relative in PLUGIN_DOCS:
        # The plugin's own docs sit in docs/ in the published repository and in the
        # working tree's docs/ (which also holds the backend's, so it is by name).
        path = first_existing(os.path.join(REPO, relative), os.path.join(REPO, relative))
        if os.path.exists(path):
            files.append(path)
    return files


def source_root():
    return first_existing(
        os.path.join(REPO, "ChillFocused"),
        os.path.join(REPO, "plugin", "ChillFocused"),
    )


def source_files():
    found = []
    for directory, _dirs, files in os.walk(source_root()):
        found += [os.path.join(directory, name) for name in files if name.endswith(".cs")]
    return sorted(found)


def without_term_table(text):
    """The term table names the retired words on purpose -- that is its job."""
    for heading in ("## 术语对照", "## Vocabulary"):
        start = text.find(heading)
        if start < 0:
            continue
        end = text.find("\n## ", start + 1)
        text = text[:start] + (text[end:] if end > 0 else "")
    return text

class RetiredWordingTests(unittest.TestCase):
    def test_no_retired_word_in_the_markdown(self):
        hits = []
        for relative in markdown_files():
            text = without_term_table(read(relative))
            for word in RETIRED_CHINESE:
                if word in text:
                    hits.append("%s says %s" % (os.path.basename(relative), word))
            if relative.endswith(".en.md"):
                lowered = without_term_table(read(relative)).lower()
                for word in RETIRED_ENGLISH:
                    if word in lowered:
                        hits.append("%s says %s" % (os.path.basename(relative), word))

        self.assertEqual([], hits, "retired wording is still visible: %s" % hits)

    def test_no_retired_word_in_the_code_the_player_can_read(self):
        # String literals and comments: both end up in front of a user (a config
        # screen shows the descriptions, and the fallback strings show when a text
        # table is absent).
        hits = []
        for path in source_files():
            text = read(path)
            for word in RETIRED_CHINESE:
                if word in text:
                    relative = os.path.relpath(path, REPO)
                    line = next(
                        (number for number, content in enumerate(text.splitlines(), 1) if word in content),
                        0,
                    )
                    hits.append("%s:%d says %s" % (relative, line, word))

        self.assertEqual([], hits, "retired wording in the plugin: %s" % hits)


class SharedVocabularyTests(unittest.TestCase):
    def test_the_chinese_readme_uses_the_shared_words(self):
        text = read(readme("README.md"))

        for word in REQUIRED_CHINESE:
            self.assertIn(word, text, "README.md lost the word %s" % word)

    def test_the_english_readme_uses_the_shared_words(self):
        text = read(readme("README.en.md")).lower()

        for word in REQUIRED_ENGLISH:
            self.assertIn(word, text, "README.en.md lost the phrase %s" % word)

    def test_the_term_table_is_in_the_readme(self):
        # docs/ui-design.md asks for it: the table is what stops the wording from
        # drifting back to implementation words.
        self.assertIn("术语对照", read(readme("README.md")))

    def test_the_panel_text_uses_the_shared_words(self):
        text = read(os.path.join(source_root(), "Core", "PanelText.cs"))
        chinese = re.findall(r'Chinese = "((?:[^"\\]|\\.)*)"', text)

        self.assertGreater(len(chinese), 60, "the text table looks truncated")
        joined = "\n".join(chinese)
        for word in ("屏蔽模式", "保护模式", "只记录，不冻结"):
            self.assertIn(word, joined, "the panel text lost %s" % word)


if __name__ == "__main__":
    unittest.main()

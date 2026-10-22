import hashlib
import pathlib
import subprocess
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
PINNED = "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7"
PATHS = (
    "third_party/iphreeqc-source/database/minteq.v4.dat",
    "unity/Assets/StreamingAssets/science/minteq.v4.dat",
)


class ScientificPackageBytesTests(unittest.TestCase):
    def test_native_and_player_database_bytes_match_pinned_identity(self):
        for path in PATHS:
            with self.subTest(path=path):
                actual = hashlib.sha256((ROOT / path).read_bytes()).hexdigest().upper()
                self.assertEqual(actual, PINNED)

    def test_database_checkout_is_not_platform_newline_converted(self):
        for path in PATHS:
            with self.subTest(path=path):
                attr = subprocess.check_output(
                    ["git", "check-attr", "text", "--", path], cwd=ROOT, text=True
                ).strip()
                self.assertEqual(attr, f"{path}: text: unset")


if __name__ == "__main__":
    unittest.main()

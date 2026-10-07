"""Exercise script routing with fake tools; never touch an installed input method."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]

class MacScripts(unittest.TestCase):
    def test_build_only_does_not_install_or_start(self):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            (work / 'build-cli.sh').write_text((ROOT / 'mac/build-cli.sh').read_text())
            build = work / 'build.sh'
            build.write_text('#!/bin/bash\necho "$*" >> "$MELTYPE_TEST_LOG"\n')
            build.chmod(0o755)
            for tool in ('dotnet', 'swift'):
                executable = work / tool
                executable.write_text('#!/bin/bash\nexit 0\n')
                executable.chmod(0o755)
            log = work / 'calls'
            subprocess.run(['bash', str(work / 'build-cli.sh'), '--build'], check=True,
                           env={**os.environ, 'PATH': str(work) + ':' + os.environ['PATH'], 'MELTYPE_TEST_LOG': str(log)})
            self.assertEqual('--no-install\n', log.read_text())

    def test_update_runs_checks_then_starts_without_open(self):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            app = work / 'Meltype.app'
            binary = app / 'Contents/MacOS/Meltype'
            binary.parent.mkdir(parents=True)
            binary.write_text('#!/bin/bash\necho "app:$*" >> "$MELTYPE_TEST_LOG"\n')
            binary.chmod(0o755)
            script = (ROOT / 'mac/update.sh').read_text().replace('$HOME/Library/Input Methods/Meltype.app', str(app))
            (work / 'update.sh').write_text(script)
            for name, body in {
                'build-cli.sh': 'echo "build:$MELTYPE_SKIP_START:$*" >> "$MELTYPE_TEST_LOG"',
                'uname': 'echo Darwin',
                'swift': 'echo select >> "$MELTYPE_TEST_LOG"',
                'open': 'echo UNEXPECTED_OPEN >> "$MELTYPE_TEST_LOG"; exit 1',
            }.items():
                executable = work / name
                executable.write_text('#!/bin/bash\n' + body + '\n')
                executable.chmod(0o755)
            log = work / 'calls'
            subprocess.run(['bash', str(work / 'update.sh')], check=True,
                           env={**os.environ, 'PATH': str(work) + ':' + os.environ['PATH'], 'MELTYPE_TEST_LOG': str(log)})
            lines = log.read_text().splitlines()
            self.assertEqual('build:1:--test --install', lines[0])
            self.assertTrue(lines[1].startswith('app:--check-inputs '))
            self.assertIn('app:', lines)
            self.assertIn('app:--register-input-source', lines)
            self.assertIn('select', lines)
            self.assertNotIn('UNEXPECTED_OPEN', lines)

if __name__ == '__main__':
    unittest.main()

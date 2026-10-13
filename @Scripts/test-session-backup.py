import argparse
import importlib.util
import json
from pathlib import Path
import sqlite3
import tarfile
import tempfile
import unittest
from contextlib import closing

spec = importlib.util.spec_from_file_location('backup', Path(__file__).with_name('session-backup.py'))
backup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(backup)


class UnifiedBackupTests(unittest.TestCase):
    def test_both_providers_wal_auth_and_tamper(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            claude, codex = root / 'claude', root / 'codex'
            (claude / 'projects').mkdir(parents=True)
            (claude / 'projects' / 'conversation.jsonl').write_text('claude transcript\n')
            (claude / '.credentials.json').write_text('secret')
            (codex / 'sessions').mkdir(parents=True)
            (codex / 'sessions' / 'rollout.jsonl').write_text('codex transcript\n')
            (codex / 'archived_sessions').mkdir()
            (codex / 'archived_sessions' / 'old.jsonl').write_text('archived\n')
            (codex / 'auth.json').write_text('secret')
            connection = sqlite3.connect(codex / 'state_5.sqlite')
            try:
                connection.execute('PRAGMA journal_mode=WAL')
                connection.execute('CREATE TABLE threads (name TEXT)')
                connection.execute("INSERT INTO threads VALUES ('live WAL thread')")
                connection.commit()
                args = argparse.Namespace(claude_home=claude, codex_home=codex,
                    output=root / 'out', format='tar', include_backups=False, include_auth=False)
                backup.backup(args)
                archive_path = next(args.output.glob('*.tar.gz'))
                with tarfile.open(archive_path) as archive:
                    names = archive.getnames()
                    self.assertIn('.claude/projects/conversation.jsonl', names)
                    self.assertIn('.codex/sessions/rollout.jsonl', names)
                    self.assertIn('.codex/archived_sessions/old.jsonl', names)
                    self.assertNotIn('.codex/auth.json', names)
                    self.assertNotIn('.claude/.credentials.json', names)
                    self.assertNotIn('.codex/state_5.sqlite-wal', names)
                    archive.extractall(root / 'extracted', filter='data')
                with closing(sqlite3.connect(root / 'extracted/.codex/state_5.sqlite')) as snapshot:
                    self.assertEqual(snapshot.execute('SELECT name FROM threads').fetchone()[0], 'live WAL thread')
                backup.verify(root / 'extracted')
                (root / 'extracted/.codex/sessions/rollout.jsonl').write_text('corrupted')
                with self.assertRaisesRegex(ValueError, 'Hash mismatch'):
                    backup.verify(root / 'extracted')
                args.format = 'copy'
                args.output = root / 'copy-out'
                backup.backup(args)
            finally:
                connection.close()

    def test_missing_provider_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            args = argparse.Namespace(claude_home=root, codex_home=root / 'missing', output=root / 'out')
            with self.assertRaises(FileNotFoundError):
                backup.backup(args)
            self.assertFalse(args.output.exists())

    def test_external_sqlite_home(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, external = root / 'codex', root / 'external'
            source.mkdir()
            external.mkdir()
            (source / 'config.toml').write_text('sqlite_home = ' + json.dumps(str(external)))
            with closing(sqlite3.connect(external / 'codex-dev.db')) as db:
                db.execute('CREATE TABLE threads (id TEXT)')
            backup.stage_codex(source, root / 'stage')
            self.assertTrue((root / 'stage/sqlite/codex-dev.db').is_file())


if __name__ == '__main__':
    unittest.main()

"""Restore missing Codex chats without replacing active databases.

Codex (GPT-6), 2026-10-16. Keeps original transcript bytes and history offsets;
maps only the restored thread metadata to this PC's existing project.
"""
import argparse
import hashlib
import json
from pathlib import Path, PureWindowsPath
import shutil
import sqlite3
import sys
from datetime import datetime


def connect_read(path):
    """Read a database without creating it when the path is wrong."""
    conn = sqlite3.connect(path.as_uri() + '?mode=ro', uri=True)
    conn.row_factory = sqlite3.Row
    return conn


def sha(path):
    """Verify restored files against the original archive manifest."""
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def insert_rows(conn, schema, table, rows):
    """Insert explicit rows, refusing schema changes or duplicate keys."""
    if not rows:
        return
    cols = list(rows[0])
    available = {r[1] for r in conn.execute(f'PRAGMA {schema}.table_info("{table}")')}
    if not set(cols) <= available:
        raise RuntimeError('Incompatible schema: ' + table)
    names = ','.join('"' + c + '"' for c in cols)
    marks = ','.join('?' for _ in cols)
    conn.executemany(f'INSERT INTO {schema}."{table}" ({names}) VALUES ({marks})',
                     [[r[c] for c in cols] for r in rows])


def main():
    """Prepare a collision-free plan, then snapshot and merge on --apply."""
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    stage = Path(r'C:\Users\catkl\AppData\Local\Temp\codex_restore_1016_hs12b1lr')
    src = stage / '.codex'
    dest = Path.home() / '.codex'
    project = Path(__file__).resolve().parents[1]
    project_id = '01a1447a-2695-77c2-8dee-101f93631d44'
    manifest = json.loads((stage / 'MANIFEST.json').read_text(encoding='utf-8'))
    hashes = {r['path']: r['hash'] for r in manifest['files']}
    source = connect_read(src / 'state_5.sqlite')
    current = connect_read(dest / 'state_5.sqlite')
    threads = [dict(r) for r in source.execute('SELECT * FROM threads')]
    ids = {r['id'] for r in threads}
    existing = {r[0] for r in current.execute('SELECT id FROM threads')}
    if ids & existing:
        raise RuntimeError('Existing thread collision; refusing overwrite')
    if not current.execute('SELECT 1 FROM projects WHERE id=?', (project_id,)).fetchone():
        raise RuntimeError('Destination project missing')
    files = []
    for row in threads:
        old = PureWindowsPath(row['rollout_path'])
        parts = old.parts
        relative = Path(*parts[parts.index('.codex') + 1:])
        source_file, target = src / relative, dest / relative
        expected = hashes['.codex/' + relative.as_posix()]
        if sha(source_file) != expected or target.exists():
            raise RuntimeError('Transcript mismatch or destination collision')
        files.append((source_file, target, expected))
        row['rollout_path'] = str(target)
        row['cwd'] = str(project)
        row['project_id'] = project_id
    history = connect_read(src / 'thread_history_1.sqlite')
    tables = ['thread_turns', 'thread_items', 'thread_history_projection_state', 'thread_realtime_items']
    history_rows = {t: [dict(r) for r in history.execute(f'SELECT * FROM {t}') if r['thread_id'] in ids] for t in tables}
    related = {t: [dict(r) for r in source.execute(f'SELECT * FROM {t}') if r['thread_id'] in ids]
               for t in ['thread_dynamic_tools', 'thread_attachments', 'guardian_review_feedback', 'thread_read_receipts']}
    plan = {'threads': len(threads), 'archived': sum(r['archived'] for r in threads),
            'project': str(project), 'history_rows': {t: len(r) for t, r in history_rows.items()},
            'ids': sorted(ids)}
    print(json.dumps(plan, ensure_ascii=False), flush=True)
    current.close()
    if not args.apply:
        return
    safety = project.parent / 'LHS' / ('CodexRestoreSafety_' + datetime.now().strftime('%Y%m%d_%H%M%S'))
    safety.mkdir(parents=True, exist_ok=False)
    for name in ['state_5.sqlite', 'thread_history_1.sqlite']:
        with connect_read(dest / name) as reader, sqlite3.connect(safety / name) as writer:
            reader.backup(writer)
            if writer.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise RuntimeError('Safety snapshot failed')
    for name in ['session_index.jsonl', '.codex-global-state.json']:
        shutil.copy2(dest / name, safety / name)
    for original, target, expected in files:
        target.parent.mkdir(parents=True, exist_ok=True)
        with original.open('rb') as reader, target.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        if sha(target) != expected:
            raise RuntimeError('Restored transcript hash mismatch')
    conn = sqlite3.connect(dest / 'state_5.sqlite', timeout=30)
    conn.execute('PRAGMA foreign_keys=ON')
    conn.execute('ATTACH DATABASE ? AS history', (str(dest / 'thread_history_1.sqlite'),))
    with conn:
        conn.execute('BEGIN IMMEDIATE')
        insert_rows(conn, 'main', 'threads', threads)
        for table, rows in related.items():
            insert_rows(conn, 'main', table, rows)
        for table, rows in history_rows.items():
            insert_rows(conn, 'history', table, rows)
    for schema in ['main', 'history']:
        if conn.execute(f'PRAGMA {schema}.integrity_check').fetchone()[0] != 'ok':
            raise RuntimeError('Post-restore integrity check failed')
    marks = ','.join('?' for _ in ids)
    for table, rows in history_rows.items():
        count = conn.execute(f'SELECT count(*) FROM history.{table} WHERE thread_id IN ({marks})', tuple(ids)).fetchone()[0]
        if count != len(rows):
            raise RuntimeError('History count mismatch: ' + table)
    conn.close()
    plan.update(safety_backup=str(safety), restored=True, ai='Codex (GPT-6)', date='2026-10-16')
    (safety / 'restore-report.json').write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(plan, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()

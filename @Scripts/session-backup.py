"""Claude + Codex backup, with online SQLite snapshots and SHA256 verification."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import tarfile
import tempfile
import time
import tomllib
from datetime import datetime
from contextlib import closing


def digest(stream):
    h = hashlib.sha256()
    while chunk := stream.read(1024 * 1024):
        h.update(chunk)
    return h.hexdigest()


def snapshot_file(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    # A running session may append continuously: copy only the initial byte count.
    with src.open('rb') as reader, dst.open('wb') as writer:
        remaining = os.fstat(reader.fileno()).st_size
        while remaining:
            chunk = reader.read(min(1024 * 1024, remaining))
            if not chunk:
                raise RuntimeError(f'Source truncated during backup: {src}')
            writer.write(chunk)
            remaining -= len(chunk)


def copy_tree(src, dst, excluded=()):
    dst.mkdir(parents=True, exist_ok=True)
    for item in src.iterdir():
        if item.name in excluded or item.is_symlink() or item.is_junction():
            continue
        if item.is_dir():
            copy_tree(item, dst / item.name, excluded)
        elif item.is_file():
            snapshot_file(item, dst / item.name)


def snapshot_database(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    deadline = time.monotonic() + 60
    def progress(*_):
        if time.monotonic() > deadline:
            raise TimeoutError(f'SQLite snapshot timed out: {source.name}')
    with closing(sqlite3.connect(source.resolve().as_uri() + '?mode=ro', uri=True)) as src:
        with closing(sqlite3.connect(target)) as dst:
            src.backup(dst, pages=256, progress=progress)
            if dst.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise RuntimeError(f'SQLite integrity failure: {source.name}')


def stage_codex(source, target):
    target.mkdir(parents=True)
    # Allowlist: do not copy login tokens, sandbox secrets, browsers or caches.
    for name in ('sessions', 'archived_sessions', 'memories', 'history.jsonl',
                 'session_index.jsonl', '.codex-global-state.json',
                 'external_agent_session_imports.json'):
        item = source / name
        if item.is_dir():
            copy_tree(item, target / name)
        elif item.is_file():
            snapshot_file(item, target / name)
    for db in sorted(source.glob('*.sqlite')):
        snapshot_database(db, target / db.name)
    sqlite_root = source / 'sqlite'
    config = source / 'config.toml'
    if config.exists():
        settings = tomllib.loads(config.read_text(encoding='utf-8-sig'))
        if settings.get('sqlite_home'):
            sqlite_root = Path(settings['sqlite_home']).expanduser()
            if not sqlite_root.is_absolute():
                raise ValueError('sqlite_home must be an absolute path')
    if sqlite_root.is_dir():
        for db in sorted(sqlite_root.rglob('*')):
            if db.is_file() and db.suffix in ('.db', '.sqlite'):
                snapshot_database(db, target / 'sqlite' / db.relative_to(sqlite_root))


def make_manifest(root):
    files = []
    for item in sorted(root.rglob('*')):
        if item.is_file() and item != root / 'MANIFEST.json':
            with item.open('rb') as stream:
                files.append(dict(path=item.relative_to(root).as_posix(),
                                  size=item.stat().st_size, hash=digest(stream)))
    manifest = dict(version=2, providers=['claude', 'codex'],
                    timestamp=datetime.now().astimezone().isoformat(), files=files)
    (root / 'MANIFEST.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    return manifest


def verify(path):
    if path.suffix == '.7z':
        with tempfile.TemporaryDirectory(prefix='ai_verify_') as tmp:
            subprocess.run(['7z', 'x', str(path), '-o' + tmp], check=True, stdout=subprocess.DEVNULL)
            return verify(Path(tmp))
    archive = None
    if path.is_dir():
        manifest = json.loads((path / 'MANIFEST.json').read_text(encoding='utf-8-sig'))
        def read(name):
            item = (path / name).resolve()
            if not item.is_relative_to(path.resolve()):
                raise ValueError('Unsafe manifest path')
            return item.open('rb')
    else:
        archive = tarfile.open(path, 'r:gz')
        manifest = json.load(archive.extractfile('MANIFEST.json'))
        def read(name):
            return archive.extractfile(name)
    try:
        for provider in ('.claude/', '.codex/'):
            if not any(f['path'].startswith(provider) for f in manifest['files']):
                raise ValueError(f'Provider data missing: {provider}')
        for item in manifest['files']:
            with read(item['path']) as stream:
                if digest(stream) != item['hash']:
                    raise ValueError(f"Hash mismatch: {item['path']}")
        print(f"Verified: {len(manifest['files'])} files (Claude + Codex)", flush=True)
    finally:
        if archive:
            archive.close()


def backup(args):
    claude = args.claude_home
    codex = args.codex_home
    for source in (claude, codex):
        if not source.is_dir():
            raise FileNotFoundError(f'AI home missing: {source}')
    args.output.mkdir(parents=True, exist_ok=True)
    name = 'ai_sessions_backup_' + datetime.now().strftime('%Y-%m-%d_%H-%M-%S')
    # Only TemporaryDirectory owns and removes its generated temporary paths.
    with tempfile.TemporaryDirectory(prefix='ai_sessions_') as tmp:
        stage = Path(tmp)
        excluded = {'MANIFEST.json'}
        if not args.include_backups:
            excluded.add('backups')
        if not args.include_auth:
            excluded.update(('auth.json', '.credentials.json', 'credentials.json'))
        print('Staging Claude sessions...', flush=True)
        copy_tree(claude, stage / '.claude', excluded)
        print('Staging GPT(Codex) sessions and SQLite snapshots...', flush=True)
        stage_codex(codex, stage / '.codex')
        manifest = make_manifest(stage)
        if args.format == 'copy':
            result = args.output / name
            shutil.copytree(stage, result)
        elif args.format == '7z':
            result = args.output / (name + '.7z')
            if result.exists():
                raise FileExistsError(result)
            subprocess.run(['7z', 'a', str(result), '.claude', '.codex', 'MANIFEST.json'],
                           cwd=stage, check=True, stdout=subprocess.DEVNULL)
        else:
            result = args.output / (name + '.tar.gz')
            with result.open('xb') as writer, tarfile.open(fileobj=writer, mode='w:gz') as archive:
                for item in stage.iterdir():
                    archive.add(item, arcname=item.name)
        verify(result)
        counts = {p: sum(f['path'].startswith('.' + p + '/') for f in manifest['files'])
                  for p in ('claude', 'codex')}
        print(json.dumps(dict(file=str(result), files=counts,
                              size_mb=round(sum(f['size'] for f in manifest['files']) / 1048576, 2)
                              if result.is_dir() else round(result.stat().st_size / 1048576, 2)),
                         ensure_ascii=False), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='action', required=True)
    b = sub.add_parser('backup')
    b.add_argument('--output', type=Path, required=True)
    b.add_argument('--format', choices=['tar', '7z', 'copy'], default='tar')
    b.add_argument('--claude-home', type=Path, default=Path.home() / '.claude')
    b.add_argument('--codex-home', type=Path, default=Path(os.environ.get('CODEX_HOME') or Path.home() / '.codex'))
    b.add_argument('--include-backups', action='store_true')
    b.add_argument('--include-auth', action='store_true')
    v = sub.add_parser('verify')
    v.add_argument('path', type=Path)
    args = parser.parse_args()
    if args.action == 'backup':
        backup(args)
    else:
        verify(args.path)

function Get-SessionPython {
    $candidates = @()
    if ($env:SESSION_BACKUP_PYTHON) { $candidates += $env:SESSION_BACKUP_PYTHON }
    $candidates += "$env:USERPROFILE\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
    foreach ($name in @('python', 'python3')) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command -and $command.Source -notlike '*WindowsApps*') { $candidates += $command.Source }
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            & $candidate -c 'import sys; sys.exit(0 if sys.version_info >= (3,12) else 1)' 2>$null
            if ($LASTEXITCODE -eq 0) { return $candidate }
        }
    }
    throw 'Python 3.12+ required. Set SESSION_BACKUP_PYTHON to its executable path.'
}

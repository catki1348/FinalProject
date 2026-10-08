$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot 'raw_identity'
$files = @(
    @{ Name = 'run_3'; Url = 'https://3d.varco.ai/api/objects/73d892b2582a017c92e1f7eb356bdf7c.png' }
    @{ Name = 'attack_4'; Url = 'https://3d.varco.ai/api/objects/4e5d2954378406d23a70d8313da0c456.png' }
    @{ Name = 'attack_6'; Url = 'https://3d.varco.ai/api/objects/7f7026575e7fb02e7ff4f8ec62d13647.png' }
)
foreach ($file in $files) { Invoke-WebRequest -Uri $file.Url -OutFile (Join-Path $target ($file.Name + '.png')) }


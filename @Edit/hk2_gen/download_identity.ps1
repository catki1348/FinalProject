$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot 'raw_identity'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$files = @(
    @{ Name = 'run_1'; Url = 'https://3d.varco.ai/api/objects/621226b60a1b1b19c534abd42e972b1e.png' }
    @{ Name = 'run_2'; Url = 'https://3d.varco.ai/api/objects/206798f01a9f0e4321ef8cdefd92a3ea.png' }
    @{ Name = 'run_3'; Url = 'https://3d.varco.ai/api/objects/df09655070ccd365d683ffa896eccb48.png' }
    @{ Name = 'run_4'; Url = 'https://3d.varco.ai/api/objects/96c43dcd48e416ed3175fa4720a4bc34.png' }
    @{ Name = 'run_5'; Url = 'https://3d.varco.ai/api/objects/2e9d8a44d28b109b47f2a2c01d75b066.png' }
    @{ Name = 'run_6'; Url = 'https://3d.varco.ai/api/objects/b8f669f586d1121dddf5a95ba3dfc627.png' }
    @{ Name = 'run_7'; Url = 'https://3d.varco.ai/api/objects/c7b79d563241d4a0cdcdd538d26d6fd0.png' }
    @{ Name = 'run_8'; Url = 'https://3d.varco.ai/api/objects/3f07ca9eb067d62f31d4b48be4a83512.png' }
    @{ Name = 'attack_1'; Url = 'https://3d.varco.ai/api/objects/9fc06b8c097a6793943d740f1aa41f19.png' }
    @{ Name = 'attack_2'; Url = 'https://3d.varco.ai/api/objects/15f7b048860b1456156e703fb4a0470e.png' }
    @{ Name = 'attack_3'; Url = 'https://3d.varco.ai/api/objects/c6f4dfeaa89900999c402fc39168ef70.png' }
    @{ Name = 'attack_4'; Url = 'https://3d.varco.ai/api/objects/b5558472d6c3f79910d16aa8ab58c7e7.png' }
    @{ Name = 'attack_5'; Url = 'https://3d.varco.ai/api/objects/fb30ae82c09870c393d3f71171a563ef.png' }
    @{ Name = 'attack_6'; Url = 'https://3d.varco.ai/api/objects/ddd684a60d44567176bad46520c99a8e.png' }
    @{ Name = 'dash_1'; Url = 'https://3d.varco.ai/api/objects/816b30a8b0abfea0d43e50c5fcf6356f.png' }
)
foreach ($file in $files) {
    Invoke-WebRequest -Uri $file.Url -OutFile (Join-Path $target ($file.Name + '.png'))
}
Write-Output ('Downloaded ' + $files.Count + ' workflow outputs.')


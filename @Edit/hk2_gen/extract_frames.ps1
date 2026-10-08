param(
    [Parameter(Mandatory=$true)][string]$SheetPath,
    [Parameter(Mandatory=$true)][string]$Name,
    [int]$FrameSize = 140,
    [int]$Scale = 4
)
# 스프라이트 시트를 프레임 단위로 잘라 (1) 흰 배경 위 확대 포즈 레퍼런스 PNG와 (2) 프레임별 캐릭터 바운딩 박스(원본 140px 좌표)를 출력한다
Add-Type -AssemblyName System.Drawing

$outDir = 'C:\Users\MBC-501-16\Desktop\FinalProject\@Edit\hk2_gen\pose'
$sheet = New-Object System.Drawing.Bitmap $SheetPath
$count = [int]($sheet.Width / $FrameSize)
$lines = @()

for ($i = 0; $i -lt $count; $i++) {
    $minX = $FrameSize; $minY = $FrameSize; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $FrameSize; $y++) {
        for ($x = 0; $x -lt $FrameSize; $x++) {
            if ($sheet.GetPixel($i * $FrameSize + $x, $y).A -gt 20) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $lines += "$i,$minX,$minY,$maxX,$maxY"

    $size = $FrameSize * $Scale
    $out = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $src = New-Object System.Drawing.Rectangle(($i * $FrameSize), 0, $FrameSize, $FrameSize)
    $dst = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $g.DrawImage($sheet, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $out.Save((Join-Path $outDir ("{0}_{1}.png" -f $Name, ($i + 1))), [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
}
$sheet.Dispose()
$lines | Set-Content -Encoding utf8 (Join-Path $outDir ("{0}_bbox.csv" -f $Name))
Write-Output ("{0}: {1} frames" -f $Name, $count)
$lines

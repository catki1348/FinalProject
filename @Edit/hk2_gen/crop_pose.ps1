param(
    [Parameter(Mandatory=$true)][string]$SheetPath,
    [Parameter(Mandatory=$true)][string]$Name,
    [int]$FrameSize = 140,
    [int]$OutSize = 512
)
# 각 프레임에서 "모든 프레임의 바운딩 박스를 합친 영역"을 동일하게 잘라 확대한다.
# 프레임마다 같은 영역을 쓰므로 레퍼런스끼리 위치/크기 관계(달리기 시 위아래 흔들림 등)가 그대로 보존된다.
Add-Type -AssemblyName System.Drawing

$outDir = 'C:\Users\MBC-501-16\Desktop\FinalProject\@Edit\hk2_gen\pose'
$rows = Get-Content (Join-Path $outDir ("{0}_bbox.csv" -f $Name)) | ForEach-Object { ,($_.Split(',') | ForEach-Object { [int]$_ }) }
$minX = ($rows | ForEach-Object { $_[1] } | Measure-Object -Minimum).Minimum
$minY = ($rows | ForEach-Object { $_[2] } | Measure-Object -Minimum).Minimum
$maxX = ($rows | ForEach-Object { $_[3] } | Measure-Object -Maximum).Maximum
$maxY = ($rows | ForEach-Object { $_[4] } | Measure-Object -Maximum).Maximum

$pad = 6
$side = [Math]::Max($maxX - $minX, $maxY - $minY) + 1 + $pad * 2
$cx = [int](($minX + $maxX) / 2); $cy = [int](($minY + $maxY) / 2)
$left = $cx - [int]($side / 2); $top = $cy - [int]($side / 2)

$sheet = New-Object System.Drawing.Bitmap $SheetPath
for ($i = 0; $i -lt $rows.Count; $i++) {
    $out = New-Object System.Drawing.Bitmap($OutSize, $OutSize)
    $g = [System.Drawing.Graphics]::FromImage($out)
    $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $src = New-Object System.Drawing.Rectangle(($i * $FrameSize + $left), $top, $side, $side)
    $dst = New-Object System.Drawing.Rectangle(0, 0, $OutSize, $OutSize)
    $g.DrawImage($sheet, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $out.Save((Join-Path $outDir ("{0}_crop_{1}.png" -f $Name, ($i + 1))), [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
}
$sheet.Dispose()
Write-Output ("crop region (140px space): left={0} top={1} side={2}" -f $left, $top, $side)

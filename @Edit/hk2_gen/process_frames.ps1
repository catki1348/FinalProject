param(
    [Parameter(Mandatory=$true)][string]$InDir,
    [Parameter(Mandatory=$true)][string]$OutDir,
    [Parameter(Mandatory=$true)][string]$Prefix,
    [int]$Tolerance = 45,
    [int]$MinComponent = 400,
    [int]$Canvas = 1024,
    [int]$GroundY = 980,
    [int]$TargetBodyH = 560,
    [switch]$KeepX
)
# 흰 배경 생성 이미지를 (1) 테두리에서 시작하는 flood-fill로 배경 제거, (2) 작은 잔여 덩어리 제거,
# (3) 프레임끼리 흔들리지 않도록 "발 끝(최하단) = GroundY", "망토(빨강)를 뺀 몸통 무게중심 X = 캔버스 중앙"으로 정렬해
# 동일 크기 캔버스에 다시 배치한다. 결과는 투명 PNG.
$src = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class FrameProc
{
    public static string Process(string inPath, string outPath, int tol, int minComp, int canvas, int groundY, int targetBodyH, bool keepX)
    {
        Bitmap bmp = new Bitmap(inPath);
        int w = bmp.Width, h = bmp.Height;
        Bitmap src32 = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(src32)) g.DrawImage(bmp, 0, 0, w, h);
        bmp.Dispose();

        BitmapData d = src32.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        byte[] px = new byte[w * h * 4];
        Marshal.Copy(d.Scan0, px, 0, px.Length);

        // 1) 테두리와 연결된 "흰색에 가까운" 픽셀을 배경으로 판정
        bool[] bg = new bool[w * h];
        Queue<int> q = new Queue<int>();
        Func<int, bool> nearWhite = i => {
            int b = px[i * 4], gg = px[i * 4 + 1], r = px[i * 4 + 2];
            int dist = Math.Max(255 - r, Math.Max(255 - gg, 255 - b));
            return dist < tol;
        };
        for (int x = 0; x < w; x++) { Seed(x, 0, w, bg, q, nearWhite); Seed(x, h - 1, w, bg, q, nearWhite); }
        for (int y = 0; y < h; y++) { Seed(0, y, w, bg, q, nearWhite); Seed(w - 1, y, w, bg, q, nearWhite); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = i % w, y = i / w;
            if (x > 0) Seed(x - 1, y, w, bg, q, nearWhite);
            if (x < w - 1) Seed(x + 1, y, w, bg, q, nearWhite);
            if (y > 0) Seed(x, y - 1, w, bg, q, nearWhite);
            if (y < h - 1) Seed(x, y + 1, w, bg, q, nearWhite);
        }

        // 2) 남은 전경 중 작은 덩어리(잡티) 제거
        int[] label = new int[w * h];
        int cur = 0;
        for (int i = 0; i < w * h; i++)
        {
            if (bg[i] || label[i] != 0) continue;
            cur++;
            List<int> comp = new List<int>();
            Queue<int> cq = new Queue<int>(); cq.Enqueue(i); label[i] = cur;
            while (cq.Count > 0)
            {
                int k = cq.Dequeue(); comp.Add(k); int x = k % w, y = k / w;
                int[] nb = { x > 0 ? k - 1 : -1, x < w - 1 ? k + 1 : -1, y > 0 ? k - w : -1, y < h - 1 ? k + w : -1 };
                foreach (int n in nb) if (n >= 0 && !bg[n] && label[n] == 0) { label[n] = cur; cq.Enqueue(n); }
            }
            if (comp.Count < minComp) foreach (int k in comp) bg[k] = true;
        }

        // 3) 바운딩 박스 / 최하단 / 몸통(빨강 제외) 무게중심
        int minX = w, minY = h, maxX = -1, maxY = -1, bodyTop = h; long sx = 0, cnt = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (bg[i]) { px[i * 4 + 3] = 0; continue; }
                if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                int b = px[i * 4], gg = px[i * 4 + 1], r = px[i * 4 + 2];
                bool red = r > 120 && gg < 90 && b < 90;
                if (!red) { sx += x; cnt++; if (y < bodyTop) bodyTop = y; }
            }
        Marshal.Copy(px, 0, d.Scan0, px.Length);
        src32.UnlockBits(d);
        if (maxX < 0) { src32.Dispose(); return "EMPTY"; }
        int cx = cnt > 0 ? (int)(sx / cnt) : (minX + maxX) / 2;

        // 4) 몸 높이(망토 제외, 투구~발끝)를 targetBodyH로 맞추는 배율을 구한 뒤 공통 캔버스에 정렬 배치
        int bodyH = Math.Max(1, maxY - bodyTop);
        float s = targetBodyH > 0 ? (float)targetBodyH / bodyH : 1f;
        Bitmap outBmp = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(outBmp))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            // keepX: 무기가 몸 밖으로 크게 뻗는 동작(공격 등)은 무게중심이 흔들리므로 원본 가로 위치를 그대로 유지
            float dx = keepX ? (canvas - w * s) / 2f : canvas / 2f - cx * s, dy = groundY - maxY * s;
            g.DrawImage(src32, new RectangleF(dx, dy, w * s, h * s), new RectangleF(0, 0, w, h), GraphicsUnit.Pixel);
        }
        outBmp.Save(outPath, ImageFormat.Png);
        outBmp.Dispose(); src32.Dispose();
        return string.Format("bbox=({0},{1})-({2},{3}) bodyH={4} scale={5:0.000} torsoX={6}", minX, minY, maxX, maxY, bodyH, s, cx);
    }

    static void Seed(int x, int y, int w, bool[] bg, Queue<int> q, Func<int, bool> nearWhite)
    {
        int i = y * w + x;
        if (bg[i] || !nearWhite(i)) return;
        bg[i] = true; q.Enqueue(i);
    }
}
"@
if (-not ('FrameProc' -as [type])) { Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing }
New-Item -ItemType Directory -Force $OutDir | Out-Null
Get-ChildItem $InDir -Filter "$Prefix*.png" | Sort-Object Name | ForEach-Object {
    $out = Join-Path $OutDir $_.Name
    $r = [FrameProc]::Process($_.FullName, $out, $Tolerance, $MinComponent, $Canvas, $GroundY, $TargetBodyH, [bool]$KeepX)
    Write-Output ("{0}: {1}" -f $_.Name, $r)
}

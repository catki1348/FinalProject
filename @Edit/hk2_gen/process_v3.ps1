param([Parameter(Mandatory=$true)][string]$InDir,[Parameter(Mandatory=$true)][string]$OutDir,[switch]$KeepExistingAlpha)
# 픽셀 그림은 재샘플링하지 않고 배경만 제거한다.
# 검이 올라간 위치와 무관하게 몸통 기준 높이를 측정해 Unity 표시 크기/발 피벗을 기록한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$src = @"
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public sealed class SpriteMeasureV3 {
 public string Name;
 public int Width,Height,BottomY,BodyTop;
 public float BodyHeight,PivotX,PivotY,PixelsPerUnit;
}
public static class SpritePrepV3 {
 public static SpriteMeasureV3 Process(string path,string output,bool removeBackground) {
  using(var input=new Bitmap(path)) using(var bmp=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb)){
   int w=bmp.Width,h=bmp.Height;
   using(var g=Graphics.FromImage(bmp))g.DrawImageUnscaled(input,0,0);
   var data=bmp.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   byte[] px=new byte[w*h*4]; Marshal.Copy(data.Scan0,px,0,px.Length);
   bool[] bg=new bool[w*h]; for(int i=0;i<bg.Length;i++)bg[i]=px[i*4+3]<4;
   if(removeBackground){
    var q=new Queue<int>();
    Func<int,bool> white=i=>Math.Max(255-px[i*4],Math.Max(255-px[i*4+1],255-px[i*4+2]))<45;
    Action<int> seed=i=>{if(i<0||i>=bg.Length||bg[i]||!white(i))return;bg[i]=true;q.Enqueue(i);};
    for(int x=0;x<w;x++){seed(x);seed((h-1)*w+x);}
    for(int y=0;y<h;y++){seed(y*w);seed(y*w+w-1);}
    while(q.Count>0){int i=q.Dequeue(),x=i%w,y=i/w;if(x>0)seed(i-1);if(x<w-1)seed(i+1);if(y>0)seed(i-w);if(y<h-1)seed(i+w);}
   }
   int[] row=new int[h];int bottom=-1,minY=h;
   for(int y=0;y<h;y++)for(int x=0;x<w;x++){
    int i=y*w+x;if(bg[i])px[i*4+3]=0;
    if(px[i*4+3]<40)continue;row[y]++;bottom=y;minY=Math.Min(minY,y);
   }
   if(bottom<0)throw new Exception("Empty sprite: "+path);
   int top=minY,window=60,required=(int)(w*0.12f);
   for(int y=minY;y<bottom-window;y++){int full=0;for(int t=y;t<y+window;t++)if(row[t]>=required)full++;if(full>=45){top=y;break;}}
   float bodyH=bottom-top; long sx=0,count=0;
   int bandStart=top+(int)(bodyH*0.20f),bandEnd=top+(int)(bodyH*0.52f);
   for(int y=bandStart;y<bandEnd;y++)for(int x=0;x<w;x++){
    int i=(y*w+x)*4; if(px[i+3]<40)continue;
    int b=px[i],g=px[i+1],r=px[i+2];
    if(r>110&&r>g*1.4&&r>b*1.4)continue;
    sx+=x;count++;
   }
   float cx=count>0?(float)sx/count:w*0.5f;
   Marshal.Copy(px,0,data.Scan0,px.Length);bmp.UnlockBits(data);
   if(!String.IsNullOrEmpty(output))bmp.Save(output,ImageFormat.Png);
   return new SpriteMeasureV3{Name=Path.GetFileName(path),Width=w,Height=h,BottomY=bottom,BodyTop=top,BodyHeight=bodyH,PivotX=cx/w,PivotY=(h-1-bottom)/(float)h};
  }
 }
}
"@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing
$reference = [SpritePrepV3]::Process((Join-Path $PSScriptRoot '../../Assets/Sprites/Warrior/Transparent/idle_right.png'),$null,$false)
$bodyWorld = $reference.BodyHeight / 100.0
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$items=@()
Get-ChildItem -LiteralPath $InDir -Filter '*.png' | Sort-Object Name | ForEach-Object {
 # 이미 워크플로우 마스크를 적용한 이미지의 밝은 검 궤적은 배경으로 다시 제거하지 않는다.
 $m=[SpritePrepV3]::Process($_.FullName,(Join-Path $OutDir $_.Name),(-not $KeepExistingAlpha))
 $m.PixelsPerUnit=$m.BodyHeight/$bodyWorld
 $items+=$m
 Write-Output ($m.Name+': PPU='+[Math]::Round($m.PixelsPerUnit,2)+' pivot='+[Math]::Round($m.PivotX,3)+','+[Math]::Round($m.PivotY,3))
}
$items | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutDir 'import_settings.json') -Encoding UTF8

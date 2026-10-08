param([string]$SourceDir='Assets/Sprites/Warrior/HeroKnightTest',[string]$OutDir='@Edit/hk2_gen/fringe_final',[switch]$KeepLargestComponent)
# 흰 바탕에서 분리된 PNG의 검은 외곽선 바깥에 붙은 얇은 중성색 매트만 지운다.
# 몸 안쪽의 금속 하이라이트와 넓은 베기 궤적은 투명 경계/어두운 선 조건을 만족하지 않는다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;using System.Collections.Generic;
public static class FringeCleanup {
 public static int Apply(string source,string output,bool keepLargest){
  using(var input=new Bitmap(source))using(var b=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(b))g.DrawImageUnscaled(input,0,0);
   int w=b.Width,h=b.Height;var data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   byte[] p=new byte[data.Stride*h];Marshal.Copy(data.Scan0,p,0,p.Length);int removed=0;
   // ponytail: 최대 3px의 중성색 매트만 처리. 더 넓은 유색 오염은 원본 마스크를 다시 수정해야 한다.
   for(int pass=0;pass<3;pass++){
    var erase=new List<int>();
    for(int y=3;y<h-3;y++)for(int x=3;x<w-3;x++){
     int i=y*data.Stride+x*4;if(p[i+3]==0)continue;
     int lo=Math.Min(p[i],Math.Min(p[i+1],p[i+2])),hi=Math.Max(p[i],Math.Max(p[i+1],p[i+2]));
     if(lo<75||hi-lo>28)continue;
     bool edge=false;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(p[(y+dy)*data.Stride+(x+dx)*4+3]==0)edge=true;
     if(!edge)continue;
     int dark=0;
     for(int dy=-3;dy<=3;dy++)for(int dx=-3;dx<=3;dx++){
      int j=(y+dy)*data.Stride+(x+dx)*4;
      int light=(p[j]+p[j+1]+p[j+2])/3;
      if(p[j+3]>=240&&light<65&&lo-light>35)dark++;
     }
     if(dark>=2)erase.Add(i);
    }
    foreach(int i in erase){p[i]=p[i+1]=p[i+2]=p[i+3]=0;}removed+=erase.Count;
   }
   // 이펙트가 없는 단일 캐릭터에만 사용: 떨어진 배경 선/점은 최대 연결 실루엣에 포함되지 않는다.
   if(keepLargest){
    int[] labels=new int[w*h],queue=new int[w*h];int label=0,best=0,bestSize=0;
    for(int start=0;start<labels.Length;start++){
     if(labels[start]!=0||p[(start/w)*data.Stride+(start%w)*4+3]==0)continue;
     label++;int head=0,tail=0;queue[tail++]=start;labels[start]=label;
     while(head<tail){int k=queue[head++],x=k%w,y=k/w;
      for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
       int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int n=ny*w+nx;
       if(labels[n]==0&&p[ny*data.Stride+nx*4+3]>0){labels[n]=label;queue[tail++]=n;}
      }
     }
     if(tail>bestSize){bestSize=tail;best=label;}
    }
    for(int k=0;k<labels.Length;k++)if(labels[k]!=0&&labels[k]!=best){int i=(k/w)*data.Stride+(k%w)*4;p[i]=p[i+1]=p[i+2]=p[i+3]=0;removed++;}
   }
   Marshal.Copy(p,0,data.Scan0,p.Length);b.UnlockBits(data);b.Save(output,ImageFormat.Png);return removed;
  }
 }
}
"@
New-Item -ItemType Directory -Force $OutDir | Out-Null
$reports=@(Get-ChildItem -LiteralPath $SourceDir -Filter '*.png' | ForEach-Object {
 $output=Join-Path $OutDir $_.Name
 $count=[FringeCleanup]::Apply($_.FullName,[IO.Path]::GetFullPath($output),[bool]$KeepLargestComponent)
 [PSCustomObject]@{Name=$_.Name;RemovedFringePixels=$count}
})
$reports | ConvertTo-Json | Set-Content (Join-Path $OutDir 'report.json') -Encoding utf8
$reports

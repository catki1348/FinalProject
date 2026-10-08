param([Parameter(Mandatory=$true)][string]$InDir,[Parameter(Mandatory=$true)][string]$OutPrefix)
# 원본 파일을 수정하지 않고 알파 채널, 실루엣 경계, 분리된 잔여물을 검사한다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;using System.Collections.Generic;
public class AlphaReport {
 public string Name;public int Width,Height,MinX,MinY,MaxX,MaxY,Opaque,Transparent,Partial,BorderNonZero,Components,SmallComponents,NearWhite;
 public static AlphaReport Inspect(string path){using(var b=new Bitmap(path)){
 var r=new AlphaReport{Name=System.IO.Path.GetFileName(path),Width=b.Width,Height=b.Height,MinX=b.Width,MinY=b.Height};int w=b.Width,h=b.Height;
 var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);byte[] p=new byte[d.Stride*h];Marshal.Copy(d.Scan0,p,0,p.Length);b.UnlockBits(d);
 bool[] mask=new bool[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*d.Stride+x*4,a=p[i+3];if(a==0){r.Transparent++;continue;}if(a==255)r.Opaque++;else r.Partial++;if(x==0||y==0||x==w-1||y==h-1)r.BorderNonZero++;if(a<40)continue;mask[y*w+x]=true;r.MinX=Math.Min(r.MinX,x);r.MinY=Math.Min(r.MinY,y);r.MaxX=Math.Max(r.MaxX,x);r.MaxY=Math.Max(r.MaxY,y);if(p[i]>235&&p[i+1]>235&&p[i+2]>235)r.NearWhite++;}
 int[] q=new int[w*h];for(int s=0;s<mask.Length;s++){if(!mask[s])continue;r.Components++;int head=0,tail=0;q[tail++]=s;mask[s]=false;while(head<tail){int k=q[head++],x=k%w,y=k/w;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int n=ny*w+nx;if(mask[n]){mask[n]=false;q[tail++]=n;}}}if(tail<20)r.SmallComponents++;}return r;
 }}
}
"@
$files=@(Get-ChildItem -LiteralPath $InDir -Filter '*.png' | Sort-Object Name)
$reports=@($files | ForEach-Object {[AlphaReport]::Inspect($_.FullName)})
$reports | ConvertTo-Json | Set-Content ($OutPrefix+'.json')
$cols=5;$rows=[Math]::Ceiling($files.Count/$cols);$canvas=New-Object Drawing.Bitmap ($cols*320),([int]$rows*500)
$g=[Drawing.Graphics]::FromImage($canvas);$g.Clear([Drawing.Color]::FromArgb(31,66,80));$font=New-Object Drawing.Font 'Arial',13
for($i=0;$i -lt $files.Count;$i++){
 $x=($i%$cols)*320;$y=[Math]::Floor($i/$cols)*500
 # 이 검수 이미지는 어두운 바탕으로 흰 테두리와 배경 잔여물을 드러낸다.
 $im=[Drawing.Image]::FromFile($files[$i].FullName);$ratio=[Math]::Min(300.0/$im.Width,460.0/$im.Height);$ww=[int]($im.Width*$ratio);$hh=[int]($im.Height*$ratio)
 $g.DrawImage($im,[Drawing.Rectangle]::new([int]($x+(320-$ww)/2),[int]($y+35),$ww,$hh));$g.DrawString($files[$i].BaseName,$font,[Drawing.Brushes]::White,[single]($x+8),[single]($y+5));$im.Dispose()
}
$canvas.Save(($OutPrefix+'.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$canvas.Dispose();$reports | ConvertTo-Json -Compress

param([string]$SourceDir='@Edit/hk2_gen/v3_final',[string]$MaskDir='@Edit/hk2_gen/alpha_masks',[string]$OutDir='@Edit/hk2_gen/alpha_final',[switch]$ClearWhiteExterior)
# 워크플로우 마스크는 배경 위치만 알려준다. 원본의 RGB/캔버스/피벗은 유지하고 배경에 해당하는 밝은 중성색 픽셀의 알파만 0으로 만든다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;using System.Drawing;using System.Drawing.Imaging;using System.Runtime.InteropServices;using System.Collections.Generic;
public class ApplyWorkflowMask {
 public static int Apply(string source,string mask,string output,bool clearWhiteExterior){using(var input=new Bitmap(source))using(var s=new Bitmap(input.Width,input.Height,PixelFormat.Format32bppArgb))using(var m=new Bitmap(mask)){
 // 불투명 RGB 원본도 알파를 저장할 수 있도록 RGBA 캔버스로 먼저 복사한다.
 using(var graphics=Graphics.FromImage(s))graphics.DrawImageUnscaled(input,0,0);
 if(s.Size!=m.Size)throw new Exception("Mask dimensions differ: "+source);
 int w=s.Width,h=s.Height;var sd=s.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);var md=m.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
 byte[] p=new byte[sd.Stride*h],q=new byte[md.Stride*h];Marshal.Copy(sd.Scan0,p,0,p.Length);Marshal.Copy(md.Scan0,q,0,q.Length);int removed=0;
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*sd.Stride+x*4,j=y*md.Stride+x*4;int min=Math.Min(p[i],Math.Min(p[i+1],p[i+2])),max=Math.Max(p[i],Math.Max(p[i+1],p[i+2]));int v=(q[j]+q[j+1]+q[j+2])/3;
 if(p[i+3]>0&&v<64&&min>200&&max-min<25){p[i+3]=0;removed++;}}
 // 흰 바탕 출력에서는 외곽과 연결된 거의 순백색 영역을 추가로 지운다.
 // 마스크가 원본에 없는 흰 궤적을 덧붙여도 투명 배경에 남지 않으며, 유색 궤적은 보존한다.
 if(clearWhiteExterior){bool[] visited=new bool[w*h];var pending=new Queue<int>();
 Action<int> seed=k=>{if(visited[k])return;int i=(k/w)*sd.Stride+(k%w)*4;if(p[i+3]>0&&Math.Min(p[i],Math.Min(p[i+1],p[i+2]))<244)return;visited[k]=true;pending.Enqueue(k);if(p[i+3]>0){p[i+3]=0;removed++;}};
 for(int x=0;x<w;x++){seed(x);seed((h-1)*w+x);}for(int y=0;y<h;y++){seed(y*w);seed(y*w+w-1);}
 while(pending.Count>0){int k=pending.Dequeue(),x=k%w,y=k/w;if(x>0)seed(k-1);if(x<w-1)seed(k+1);if(y>0)seed(k-w);if(y<h-1)seed(k+w);}}
 // 배경 제거 후 분리된 작은 중성색 조각만 지운다. 큰 장비나 유색 장식은 보존한다.
 bool[] seen=new bool[w*h];int[] queue=new int[w*h];
 for(int start=0;start<seen.Length;start++){
 int si=(start/w)*sd.Stride+(start%w)*4;if(seen[start]||p[si+3]<40)continue;
 int head=0,tail=0,bg=0;long light=0;bool neutral=true;seen[start]=true;queue[tail++]=start;
 while(head<tail){int k=queue[head++],x=k%w,y=k/w,i=y*sd.Stride+x*4,j=y*md.Stride+x*4;int lo=Math.Min(p[i],Math.Min(p[i+1],p[i+2])),hi=Math.Max(p[i],Math.Max(p[i+1],p[i+2]));if(hi-lo>25)neutral=false;light+=(p[i]+p[i+1]+p[i+2])/3;if((q[j]+q[j+1]+q[j+2])/3<64)bg++;
 for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int n=ny*w+nx,ni=ny*sd.Stride+nx*4;if(!seen[n]&&p[ni+3]>=40){seen[n]=true;queue[tail++]=n;}}}
 if(tail<150&&neutral&&light/tail>120&&(bg>tail*0.8||(clearWhiteExterior&&tail<32))){for(int k=0;k<tail;k++){int n=queue[k];p[(n/w)*sd.Stride+(n%w)*4+3]=0;}removed+=tail;}
 }
 // 완전히 투명한 배경의 숨은 체크무늬 RGB도 비운다. 보이는 캐릭터 픽셀의 색상은 그대로 유지한다.
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*sd.Stride+x*4;if(p[i+3]==0){p[i]=0;p[i+1]=0;p[i+2]=0;}}
 Marshal.Copy(p,0,sd.Scan0,p.Length);s.UnlockBits(sd);m.UnlockBits(md);s.Save(output,ImageFormat.Png);return removed;
 }}
}
"@
New-Item -ItemType Directory -Force $OutDir | Out-Null
Get-ChildItem -LiteralPath $MaskDir -Filter '*.png' | ForEach-Object {
 $source=Join-Path $SourceDir $_.Name;$output=Join-Path $OutDir $_.Name
 $n=[ApplyWorkflowMask]::Apply([IO.Path]::GetFullPath($source),$_.FullName,[IO.Path]::GetFullPath($output),[bool]$ClearWhiteExterior);Write-Output ($_.Name+': removed background pixels '+$n)
}




# climb_5에 생성 모델이 추가한 지면 선만 워크플로우 마스크 밖에서 제거한다.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;using System.Drawing;using System.Drawing.Imaging;
public static class TrimSlopeLine {
 public static void Run(string source,string mask,string output){
  using(var s=new Bitmap(source))using(var m=new Bitmap(mask)){
   if(s.Size!=m.Size)throw new Exception("Mask size mismatch");
   for(int y=(int)(s.Height*.65);y<s.Height;y++)for(int x=0;x<s.Width;x++){
    var c=s.GetPixel(x,y);if(c.A==0||m.GetPixel(x,y).R>=64)continue;
    bool near=false;for(int dy=-2;dy<=2&&!near;dy++)for(int dx=-2;dx<=2;dx++){
     int nx=x+dx,ny=y+dy;if(nx>=0&&ny>=0&&nx<m.Width&&ny<m.Height&&m.GetPixel(nx,ny).R>=64){near=true;break;}
    }
    if(!near)s.SetPixel(x,y,Color.Transparent);
   }
   s.Save(output,ImageFormat.Png);
  }
 }
}
"@
[TrimSlopeLine]::Run([IO.Path]::GetFullPath('@Edit/hk2_gen/slope_alpha/climb_5.png'),[IO.Path]::GetFullPath('@Edit/hk2_gen/slope_masks/climb_5.png'),[IO.Path]::GetFullPath('@Edit/hk2_gen/slope_alpha/climb_5_trimmed.png'))
Copy-Item @Edit/hk2_gen/slope_alpha/climb_5_trimmed.png @Edit/hk2_gen/slope_alpha/climb_5.png
Remove-Item -LiteralPath @Edit/hk2_gen/slope_alpha/climb_5_trimmed.png

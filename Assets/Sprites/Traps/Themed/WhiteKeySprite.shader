Shader "Traps/WhiteKeySprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _WhiteCutoff("White background cutoff", Range(0,1)) = .97
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _WhiteCutoff;
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            // SpriteRenderer의 도형 좌표와 예고 점멸 색을 전달한다.
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            // 원본 흰 배경만 투명 처리하며 외관과 SpriteRenderer 색을 유지한다.
            fixed4 frag(output i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv); clip(_WhiteCutoff-min(c.r,min(c.g,c.b))); return c*i.color; }
            ENDCG
        }
    }
}

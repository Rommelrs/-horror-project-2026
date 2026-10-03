// Unlit road shader (replaces Particles/Standard Unlit on the road materials).
// base atlas (unique UVs, ~2.7 texels/m) x world-space asphalt grit (fine, ~64 texels/m) x a large-scale grit pass that breaks up tiling.
// Fine grit fades with camera distance so it never shimmers. Built-in RP, fog supported.
Shader "Custom/RoadAsphaltDetail"
{
    Properties
    {
        _MainTex ("Base (road atlas)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DetailTex ("Asphalt grit (grey, tileable, 0.5 = neutral)", 2D) = "grey" {}
        _DetailTile ("Fine grit tile size (m)", Float) = 4
        _DetailAmount ("Fine grit amount", Range(0,1)) = 0.8
        _DetailFadeStart ("Fine grit fade start (m)", Float) = 35
        _DetailFadeEnd ("Fine grit fade end (m)", Float) = 110
        _MacroTile ("Large grit tile size (m)", Float) = 23
        _MacroAmount ("Large grit amount", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _DetailTex;
            fixed4 _Color;
            float _DetailTile, _DetailAmount, _DetailFadeStart, _DetailFadeEnd, _MacroTile, _MacroAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float dist : TEXCOORD2; fixed4 color : COLOR; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wpos = w;
                o.dist = length(_WorldSpaceCameraPos - w);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color * i.color;
                float fine = tex2D(_DetailTex, i.wpos.xz / _DetailTile).r;
                float macro = tex2D(_DetailTex, i.wpos.xz / _MacroTile + float2(0.37, 0.61)).r;
                float fade = 1.0 - saturate((i.dist - _DetailFadeStart) / max(1.0, _DetailFadeEnd - _DetailFadeStart));
                float mulFine = lerp(1.0, fine * 2.0, _DetailAmount * fade);
                float mulMacro = lerp(1.0, macro * 2.0, _MacroAmount);
                c.rgb *= mulFine * mulMacro;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return fixed4(c.rgb, 1);
            }
            ENDCG
        }
    }
}

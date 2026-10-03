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
        _CrackTex ("Crack network (white = intact, tileable)", 2D) = "white" {}
        _CrackTile ("Crack tile size (m)", Float) = 9
        _CrackAmount ("Crack strength where uv2.x = 1", Range(0,1)) = 0.9
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
            sampler2D _CrackTex; float _CrackTile, _CrackAmount;
            fixed4 _Color;
            float _DetailTile, _DetailAmount, _DetailFadeStart, _DetailFadeEnd, _MacroTile, _MacroAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float dist : TEXCOORD2; float crack : TEXCOORD3; fixed4 color : COLOR; UNITY_FOG_COORDS(4) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wpos = w;
                o.dist = length(_WorldSpaceCameraPos - w);
                o.color = v.color;
                o.crack = v.uv2.x;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color * i.color;
                float fine = tex2D(_DetailTex, i.wpos.xz / _DetailTile).r;
                float macro = tex2D(_DetailTex, i.wpos.xz / _MacroTile + float2(0.37, 0.61)).r;
                float fade = 1.0 - saturate((i.dist - _DetailFadeStart) / max(1.0, _DetailFadeEnd - _DetailFadeStart));
                float gm = i.color.a;                       // vertex-colour alpha masks the grit (0 on cut faces / rubble)
                float mulFine = lerp(1.0, fine * 2.0, _DetailAmount * fade * gm);
                float mulMacro = lerp(1.0, macro * 2.0, _MacroAmount * gm);
                c.rgb *= mulFine * mulMacro;
                // cracks: world-space network, strongest on the broken slabs (uv2.x = 1) and fading out along the road
                float ck = tex2D(_CrackTex, i.wpos.xz / _CrackTile).r;
                c.rgb *= lerp(1.0, ck, saturate(i.crack) * _CrackAmount);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return fixed4(c.rgb, 1);
            }
            ENDCG
        }
    }
    // gives the shader a ShadowCaster pass so it is written into the camera depth texture; without it Enviro's depth-based
    // fog image effect treats the road as not there and fogs it like the distant background (road looks see-through)
    Fallback "Legacy Shaders/VertexLit"
}

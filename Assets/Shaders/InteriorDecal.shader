// Alpha-blended floor/wall decal for closed interiors that use Custom/PSXInterior (the park toilet room): lit the same way (room lights
// only, no sun, fixed room ambient) so it sits in the surface instead of glowing. A wetness mask adds a soft highlight from the room's
// lights so wet muck reads as wet. A first pass multiplies the surface underneath darker by the wetness map (a soaked floor keeps its
// own detail, it just gets darker), then the muck colour is blended on top. Drawn right after opaque geometry (Geometry+450) with a
// depth offset, so it never z-fights.
Shader "Custom/InteriorDecal"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Decal (RGB colour, A coverage)", 2D) = "white" {}
        _WetMap ("Wetness (R)", 2D) = "black" {}
        _WetDarken ("Soaked surface brightness (multiply)", Range(0,1)) = 0.5
        _WetShine ("Wet highlight strength", Range(0,2)) = 0.6
        _WetTightness ("Wet highlight tightness", Range(4,128)) = 40
        _Opacity ("Opacity", Range(0,1)) = 1
        _InteriorAmbient ("Room ambient (match the room's materials)", Color) = (0.34, 0.30, 0.24, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry+450" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        ZWrite Off
        Offset -1, -1

        // soak: darken whatever is underneath by the wetness
        Pass
        {
            Blend DstColor Zero
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _WetMap; float4 _MainTex_ST; half _WetDarken, _Opacity;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                half w = tex2D(_WetMap, i.uv).r * _Opacity;
                return fixed4(lerp(1.0.xxx, _WetDarken.xxx, w), 1);
            }
            ENDCG
        }

        CGPROGRAM
        #pragma surface surf InteriorWet alpha:fade noambient nolightmap nodynlightmap nodirlightmap noshadow
        #pragma target 3.0

        sampler2D _MainTex, _WetMap;
        fixed4 _Color, _InteriorAmbient;
        half _WetShine, _WetTightness, _Opacity;

        struct Input { float2 uv_MainTex; };

        // room lights only (directional w == 0 is ignored), Lambert + a Blinn highlight scaled by wetness (stored in Gloss)
        half4 LightingInteriorWet (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half nl = saturate(dot(s.Normal, lightDir));
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), _WetTightness) * s.Gloss * _WetShine;
            half4 c;
            c.rgb = (s.Albedo * nl + spec) * _LightColor0.rgb * atten * _WorldSpaceLightPos0.w;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Gloss = tex2D(_WetMap, IN.uv_MainTex).r;
            o.Specular = 1;
            o.Emission = c.rgb * _InteriorAmbient.rgb;
            o.Alpha = c.a * _Opacity;
        }
        ENDCG
    }
    FallBack Off
}

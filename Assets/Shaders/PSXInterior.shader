// Surface for closed interiors that live away from the world (the park toilet room under the map). Same inputs as Custom/PSXAOLit
// (albedo + normal on UV0, baked AO on UV1) but lit only by the room's own point/spot lights: the sun (directional light) is ignored,
// and the outdoor sky ambient is replaced by a fixed _InteriorAmbient colour, so the room is not lit by daylight it can't see.
Shader "Custom/PSXInterior"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo (UV0)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map (UV0)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _AOMap ("Baked AO (UV1)", 2D) = "white" {}
        _AOStrength ("AO strength", Range(0,1)) = 0.9
        _InteriorAmbient ("Room ambient (replaces sky light)", Color) = (0.16, 0.14, 0.12, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Interior noambient nolightmap nodynlightmap nodirlightmap fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _AOMap, _BumpMap;
        fixed4 _Color, _InteriorAmbient, _EmissionColor;
        half _AOStrength, _BumpScale;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_AOMap;
        };

        // Lambert, but directional lights (w == 0, i.e. the Enviro sun) contribute nothing
        half4 LightingInterior (SurfaceOutput s, half3 lightDir, half atten)
        {
            half nl = saturate(dot(s.Normal, lightDir));
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (nl * atten * _WorldSpaceLightPos0.w);
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half ao = lerp(1.0, tex2D(_AOMap, IN.uv2_AOMap).r, _AOStrength);
            o.Albedo = c.rgb * ao;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Emission = o.Albedo * _InteriorAmbient.rgb + _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

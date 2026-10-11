// Surface for the Blender-made park toilet: normal albedo texture on UV0 (the UVs already carry the metric tiling), plus a baked
// ambient-occlusion texture on UV1 that darkens corners, recesses, vents and contact with the ground. Opaque, so Enviro fog covers
// it like the rest of the world. _AOStrength 0 = no baked AO, 1 = full. Optional normal map shares the albedo's UVs and tiling.
Shader "Custom/PSXAOLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo (UV0)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map (UV0, same tiling as Albedo)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _AOMap ("Baked AO (UV1)", 2D) = "white" {}
        _AOStrength ("AO strength", Range(0,1)) = 0.85
        _Glossiness ("Smoothness", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emission (self-lit, e.g. lamp glass)", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _AOMap, _BumpMap;
        fixed4 _Color;
        half _AOStrength, _Glossiness, _BumpScale;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_AOMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half ao = tex2D(_AOMap, IN.uv2_AOMap).r;
            o.Albedo = c.rgb * lerp(1.0, ao, _AOStrength);
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Emission = _EmissionColor.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

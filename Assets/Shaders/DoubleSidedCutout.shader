// Lit, two-sided, alpha-tested cloth (torn robes, scarves). Back faces are lit with a flipped normal and slightly darkened.
// Stays in the AlphaTest queue (opaque pass), so the Enviro fog image effect covers it like any other opaque surface.
Shader "Custom/DoubleSidedCutout"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (A = cut out)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _BackfaceDarken ("Back face darkening", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow alphatest:_Cutoff
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _BackfaceDarken;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float back = IN.facing < 0 ? 1.0 : 0.0;
            o.Albedo = c.rgb * (1.0 - back * _BackfaceDarken);
            o.Normal = float3(0, 0, back > 0.5 ? -1 : 1);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}

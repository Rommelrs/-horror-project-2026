// Custom/DoubleSidedCutout (two-sided, alpha-tested cloth) that can also dissolve with the same screen-door dither as
// Custom/DitherDissolve. Stays in the AlphaTest queue, so the fog covers it like any other opaque surface.
// _Opacity 1 = solid, 0 = gone.
Shader "Custom/DitherDissolveCutout"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (A = cut out)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _BackfaceDarken ("Back face darkening", Range(0,1)) = 0.35
        _Opacity ("Opacity", Range(0,1)) = 1
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
        half _Opacity;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
            float facing : VFACE;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 px = floor(IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy);
            float n = frac(52.9829189 * frac(dot(px, float2(0.06711056, 0.00583715))));
            clip(_Opacity - n - 0.001);

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

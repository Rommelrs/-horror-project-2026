// Lit, two-sided, opaque (single-shell cloth such as the Runner's hood, whose inside would otherwise be culled and
// look see-through). Back faces are lit with a flipped normal and slightly darkened. Same inputs as Standard's
// albedo / colour / smoothness / metallic, so a Standard material can be switched to it without losing its look.
Shader "Custom/DoubleSidedLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _BackfaceDarken ("Back face darkening", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
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
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

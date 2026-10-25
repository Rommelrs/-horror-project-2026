// Leaf cards (alpha tested, both sides drawn) for the low-fi foliage: Lambert + fog, shadows cast with the cutout.
Shader "Custom/FoliageCutout"
{
    Properties
    {
        _MainTex ("Leaves (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input { float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}

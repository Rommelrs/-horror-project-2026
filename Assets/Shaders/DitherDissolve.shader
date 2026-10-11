// Standard-looking opaque surface that can be dissolved away with a screen-door dither.
// It stays an OPAQUE material, so the fog image effect treats it exactly like everything else - which a
// transparent fade can't do (fog skips transparent materials). _Opacity 1 = solid, 0 = gone.
Shader "Custom/DitherDissolve"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _Opacity;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // interleaved gradient noise per screen pixel: a fine, even dither
            float2 px = floor(IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy);
            float n = frac(52.9829189 * frac(dot(px, float2(0.06711056, 0.00583715))));
            clip(_Opacity - n - 0.001);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

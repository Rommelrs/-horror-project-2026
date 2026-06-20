Shader "MeshPainter/Texture Blend 4 Layers"
{
    Properties
    {
        _Tint ("Global Tint", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseTex ("Base Texture", 2D) = "white" {}
        _Layer1Tex ("Layer 1 Texture (R)", 2D) = "white" {}
        _Layer2Tex ("Layer 2 Texture (G)", 2D) = "white" {}
        _Layer3Tex ("Layer 3 Texture (B)", 2D) = "white" {}
        _Layer4Tex ("Layer 4 Texture (A)", 2D) = "white" {}
        _ControlTex ("Control Map", 2D) = "black" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BaseTex;
        sampler2D _Layer1Tex;
        sampler2D _Layer2Tex;
        sampler2D _Layer3Tex;
        sampler2D _Layer4Tex;
        sampler2D _ControlTex;

        fixed4 _Tint;
        fixed4 _BaseColor;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float2 uv_BaseTex;
            float2 uv_Layer1Tex;
            float2 uv_Layer2Tex;
            float2 uv_Layer3Tex;
            float2 uv_Layer4Tex;
            float2 uv_ControlTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 weights = saturate(tex2D(_ControlTex, IN.uv_ControlTex));
            half layerTotal = weights.r + weights.g + weights.b + weights.a;
            half baseWeight = saturate(1.0h - layerTotal);
            half denom = max(baseWeight + layerTotal, 0.0001h);

            fixed4 baseColor = tex2D(_BaseTex, IN.uv_BaseTex) * _BaseColor;
            fixed4 layer1 = tex2D(_Layer1Tex, IN.uv_Layer1Tex);
            fixed4 layer2 = tex2D(_Layer2Tex, IN.uv_Layer2Tex);
            fixed4 layer3 = tex2D(_Layer3Tex, IN.uv_Layer3Tex);
            fixed4 layer4 = tex2D(_Layer4Tex, IN.uv_Layer4Tex);

            fixed4 mixed = (baseColor * baseWeight
                + layer1 * weights.r
                + layer2 * weights.g
                + layer3 * weights.b
                + layer4 * weights.a) / denom;

            mixed *= _Tint;
            o.Albedo = mixed.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = mixed.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}

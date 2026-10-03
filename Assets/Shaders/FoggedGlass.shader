// Transparent glass that sits correctly in Enviro's fog.
//
// Enviro Lite applies its fog right after the opaque geometry, so anything transparent (drawn later)
// isn't fogged by it, and a dark pane stays visible long after the rest of the scene has vanished.
//
// This shader keeps the glass looking exactly as authored (your colour/alpha) when it is close to the
// camera, and as it gets further away it blends into Enviro's fog colour and fades out, so it
// disappears together with everything else. The two distances are set per material.
Shader "Custom/FoggedGlass"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,0.475)
        _MainTex ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _RevealNear ("Fully visible within (m)", Float) = 8
        _RevealFar ("Hidden beyond (m)", Float) = 22
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        CGPROGRAM
        #include "Assets/Enviro - Sky and Weather/Core/Resources/Shaders/Core/EnviroFogCore.cginc"
        // alpha:fade = ordinary (non-premultiplied) alpha blending, so lowering the alpha really does
        // fade the pane out instead of adding light on top of what is behind it
        #pragma surface surf Standard fullforwardshadows alpha:fade finalcolor:ApplyFog
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _CameraDepthTexture;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _RevealNear;
        float _RevealFar;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
            float3 worldPos;
        };

        void ApplyFog(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            float3 wPos = IN.worldPos.xyz;

            // 1 = close (looks exactly as authored), 0 = far (lost in the fog)
            float dist = distance(wPos, _WorldSpaceCameraPos);
            float reveal = saturate((_RevealFar - dist) / max(_RevealFar - _RevealNear, 0.001));
            reveal = reveal * reveal * (3.0 - 2.0 * reveal); // smoothstep

            #ifndef UNITY_PASS_FORWARDADD
                float3 uvscreen = IN.screenPos.xyz / IN.screenPos.w;
                half linear01Depth = Linear01Depth(uvscreen.z);

                // The same fog tint Enviro's own transparent shader would give this pane...
                float4 fogged = TransparentFog(color, wPos, uvscreen.xy, linear01Depth);

                // ...blended in only as the pane gets further away
                color.rgb = lerp(fogged.rgb, color.rgb, reveal);
            #endif

            color.a *= reveal;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Transparent/VertexLit"
}

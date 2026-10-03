Shader "Custom/CustomUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Contrast ("Contrast", Range(0.5, 3)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1

        // "Show through fog" - driven by the material inspector (CustomUnlitGUI), not edited by hand.
        // When on, the item is drawn after the fog and fades in with distance instead of being fogged.
        [HideInInspector] _ShowThroughFog ("Show Through Fog", Float) = 0
        [HideInInspector] _RevealNear ("Fully visible within (m)", Float) = 9
        [HideInInspector] _RevealFar ("Hidden beyond (m)", Float) = 20
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // No fog application here on purpose: Unity's built-in per-vertex RenderSettings.fog
        // is a separate thing from Enviro's fog, and skipping it keeps this shader from reacting
        // to fog density Enviro drives through RenderSettings.
        Pass
        {
            // Normal opaque rendering (One/Zero) unless "Show through fog" is on (SrcAlpha/OneMinusSrcAlpha),
            // so existing materials are unchanged.
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Contrast;
            half _Brightness;
            float _ShowThroughFog;
            float _RevealNear;
            float _RevealFar;

            // Global (not a material property): set to 1 by ItemContrastFilter for cameras that
            // shouldn't show the contrast/brightness boost (e.g. the Inspection or Map cameras).
            // Unset it is 0, so the boost is on by default.
            float _ContrastDisabled;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Brightness, then contrast around mid-grey. Contrast is applied in gamma space so the
                // slider behaves the same (and looks natural) whether the project is Gamma or Linear.
                // 1 / 1 leaves the colour exactly as it was.
                half brightness = lerp(_Brightness, 1.0h, _ContrastDisabled);
                half contrast = lerp(_Contrast, 1.0h, _ContrastDisabled);

                half3 rgb = col.rgb * brightness;
            #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(saturate(rgb));
            #endif
                rgb = saturate((rgb - 0.5h) * contrast + 0.5h);
            #ifndef UNITY_COLORSPACE_GAMMA
                rgb = GammaToLinearSpace(rgb);
            #endif

                col.rgb = rgb;

                // Show through fog: the item is drawn AFTER the fog, so on its own it would look crisp
                // at any distance. Fade it in with the camera's distance instead - hidden far away
                // (it stays lost in the fog), fully visible when close. Cameras that don't get the
                // boost (e.g. the Inspection camera) always show the item fully.
                float dist = distance(i.worldPos, _WorldSpaceCameraPos);
                float reveal = saturate((_RevealFar - dist) / max(_RevealFar - _RevealNear, 0.001));
                reveal = reveal * reveal * (3.0 - 2.0 * reveal); // smoothstep
                reveal = lerp(reveal, 1.0, _ContrastDisabled);

                col.a = lerp(1.0, reveal, _ShowThroughFog);
                // Fully faded pixels: skip them so they don't write depth
                clip(col.a - lerp(-1.0, 0.003, _ShowThroughFog));

                return col;
            }
            ENDCG
        }

        // Enviro's fog/dust is a full-screen post-process: it reads the camera's depth texture
        // and re-composites the whole screen based on distance. In Built-in RP, that depth
        // texture is generated from each object's ShadowCaster pass - without one, this object
        // is invisible to it and gets misread as background/sky, which is what was making it
        // look like it was fading into transparency once fog/dust turned the effect on.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    // Adds a "Show through fog" checkbox under the properties (sets the material's Render Queue)
    CustomEditor "CustomUnlitGUI"
}

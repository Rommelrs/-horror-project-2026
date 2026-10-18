// Shaped neon glow for channel-letter signs: a flat additive quad laid on the sign face whose texture is the letters' blurred
// halo (made from the letter shapes, so the glow hugs the lettering). Same idea as Custom/LampGlow: drawn in the Transparent
// queue so Enviro Lite's opaque-only fog never touches it - the sign keeps glowing through fog - and it fades in with
// distance (_MaxDist / _Curve). Beyond the camera's short far clip it is pulled inside the far plane at the same apparent size,
// so the lettering's glow still reads from far away. LampGlowOcclusion on the same object drives _Visibility.
Shader "Custom/NeonGlow"
{
    Properties
    {
        _MainTex ("Halo mask (R)", 2D) = "black" {}
        [HDR] _Color ("Glow colour", Color) = (0.55, 1.0, 0.72, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.0
        _Gamma ("Halo falloff (higher = tighter around the letters)", Range(0.3, 3)) = 1.0
        _MaxDist ("Visible up to (m)", Float) = 50
        _Curve ("Fade curve (1 = linear, higher = stays faint longer then rises late)", Range(0.3, 4)) = 0.6
        _Visibility ("Line-of-sight visibility (set by LampGlowOcclusion)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        ZTest LEqual
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity, _Gamma, _MaxDist, _Curve, _Visibility;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                // beyond ~90% of the camera's far clip, scale the quad toward the camera so it lands inside the far plane at the
                // same apparent size (as Custom/LampGlow does) - the shaped glow stays visible far away
                float3 centre = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float d = distance(_WorldSpaceCameraPos, centre);
                float squeeze = min(d, _ProjectionParams.z * 0.9) / max(d, 0.001);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                wp = _WorldSpaceCameraPos + (wp - _WorldSpaceCameraPos) * squeeze;
                o.pos = UnityWorldToClipPos(wp);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fade = pow(saturate(1.0 - d / _MaxDist), _Curve) * _Visibility;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float m = pow(saturate(tex2D(_MainTex, i.uv).r), _Gamma);
                return fixed4(_Color.rgb * m * _Intensity * i.fade, 1);
            }
            ENDCG
        }
    }
}

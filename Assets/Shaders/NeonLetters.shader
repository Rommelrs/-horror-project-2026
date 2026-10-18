// Unlit neon-sign surface (3D channel letters, lit logo faces) that keeps showing through fog and beyond the camera's short
// far clip. Drawn in the Transparent queue, so Enviro Lite's opaque-only fog doesn't touch it. When the object is farther
// than ~90% of the far clip, the whole mesh is scaled toward the camera (about the camera position) so it lands inside the
// far plane at exactly the same apparent size and shape - the same trick Custom/LampGlow uses for its glow dot.
// Past _FadeStart it fades out towards _MaxDist. LampGlowOcclusion on the same object drives _Visibility (things beyond
// the far clip aren't drawn, so they can't hide it by depth).
Shader "Custom/NeonLetters"
{
    Properties
    {
        _MainTex ("Texture (optional)", 2D) = "white" {}
        _Color ("Colour", Color) = (0.93, 1.0, 0.94, 1)
        _FadeStart ("Full strength up to (m)", Float) = 14
        _MaxDist ("Visible up to (m)", Float) = 90
        _Curve ("Fade curve (1 = linear, lower = stays visible longer)", Range(0.3, 4)) = 0.8
        _Visibility ("Line-of-sight visibility (set by LampGlowOcclusion)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            float _FadeStart, _MaxDist, _Curve, _Visibility;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 centre = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float d = distance(_WorldSpaceCameraPos, centre);
                float squeeze = min(d, _ProjectionParams.z * 0.9) / max(d, 0.001);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                wp = _WorldSpaceCameraPos + (wp - _WorldSpaceCameraPos) * squeeze;
                o.pos = UnityWorldToClipPos(wp);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float t = saturate((d - _FadeStart) / max(_MaxDist - _FadeStart, 0.001));
                o.fade = pow(1.0 - t, _Curve) * _Visibility;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                return fixed4(c.rgb, i.fade);
            }
            ENDCG
        }
    }
}

// A shell of the Questioner's scream (ScreamWaveEmitter): an expanding sphere that bends what's behind it at its silhouette,
// so the sound reads as rings of rippling air rolling out from his mouth. A faint pale rim on top. _Fade 1 -> 0 over its life.
Shader "Custom/QuestionerScreamWave"
{
    Properties
    {
        _Strength ("Distortion", Float) = 0.07
        _Tint ("Rim tint (alpha = amount)", Color) = (0.9, 0.86, 0.8, 0.10)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        GrabPass { "_ScreamGrab" }
        Pass
        {
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ScreamGrab;
            float _Strength;
            float4 _Tint;
            float _Fade;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float3 nrm : TEXCOORD1;
                float3 view : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeGrabScreenPos(o.pos);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.nrm);
                float f = 1.0 - abs(dot(n, normalize(i.view)));
                float rim = f * f * f;
                float3 vn = mul((float3x3)UNITY_MATRIX_V, n);
                float2 off = vn.xy * _Strength * rim * _Fade;
                float4 g = i.grab;
                g.xy += off * g.w;
                float3 col = tex2Dproj(_ScreamGrab, UNITY_PROJ_COORD(g)).rgb;
                col += _Tint.rgb * _Tint.a * rim;
                return float4(col, saturate(rim * 2.5) * _Fade);
            }
            ENDCG
        }
    }
    Fallback Off
}

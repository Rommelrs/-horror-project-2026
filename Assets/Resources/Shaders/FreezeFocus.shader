// The world around the two of them washes out (time stands still): used by FreezeFocusEffect.
Shader "Hidden/FreezeFocus"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4x4 _InvVP;
            float3 _Focus;
            float _Amount, _Near, _Far;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float2 duv = i.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) duv.y = 1 - duv.y;
                #endif
                float d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, duv);
                float4 w = mul(_InvVP, float4(duv * 2 - 1, d, 1));
                w.xyz /= w.w;
                float far01 = smoothstep(_Near, _Far, distance(w.xyz, _Focus));
                float g = dot(c.rgb, fixed3(0.3, 0.59, 0.11));
                // far: pale and see-through, like a faded print; near: only drained of most of its colour
                fixed3 ghost = lerp(fixed3(g, g, g) * 0.45 + 0.2, fixed3(0.74, 0.71, 0.65), 0.6);
                fixed3 near = lerp(c.rgb, fixed3(g, g, g), 0.35);
                c.rgb = lerp(c.rgb, lerp(near, ghost, far01), _Amount);
                return c;
            }
            ENDCG
        }
    }
}

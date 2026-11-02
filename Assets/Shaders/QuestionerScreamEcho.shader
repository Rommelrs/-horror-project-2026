// The Questioner's scream hitting Khalid (ScreamEchoEffect on the main camera): the view blurs out of focus, ripples roll
// out from the centre of the screen, echo copies of the picture are pushed outward, a radial colour split, and the last
// frames linger (feedback), so the view smears like the sound bouncing around his head.
Shader "Hidden/QuestionerScreamEcho"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _History ("History", 2D) = "black" {}
    }
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
            sampler2D _History;
            float _Amount;      // 0..1 envelope
            float _Feedback;    // how much of the last frame lingers
            float _Phase;       // ripple time
            float _Blur;        // blur radius at full strength (uv)

            // 13-tap disc blur (two rings + centre)
            float3 Blurred (float2 uv, float r)
            {
                float3 c = tex2D(_MainTex, uv).rgb * 2.0;
                float w = 2.0;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.0472;
                    float2 o = float2(cos(a), sin(a)) * r;
                    c += tex2D(_MainTex, uv + o * 0.5).rgb * 1.5;
                    c += tex2D(_MainTex, uv + float2(cos(a + 0.5236), sin(a + 0.5236)) * r).rgb;
                    w += 2.5;
                }
                return c / w;
            }

            float4 frag (v2f_img i) : SV_Target
            {
                const float2 c = float2(0.5, 0.5);
                float2 d = i.uv - c;
                float r = length(d);
                float2 n = r > 1e-4 ? d / r : float2(0, 0);

                // rings rolling outward
                float ripple = sin(r * 46.0 - _Phase * 24.0) * 0.004 * _Amount * smoothstep(0.03, 0.45, r);
                float2 uv = i.uv + n * ripple;
                // out of focus, more toward the edges
                float br = _Blur * _Amount * (0.55 + r);
                float3 col = Blurred(uv, br);

                // echo copies, each a little further out from the centre
                float3 acc = col;
                float wsum = 1.0;
                [unroll] for (int k = 1; k <= 4; k++)
                {
                    float s = 1.0 - 0.026 * k * _Amount;
                    float w = 0.7 / k;
                    acc += Blurred(c + (uv - c) * s, br * 0.6) * w;
                    wsum += w;
                }
                col = lerp(col, acc / wsum, _Amount);

                // radial colour split
                float ca = 0.012 * _Amount * r;
                col.r = lerp(col.r, tex2D(_MainTex, uv + n * ca).r, _Amount);
                col.b = lerp(col.b, tex2D(_MainTex, uv - n * ca).b, _Amount);

                // the previous frames linger, drifting outward
                float3 h = tex2D(_History, c + (i.uv - c) * (1.0 - 0.012 * _Amount)).rgb;
                col = lerp(col, h, _Feedback);

                // the edges close in
                col *= 1.0 - _Amount * 0.4 * smoothstep(0.25, 0.75, r);
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}

Shader "Custom/Damaged Wall Brick"
{
    Properties
    {
        [Header(Textures)]
        [NoScaleOffset] _MainTex ("Brick Tile", 2D) = "white" {}
        [NoScaleOffset] _DamageTex ("Damage Decal", 2D) = "black" {}

        [Header(Base Brick)]
        _BaseTiling ("Brick Tiling", Vector) = (30.9, 18.7, 0, 0)
        _BaseOffset ("Brick Offset", Vector) = (1.0, 0.8, 0, 0)
        _BaseTint ("Base Tint", Color) = (1, 1, 1, 1)
        _ToneNoiseScale ("Broad Tone Scale", Float) = 2.25
        _ToneVariation ("Broad Tone Strength", Range(0, 0.5)) = 0.15

        [Header(Damage Placement)]
        _DamageA ("Damage A Scale Offset", Vector) = (3.4, 1.65, 0.0, 0.0)
        _DamageB ("Damage B Scale Offset", Vector) = (4.1, 1.92, 0.31, 0.67)
        _DamageC ("Damage C Scale Offset", Vector) = (2.85, 1.42, 0.63, 0.22)
        _SelectorABScale ("Selector A-B Scale", Float) = 1.55
        _SelectorCScale ("Selector C Scale", Float) = 0.8
        _OrganicScale ("Organic Strength Scale", Float) = 3.2
        _DamageAmount ("Damage Amount", Range(0, 2)) = 1.0
        _DamageThreshold ("Damage Threshold", Range(0, 1)) = 0.42
        _DamageSoftness ("Damage Softness", Range(0.001, 0.5)) = 0.18
        _DamageTint ("Damage Tint and Mix", Color) = (0.54, 0.46, 0.38, 0.34)

        [Header(Surface)]
        _BaseSmoothness ("Base Smoothness", Range(0, 1)) = 0.28
        _DamageSmoothness ("Damage Smoothness", Range(0, 1)) = 0.05
        _BrickRelief ("Brick Relief", Range(0, 1)) = 0.17
        _DamageDepth ("Damage Recess", Range(0, 1)) = 0.28
        _GrainScale ("Fine Grain Scale", Float) = 52
        _GrainStrength ("Fine Grain Strength", Range(0, 0.5)) = 0.10
        _BumpStrength ("Combined Bump Strength", Range(0, 4)) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DamageTex;

        float4 _BaseTiling;
        float4 _BaseOffset;
        fixed4 _BaseTint;
        float _ToneNoiseScale;
        float _ToneVariation;

        float4 _DamageA;
        float4 _DamageB;
        float4 _DamageC;
        float _SelectorABScale;
        float _SelectorCScale;
        float _OrganicScale;
        float _DamageAmount;
        float _DamageThreshold;
        float _DamageSoftness;
        fixed4 _DamageTint;

        float _BaseSmoothness;
        float _DamageSmoothness;
        float _BrickRelief;
        float _DamageDepth;
        float _GrainScale;
        float _GrainStrength;
        float _BumpStrength;

        struct Input
        {
            float2 uv_MainTex;
        };

        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float valueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = hash21(i);
            float b = hash21(i + float2(1, 0));
            float c = hash21(i + float2(0, 1));
            float d = hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float2 mirroredUV(float2 uv)
        {
            return 1.0 - abs(frac(uv) * 2.0 - 1.0);
        }

        fixed4 damageSample(float2 uv, float4 transformData)
        {
            return tex2D(_DamageTex, mirroredUV(uv * transformData.xy + transformData.zw));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            float2 brickUV = uv * _BaseTiling.xy + _BaseOffset.xy;
            fixed3 brick = tex2D(_MainTex, brickUV).rgb;

            float broadTone = valueNoise(uv * _ToneNoiseScale + float2(4.7, 9.2));
            brick *= lerp(1.0 - _ToneVariation, 1.0 + _ToneVariation, broadTone);

            fixed4 decalA = damageSample(uv, _DamageA);
            fixed4 decalB = damageSample(uv.yx, _DamageB);
            fixed4 decalC = damageSample(uv, _DamageC);

            float selectorAB = smoothstep(0.30, 0.70, valueNoise(uv * _SelectorABScale + float2(11.3, 2.9)));
            float selectorC = smoothstep(0.35, 0.68, valueNoise(uv * _SelectorCScale + float2(3.1, 17.7)));
            fixed4 damage = lerp(lerp(decalA, decalB, selectorAB), decalC, selectorC);

            float organic = lerp(0.55, 1.0, valueNoise(uv * _OrganicScale + float2(23.4, 7.8)));
            float rawMask = saturate(damage.a * organic * _DamageAmount);
            float damageMask = smoothstep(
                _DamageThreshold - _DamageSoftness,
                _DamageThreshold + _DamageSoftness,
                rawMask
            );

            fixed3 baseColor = brick * _BaseTint.rgb;
            fixed3 damageColor = lerp(damage.rgb, _DamageTint.rgb, _DamageTint.a);

            o.Albedo = lerp(baseColor, damageColor, damageMask);
            o.Metallic = 0.0;
            o.Smoothness = lerp(_BaseSmoothness, _DamageSmoothness, damageMask);
            o.Occlusion = lerp(1.0, 0.82, damageMask);

            float fineGrain = valueNoise(uv * _GrainScale);
            float brickHeight = dot(brick, fixed3(0.299, 0.587, 0.114)) * _BrickRelief;
            float height = brickHeight + (fineGrain - 0.5) * _GrainStrength - damageMask * _DamageDepth;
            float2 screenGradient = float2(ddx(height), ddy(height));
            o.Normal = normalize(float3(-screenGradient.x * _BumpStrength, -screenGradient.y * _BumpStrength, 1.0));
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
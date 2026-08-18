Shader "HorrorProject/Damaged Brick Wall"
{
    Properties
    {
        _MainTex ("Brick Tile", 2D) = "white" {}
        _DamageTex ("Damage Decal", 2D) = "black" {}
        _Color ("Brick Tint", Color) = (1,1,1,1)
        _DamageColor ("Damage Tint", Color) = (1,1,1,1)
        _MainTiling ("Brick Tiling", Vector) = (4,4,0,0)
        _MainRotation ("Brick Rotation (Degrees)", Range(-180,180)) = 0
        _DamageTiling ("Damage Tiling", Vector) = (0.72,0.72,0,0)
        _DamageRotation ("Damage Rotation (Degrees)", Range(-180,180)) = 0
        _DamageStrength ("Damage Coverage", Range(0,2)) = 1.0
        _DamageContrast ("Damage Contrast", Range(0.25,4)) = 1.35
        _Roughness ("Roughness", Range(0,1)) = 0.82
        _ReliefStrength ("Brick Relief", Range(0,4)) = 1.1
        _ToneVariation ("Tone Variation", Range(0,0.3)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DamageTex;
        float4 _MainTex_TexelSize;
        float4 _MainTiling;
        float4 _DamageTiling;
        half _MainRotation;
        half _DamageRotation;
        fixed4 _Color;
        fixed4 _DamageColor;
        half _DamageStrength;
        half _DamageContrast;
        half _Roughness;
        half _ReliefStrength;
        half _ToneVariation;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        inline half Luma(fixed3 c)
        {
            return dot(c, fixed3(0.2126, 0.7152, 0.0722));
        }

        inline float2 RotateTiledUV(float2 uv, float2 tiling, half degrees)
        {
            float2 tiled = uv * tiling;
            float2 pivot = tiling * 0.5;
            float angle = degrees * 0.01745329252;
            float sine = sin(angle);
            float cosine = cos(angle);
            float2 centered = tiled - pivot;
            float2 rotated = float2(
                cosine * centered.x - sine * centered.y,
                sine * centered.x + cosine * centered.y
            );
            return rotated + pivot;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 brickUV = RotateTiledUV(IN.uv_MainTex, _MainTiling.xy, _MainRotation);
            fixed4 brick = tex2D(_MainTex, brickUV);

            float2 damageUV = RotateTiledUV(IN.uv_MainTex, _DamageTiling.xy, _DamageRotation);
            fixed4 damageA = tex2D(_DamageTex, damageUV);
            fixed4 damageB = tex2D(_DamageTex, float2(-damageUV.x * 0.91 + 0.37, damageUV.y * 1.07 + 0.19));
            fixed4 damageC = tex2D(_DamageTex, float2(damageUV.x * 1.13 + 0.61, -damageUV.y * 0.86 + 0.43));

            half selectorAB = smoothstep(0.25, 0.75, 0.5 + 0.5 * sin(dot(IN.uv_MainTex, float2(7.17, 11.31))));
            half selectorC = smoothstep(0.30, 0.78, 0.5 + 0.5 * sin(dot(IN.uv_MainTex, float2(-13.7, 5.9)) + 1.7));
            fixed4 damage = lerp(lerp(damageA, damageB, selectorAB), damageC, selectorC * 0.45);

            half mask = pow(saturate(damage.a * _DamageStrength), _DamageContrast);
            half variation = (sin(dot(IN.worldPos.xz, float2(0.31, 0.47))) * 0.5 + 0.5) * _ToneVariation;
            fixed3 baseColor = brick.rgb * _Color.rgb * (1.0 - _ToneVariation * 0.5 + variation);
            fixed3 damagedColor = damage.rgb * _DamageColor.rgb;

            o.Albedo = lerp(baseColor, damagedColor, mask);
            o.Metallic = 0.0;
            o.Smoothness = saturate(1.0 - _Roughness - mask * 0.08);
            o.Occlusion = lerp(1.0, 0.72, mask);

            float2 texel = _MainTex_TexelSize.xy;
            half hL = Luma(tex2D(_MainTex, brickUV - float2(texel.x, 0)).rgb);
            half hR = Luma(tex2D(_MainTex, brickUV + float2(texel.x, 0)).rgb);
            half hD = Luma(tex2D(_MainTex, brickUV - float2(0, texel.y)).rgb);
            half hU = Luma(tex2D(_MainTex, brickUV + float2(0, texel.y)).rgb);
            o.Normal = normalize(half3((hL - hR) * _ReliefStrength, (hD - hU) * _ReliefStrength, 1.0));
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}
// Fake spot-light pool for surfaces that use an UNLIT shader (the road uses Custom/RoadAsphaltDetail, which no real Light can brighten).
// Put it on a mesh copied from the surface (slightly above it); LightPoolDecal feeds it the lamp position / direction / angle / range
// from a real spot Light, so the pool always matches the light. Additive, so it only ever brightens; the cone edge uses the same soft
// radial profile as the SoftSpotCookie texture, so there is no hard rim. Fades out in fog like other additive effects.
Shader "Custom/LightPoolDecal"
{
    Properties
    {
        [HDR] _Color ("Light colour", Color) = (1.0, 0.72, 0.4, 1)
        _Strength ("Strength (set by LightPoolDecal from the light's intensity)", Range(0, 3)) = 0.35
        _LampPos ("Lamp position (set by LightPoolDecal)", Vector) = (0, 5, 0, 0)
        _LampDir ("Lamp direction (set by LightPoolDecal)", Vector) = (0, -1, 0, 0)
        _TanHalfAngle ("tan(half spot angle) (set by LightPoolDecal)", Float) = 0.6
        _Range ("Range (set by LightPoolDecal)", Float) = 14
        // each pixel is lit once even where road triangles overlap (stencil). Give overlapping pools of different lamps different values.
        [IntRange] _StencilRef ("Stencil value", Range(1, 255)) = 77
        // optional cut-out: an object that sits in the pool and is lit by the real light instead (set by LightPoolDecal)
        _HoleRow0 ("", Vector) = (0,0,0,0)
        _HoleRow1 ("", Vector) = (0,0,0,0)
        _HoleRow2 ("", Vector) = (0,0,0,0)
        _HoleCenter ("", Vector) = (0,0,0,0)
        _HoleExtents ("", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1
        Stencil { Ref [_StencilRef] Comp NotEqual Pass Replace }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength, _TanHalfAngle, _Range;
            float4 _LampPos, _LampDir;

            // Enviro Lite's dust is an opaque-only image effect, so anything transparent (this pool) must apply it itself.
            // These globals are set every frame by EnviroSkyLite / EnviroSkyRenderingLW (see EnviroFogCore.cginc: ComputeFogFactor / TransparentFog)
            float4 _SceneFogParams, _SceneFogMode, _DistanceParams, _EnviroParams;
            float _distanceFogIntensity, _maximumFogDensity;

            float4 _HoleRow0, _HoleRow1, _HoleRow2, _HoleCenter, _HoleExtents;   // _HoleExtents.w = 0 -> no hole

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_FOG_COORDS(1) float3 nrm : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 toP = i.wp - _LampPos.xyz;
                float d = length(toP);
                float3 dir = toP / max(d, 1e-4);
                float cosA = dot(dir, normalize(_LampDir.xyz));

                // angle off the spot axis -> radius in the cookie (1 = the cone edge), same as Unity's spot cookie mapping
                float tanA = sqrt(max(1.0 - cosA * cosA, 0.0)) / max(cosA, 1e-3);
                float r = cosA > 0.0 ? tanA / max(_TanHalfAngle, 1e-3) : 2.0;
                float x = saturate((r - 0.05) / 0.95);
                float cookie = pow(1.0 - smoothstep(0.0, 1.0, x), 1.3);     // identical to the SoftSpotCookie.png profile

                float att = smoothstep(0.0, 1.0, saturate(1.0 - d / max(_Range, 0.01)));   // smooth fade to nothing at the light's range
                float ndl = saturate(dot(normalize(i.nrm), -dir));          // lambert term from the surface normal (the road pool mesh has up normals, so it behaves as before)

                float v = cookie * att * ndl * _Strength;

                // an object standing in the pool (the plank) is lit by the real light, so no fake light on top of it: soft-edged box cut-out
                if (_HoleExtents.w > 0.5)
                {
                    float4 wp4 = float4(i.wp, 1.0);
                    float3 q = float3(dot(_HoleRow0, wp4), dot(_HoleRow1, wp4), dot(_HoleRow2, wp4)) - _HoleCenter.xyz;
                    float3 outside = abs(q) - _HoleExtents.xyz;                 // < 0 inside the box on that axis
                    float edge = max(outside.x, max(outside.y, outside.z));
                    v *= smoothstep(0.0, 0.08, edge);                           // 0 inside the plank, back to full 8 cm outside it
                }
                // distance to the camera, same as the opaque fog uses (minus the near plane)
                float3 camVec = i.wp - _WorldSpaceCameraPos;
                float camDist = length(camVec);

                // keep the light in step with the dust: how much of the surface still shows through the fog (1 = clear, 0 = fully fogged)
                bool enviroFog = _SceneFogMode.x > 0.5 && _EnviroParams.y > 0.0;
                float fogVis = 1.0;
                if (enviroFog)
                {
                    float g = (_DistanceParams.x + (camDist - _ProjectionParams.y)) * _distanceFogIntensity;
                    g = max(0.0, g);
                    float fogFac = 0.0;
                    if (_SceneFogMode.x < 1.5)      fogFac = g * _SceneFogParams.z + _SceneFogParams.w;          // linear
                    else if (_SceneFogMode.x < 2.5) fogFac = exp2(-_SceneFogParams.y * g);                       // exp
                    else                            { float f = _SceneFogParams.x * g; fogFac = exp2(-f * f); } // exp2
                    fogVis = lerp(_maximumFogDensity, 1.0, saturate(fogFac));
                }
                v *= fogVis;

                // never end in a hard line at the camera's far clip plane: fade out over the last stretch before it
                v *= 1.0 - smoothstep(0.78, 0.97, camDist / max(_ProjectionParams.z, 0.01));

                fixed4 col = fixed4(_Color.rgb * v, 1);
                if (!enviroFog)
                {
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));   // no Enviro: normal Unity fog
                }
                return col;
            }
            ENDCG
        }
    }
}

// The light a vehicle's headlights throw on the ground: two separate oval beams, one per lamp, each starting at its lamp with a hot
// spot, swelling to its widest a little way out and fading away to a point, drifting slightly outward as they go. Where the two
// beams overlap in front of the bumper they add up to a brighter patch. A slow drifting dust pattern breaks up the smooth gradient.
// Additive and drawn in the Transparent queue, so Enviro Lite's opaque-only fog doesn't swallow it: the lit ground keeps glowing
// through the fog.
// Put it on the mesh HeadlightPoolQuad (x -0.5..0.5, z 0..1, uv = (across, along)); scale x = width and z = length, in metres. The
// beams are measured in metres from the quad's centre line (lamps at +/- _HalfSpacing). Real geometry, so it is hidden by anything in
// front of it (the truck, walls) and, like all geometry, clipped by the camera's far plane.
Shader "Custom/HeadlightPool"
{
    Properties
    {
        [HDR] _Color ("Light colour", Color) = (1.0, 0.92, 0.7, 1)
        _Intensity ("Brightness", Range(0,2)) = 0.8
        _HalfSpacing ("Distance of each lamp from the centre line (m)", Float) = 0.63
        _BeamHalfWidth ("Half width of each beam at its widest (m)", Float) = 0.55
        _Spread ("How far each beam drifts outward by its tip (m)", Float) = 0.2
        _Falloff ("Fade along the beam (higher = dies out sooner)", Range(0.3,4)) = 0.8
        _Hotspot ("Extra brightness right at the lamps", Range(0,2)) = 0.35
        _Dust ("Dust breakup", Range(0,1)) = 0.3
        _MaxDist ("Visible up to (m)", Float) = 80
        _Curve ("Fade curve", Range(1,4)) = 1.7
        _Visibility ("Set by code", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity, _HalfSpacing, _BeamHalfWidth, _Spread, _Falloff, _Hotspot, _Dust, _MaxDist, _Curve, _Visibility;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 size : TEXCOORD1; float fade : TEXCOORD2; float2 world : TEXCOORD3; };

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }


            // The camera's far clip is short (~19 m): geometry beyond it is sliced off piece by piece. Slide each vertex along its own
            // sight line to inside the far plane instead (gently compressing everything past 55% of it), so the on-screen shape is
            // unchanged and a far beam stays whole. Depth order among the beam's own vertices is kept.
            float3 FitInsideFarClip (float3 wp)
            {
                float3 toV = wp - _WorldSpaceCameraPos;
                float d = length(toV);
                float far = _ProjectionParams.z;
                float d0 = far * 0.55, dMax = far * 0.92;
                float dd = d <= d0 ? d : d0 + (dMax - d0) * (1.0 - exp(-(d - d0) / (far * 0.6)));
                return _WorldSpaceCameraPos + toV * (dd / max(d, 1e-4));
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(float4(FitInsideFarClip(wpos), 1));
                o.uv = v.uv;
                // the quad's size in metres (its scale), so the beams keep their real proportions whatever the quad is stretched to
                o.size = float2(length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)),
                                length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22)));
                o.world = wpos.xz;
                float3 centre = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float d = distance(_WorldSpaceCameraPos, centre);
                o.fade = pow(saturate(1.0 - d / max(_MaxDist, 0.01)), _Curve) * _Visibility;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float across = (i.uv.x - 0.5) * i.size.x;      // metres from the centre line
                float u = saturate(i.uv.y);                    // 0 at the lamps .. 1 at the tip of the beams

                // beam width: a point at the lamp (with a little body), widest ~40% of the way out, closing to a point at the tip
                float shape = max(sin(3.14159 * pow(u, 0.8)), 0.3 * (1.0 - u));
                float halfWidth = max(_BeamHalfWidth * shape, 0.03);

                // brightest at the lamp, fading out; a hot spot hugging the lamps
                float along = pow(1.0 - u, _Falloff) + _Hotspot * exp(-u * 9.0);
                along *= smoothstep(0.0, 0.08, u);

                float beams = 0.0;
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1.0 : 1.0;
                    float centre = side * (_HalfSpacing + _Spread * u);
                    float dx = (across - centre) / halfWidth;
                    beams += exp(-pow(abs(dx), 2.4) * 1.6);    // fairly full across with soft sides; the two beams add up where they overlap
                }

                // slow drifting dust so the beams aren't a flat gradient
                float n = vnoise(i.world * 0.9 + float2(_Time.y * 0.12, -_Time.y * 0.07)) * 0.65 + vnoise(i.world * 2.3 - _Time.y * 0.1) * 0.35;
                float dust = 1.0 - _Dust * (1.0 - n);

                float a = beams * along * dust * _Intensity * i.fade;
                return fixed4(_Color.rgb * a, 1);
            }
            ENDCG
        }
    }
}

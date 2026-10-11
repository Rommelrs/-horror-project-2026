// The visible shaft of a headlight beam hanging in the fog/dust in front of the lamp: a soft cone, brightest at the lamp and fading
// away along its length, with soft see-through sides (brightest where you look straight through the middle of it, fading toward its
// outline so it has no hard edge), and a slow drifting dust pattern. Additive and drawn in the Transparent queue, so Enviro Lite's
// opaque-only fog doesn't swallow it. Put it on the mesh HeadlightBeamCone (apex at the origin, pointing along +Z, length 1, radius 1):
// scale x/y = the cone's radius at its far end, z = its length.
Shader "Custom/HeadlightBeam"
{
    Properties
    {
        [HDR] _Color ("Light colour", Color) = (1.0, 0.92, 0.7, 1)
        _Intensity ("Brightness", Range(0,2)) = 0.35
        _Falloff ("Fade along the beam (higher = dies out sooner)", Range(0.3,4)) = 1.3
        _EdgePower ("Softness of the sides (higher = thinner core, softer edges)", Range(0.5,6)) = 3.0
        _GroundY ("Ground height under the beam (set by code)", Float) = -1000
        _GroundFade ("Fade out this far (m) above the ground, so it never ends in a hard line on it", Range(0.05,2)) = 0.6
        _Dust ("Dust breakup", Range(0,1)) = 0.35
        _MaxDist ("Visible up to (m)", Float) = 80
        _Curve ("Fade curve", Range(1,4)) = 1.7
        _Visibility ("Set by code", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Intensity, _Falloff, _EdgePower, _GroundY, _GroundFade, _Dust, _MaxDist, _Curve, _Visibility;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 nrm : TEXCOORD1; float3 view : TEXCOORD2; float3 world : TEXCOORD3; float fade : TEXCOORD4; };

            float hash (float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float vnoise (float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                float n000 = hash(i), n100 = hash(i + float3(1,0,0)), n010 = hash(i + float3(0,1,0)), n110 = hash(i + float3(1,1,0));
                float n001 = hash(i + float3(0,0,1)), n101 = hash(i + float3(1,0,1)), n011 = hash(i + float3(0,1,1)), n111 = hash(i + float3(1,1,1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
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
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(float4(FitInsideFarClip(o.world), 1));
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - o.world;
                float3 apex = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float d = distance(_WorldSpaceCameraPos, apex);
                o.fade = pow(saturate(1.0 - d / max(_MaxDist, 0.01)), _Curve) * _Visibility;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float u = saturate(i.uv.y);                                   // 0 at the lamp .. 1 at the far end
                float along = pow(1.0 - u, _Falloff) * smoothstep(0.0, 0.04, u + 0.04);
                // see-through sides: the cone is bright where the surface faces you and fades to nothing at its outline
                float facing = abs(dot(normalize(i.nrm), normalize(i.view)));
                float edge = pow(saturate(facing), _EdgePower);
                // fade out toward the ground instead of being cut off by it
                edge *= smoothstep(0.0, _GroundFade, i.world.y - _GroundY);
                // drifting dust
                float n = vnoise(i.world * 0.8 + float3(_Time.y * 0.1, 0, -_Time.y * 0.06)) * 0.65 + vnoise(i.world * 2.1 - _Time.y * 0.08) * 0.35;
                float dust = 1.0 - _Dust * (1.0 - n);
                float a = along * edge * dust * _Intensity * i.fade;
                return fixed4(_Color.rgb * a, 1);
            }
            ENDCG
        }
    }
}

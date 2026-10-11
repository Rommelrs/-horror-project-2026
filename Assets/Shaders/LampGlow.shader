// Camera-facing additive glow for lamps, drawn in the Transparent queue so Enviro Lite's opaque-only fog never touches it:
// the lamp keeps shining through fog. Hidden by walls/props (ZTest on), grows a little with distance so it stays a visible dot
// far away, and fades in gradually: invisible at _MaxDist, a faint speck far away, brighter and bigger as you approach. Put it on a default Unity Quad.
// Because the glow is slid inside the camera's far clip, a building beyond the clip isn't drawn and can't hide it by depth: add
// LampGlowOcclusion (physics line of sight) to the same object, which drives _Visibility.
Shader "Custom/LampGlow"
{
    Properties
    {
        [HDR] _Color ("Glow colour", Color) = (1.0, 0.78, 0.42, 1)
        _Size ("Size at the lamp (m)", Float) = 0.9
        _DistGrow ("Extra size per metre of distance", Float) = 0.01
        _Core ("Bright core strength", Range(0,3)) = 1.2
        _Halo ("Soft halo strength", Range(0,2)) = 0.55
        _MaxDist ("Visible up to (m)", Float) = 50
        _Curve ("Fade curve (1 = linear, higher = stays faint longer then rises late)", Range(1,4)) = 2.2
        _Pull ("Pull toward camera (m)", Float) = 0.12
        _Visibility ("Line-of-sight visibility (set by LampGlowOcclusion)", Range(0,1)) = 1
        _Directional ("Only seen from the front (0 = all around, 1 = front only; front = the object's +Z)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Size, _DistGrow, _Core, _Halo, _MaxDist, _Curve, _Pull, _Visibility, _Directional;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 centre = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 toCam = _WorldSpaceCameraPos - centre;
                float d = length(toCam);
                float3 dirToCam = toCam / max(d, 0.001);
                // The game camera's far clip is short (~19 m), so a far lamp would be clipped away. Slide the glow along the line of
                // sight to just inside the far plane and shrink it to the same apparent size, so it still shows up as a distant light.
                float dShow = min(d, _ProjectionParams.z * 0.9);
                float squeeze = dShow / max(d, 0.001);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float size = (_Size + d * _DistGrow) * squeeze;
                float3 wp = _WorldSpaceCameraPos - dirToCam * dShow + dirToCam * min(_Pull, dShow * 0.5)
                          + (right * v.vertex.x + up * v.vertex.y) * size;
                o.pos = UnityWorldToClipPos(float4(wp, 1));
                o.uv = v.uv;
                // headlights etc.: bright looking into them, gone from the side and behind (front = the object's local +Z)
                float3 lampFwd = normalize(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                float facing = dot(lampFwd, dirToCam);
                float aspect = lerp(1.0, smoothstep(0.1, 0.85, facing), _Directional);
                o.fade = pow(saturate(1.0 - d / _MaxDist), _Curve) * _Visibility * aspect;   // 0 at _MaxDist, rising smoothly as you get closer; _Visibility drops to 0 when a wall/hill is in the way
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;
                float core = exp(-r * r * 26.0) * _Core;
                float halo = pow(saturate(1.0 - r), 2.5) * _Halo;
                return fixed4(_Color.rgb * (core + halo) * i.fade, 1);
            }
            ENDCG
        }
    }
}

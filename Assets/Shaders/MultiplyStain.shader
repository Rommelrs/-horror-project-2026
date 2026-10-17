// Blood / dirt stain that darkens-and-tints whatever is behind it (multiply), so the surface's own texture shows through.
// Texture rgb = tint * 0.5 (0.5 grey = no change), alpha = stain mask.
// It is drawn in the transparent queue, i.e. AFTER the Enviro fog image effect (which only fogs opaque geometry), so the
// tint is applied to the final fogged colour - a heavy fog no longer washes the stain out. To keep it sitting in the fog
// at a distance it fades out between _FadeStart and _FadeEnd metres from the camera.
// UVs are world-projected so any mesh can carry the same stain (wall face, the inner sides of a hole, the floor):
//   _Projection 0 = wall facing +Z : u = (Rect.z - world.x) / width, v = (world.y - Rect.y) / height
//   _Projection 1 = floor          : u = (Rect.z - world.x) / width, v = (Rect.w - world.z) / depth   (Rect = minX, minZ, maxX, maxZ)
//   _Projection 2 = mesh UVs
Shader "Custom/MultiplyStain"
{
    Properties
    {
        _MainTex ("Tint (rgb*2) + Mask (a)", 2D) = "gray" {}
        _Power ("Darkness (tint power)", Range(0.25, 4)) = 1
        _Strength ("Strength", Range(0, 1)) = 1
        [Enum(WallZ,0,FloorY,1,MeshUV,2)] _Projection ("Projection", Float) = 2
        _Rect ("World Rect (minA, minB, maxA, maxB)", Vector) = (0,0,1,1)
        _FadeStart ("Distance Fade Start", Float) = 7
        _FadeEnd ("Distance Fade End", Float) = 18
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Power, _Strength, _Projection, _FadeStart, _FadeEnd;
            float4 _Rect;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float w = max(_Rect.z - _Rect.x, 1e-4), h = max(_Rect.w - _Rect.y, 1e-4);
                float2 uvWall = float2((_Rect.z - wp.x) / w, (wp.y - _Rect.y) / h);
                float2 uvFloor = float2((_Rect.z - wp.x) / w, (_Rect.w - wp.z) / h);   // v = 1 at the minZ (wall) side
                float2 uvMesh = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.uv = _Projection < 0.5 ? uvWall : (_Projection < 1.5 ? uvFloor : uvMesh);
                float d = distance(wp, _WorldSpaceCameraPos);
                o.fade = 1.0 - saturate((d - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                half inside = step(0.0, i.uv.x) * step(i.uv.x, 1.0) * step(0.0, i.uv.y) * step(i.uv.y, 1.0);
                fixed3 tint = pow(saturate(t.rgb * 2.0), _Power);
                return fixed4(lerp(fixed3(1, 1, 1), tint, t.a * _Strength * i.fade * inside), 1);
            }
            ENDCG
        }
    }
}

// For a Unity Projector looking straight down: lays a wet/muddy decal over whatever is underneath (terrain, paving, steps) so it
// follows the shape of the ground. Pass 1 darkens the surface by the wetness map (wet ground keeps its own detail, just darker);
// pass 2 blends the muck colour on top. Only surfaces facing up receive it (fades out on walls/risers so it doesn't smear down them).
// Drawn in the opaque range (Geometry+460) so Enviro's fog covers it like the ground itself. Muck colour is not lit by the scene;
// _Brightness matches it to the daylight on the ground.
Shader "Custom/ProjectorWetDecal"
{
    Properties
    {
        _MainTex ("Decal (RGB colour, A coverage)", 2D) = "white" {}
        _WetMap ("Wetness (R)", 2D) = "black" {}
        _WetDarken ("Soaked surface brightness (multiply)", Range(0,1)) = 0.5
        _Brightness ("Muck brightness", Range(0,2)) = 0.9
        _Opacity ("Opacity", Range(0,1)) = 1
        _UpFacing ("Only on surfaces facing up (min dot)", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Geometry+460" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Offset -1, -1

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _WetMap;
        float4x4 unity_Projector;
        float4x4 unity_ProjectorClip;      // x = 0 at the near plane .. 1 at the far plane
        half _WetDarken, _Brightness, _Opacity, _UpFacing;
        struct v2f { float4 pos : SV_POSITION; float4 uvp : TEXCOORD0; float up : TEXCOORD1; float depth : TEXCOORD3; UNITY_FOG_COORDS(2) };
        v2f vert (appdata_base v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uvp = mul(unity_Projector, v.vertex);
            o.up = UnityObjectToWorldNormal(v.normal).y;
            o.depth = mul(unity_ProjectorClip, v.vertex).x;
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
        }
        // projected UV + mask: inside the projector rectangle and facing up
        float2 ProjUV (v2f i, out half mask)
        {
            float2 uv = i.uvp.xy / i.uvp.w;
            mask = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1) * step(0, i.uvp.w);
            // only between the near and far planes: an orthographic projection would otherwise also land on surfaces above it (a roof)
            mask *= step(0, i.depth) * step(i.depth, 1);
            mask *= smoothstep(_UpFacing - 0.15, _UpFacing + 0.1, i.up) * _Opacity;
            return uv;
        }
        ENDCG

        Pass    // soak
        {
            Blend DstColor Zero
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                half m; float2 uv = ProjUV(i, m);
                half w = tex2D(_WetMap, uv).r * m;
                return fixed4(lerp(1.0.xxx, _WetDarken.xxx, w), 1);
            }
            ENDCG
        }

        Pass    // muck
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            fixed4 frag (v2f i) : SV_Target
            {
                half m; float2 uv = ProjUV(i, m);
                fixed4 c = tex2D(_MainTex, uv);
                c.rgb *= _Brightness;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return fixed4(c.rgb, c.a * m);
            }
            ENDCG
        }
    }
}

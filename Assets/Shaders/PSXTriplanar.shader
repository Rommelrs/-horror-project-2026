// Chunky PSX-style surface for blocky props: the texture is mapped box-style in OBJECT space at a fixed number of tiles per metre
// (the part's scale is taken into account), so a 5 m wall and a 0.2 m bar get the same texel size and nothing looks stretched.
// Use it with point-filtered, low-res textures. Opaque, so Enviro fog covers it like the rest of the world.
Shader "Custom/PSXTriplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Texture (low-res, point filtered)", 2D) = "white" {}
        _TilesPerMeter ("Tiles per metre", Float) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _TilesPerMeter;

        struct Input
        {
            float3 sizedPos;      // object-space position times the part's scale, i.e. metres
            float3 objNormal;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
            o.sizedPos = v.vertex.xyz * scale;
            o.objNormal = v.normal;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = abs(IN.objNormal);
            float2 uv = (n.x > n.y && n.x > n.z) ? IN.sizedPos.zy : (n.y > n.z ? IN.sizedPos.xz : IN.sizedPos.xy);
            fixed4 c = tex2D(_MainTex, uv * _TilesPerMeter) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

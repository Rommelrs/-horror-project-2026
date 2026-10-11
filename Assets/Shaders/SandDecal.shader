// Alpha-blended ground decal (sand drift, drag marks, footprints). Lit like the ground it lies on, and drawn at queue
// Geometry+450 - i.e. BEFORE Enviro Lite's opaque fog pass (which fogs everything up to queue 2500) - so the decal is
// fogged exactly like the floor under it instead of staying crisp through the dust the way queue-3000 materials do.
// The queue lives in the shader, so it can't be reset by the Standard shader's material inspector.
Shader "Custom/SandDecal"
{
    Properties
    {
        _Color ("Tint (alpha scales opacity)", Color) = (1,1,1,1)
        _MainTex ("Decal (RGBA)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+450" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off
        Offset -1, -1

        CGPROGRAM
        #pragma surface surf Lambert alpha:fade noshadow nolightmap nometa
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input { float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Transparent/VertexLit"
}

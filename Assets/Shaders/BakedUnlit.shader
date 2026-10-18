// Unlit texture x colour x vertex colour - for small set pieces whose lighting is baked into the vertex colours
// (e.g. the Emerald Salon stairwell behind the glass door, which must not use real lights that would leak outside).
// Opaque, so Enviro's fog still applies. The render queue is set per material (the stairwell uses 1899 so it draws
// before Custom/PortalDepthMask).
Shader "Custom/BakedUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0, 3)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float _Brightness;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color * i.color;
                return fixed4(c.rgb * _Brightness, 1);
            }
            ENDCG
        }
    }
}

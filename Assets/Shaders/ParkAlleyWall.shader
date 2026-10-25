// The park back alley's white rendered walls (the user's alley photos): mapped by world position so the plaster comes out
// the same size on every wall whatever its UVs, tiled up the full height of the buildings (_PanelWidth square). A painted
// lower band (the photos' grey dado, _BandHeight 0 = none) and grime / rising damp along the foot (_FootTex alpha, its
// u along the wall) on top. Lambert + fog like the rest of the game.
Shader "Custom/ParkAlleyWall"
{
    Properties
    {
        _MainTex ("Plaster (tiles both ways)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PanelWidth ("Plaster tile size (m)", Float) = 3.2
        _BaseY ("Ground height (world Y)", Float) = 2.8
        _BandHeight ("Painted lower band height (m, 0 = none)", Float) = 0
        _BandColor ("Painted lower band tint", Color) = (0.86,0.86,0.86,1)
        _FootTex ("Foot grime (alpha = amount)", 2D) = "black" {}
        _FootHeight ("Foot grime height (m)", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _FootTex;
        fixed4 _Color, _BandColor;
        float _PanelWidth, _BaseY, _BandHeight, _FootHeight;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 n = normalize(IN.worldNormal);
            float along = abs(n.x) > abs(n.z) ? IN.worldPos.z : IN.worldPos.x;
            float h = IN.worldPos.y - _BaseY;
            fixed4 c = tex2D(_MainTex, float2(along, IN.worldPos.y) / _PanelWidth);
            // the band's top is a painted line: a hair darker right at it
            if (h < _BandHeight) c.rgb *= _BandColor.rgb * (h > _BandHeight - 0.02 ? 0.92 : 1.0);
            fixed4 f = tex2D(_FootTex, float2(along / _PanelWidth, saturate(h / _FootHeight)));
            f.a *= step(h, _FootHeight);
            c.rgb = lerp(c.rgb, f.rgb, f.a);
            c *= _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

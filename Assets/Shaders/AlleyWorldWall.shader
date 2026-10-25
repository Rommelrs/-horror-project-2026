// Alley walls mapped by world position instead of their (stretched, atlas-scrap) UVs: one texture panel per
// _PanelWidth metres along the wall (shifted per wall line) and _Height metres up from _BaseY (the ground), so the plaster grain, the pilaster
// spacing and the peeled band at the foot come out the same size on every wall however it is scaled. Wall tops get the
// plain plaster from the top of the texture, a little darker. Lambert + fog, for the low-fi look of the rest of the game.
Shader "Custom/AlleyWorldWall"
{
    Properties
    {
        _MainTex ("Wall panel (v = 0 at the foot)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BaseY ("Ground height (world Y)", Float) = 1.45
        _PanelWidth ("Texture width (m)", Float) = 6.4
        _Height ("Texture height (m)", Float) = 3.6
        _TopTint ("Wall top tint", Color) = (0.85,0.82,0.78,1)
        [Toggle] _ShiftFromUV ("Shift from mesh UV.x (pillars: their wall's shift)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _TopTint;
        float _BaseY, _PanelWidth, _Height, _ShiftFromUV;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 n = normalize(IN.worldNormal);
            fixed4 c;
            if (abs(n.y) > 0.5)
            {
                float2 uv = frac(IN.worldPos.xz / _PanelWidth) * float2(0.8, 0.15) + float2(0.1, 0.8);
                c = tex2D(_MainTex, uv) * _TopTint;
            }
            else
            {
                float along = abs(n.x) > abs(n.z) ? IN.worldPos.z : IN.worldPos.x;
                // shifted by where the wall line stands, so two walls of the same style don't show the same decay; a
                // pillar stands proud of its wall, so it carries its wall's shift in its UVs instead
                float perp = abs(n.x) > abs(n.z) ? IN.worldPos.x : IN.worldPos.z;
                along += (_ShiftFromUV > 0.5 ? IN.uv_MainTex.x : frac(perp * 0.731)) * _PanelWidth;
                float v = clamp((IN.worldPos.y - _BaseY) / _Height, 0.002, 0.998);
                c = tex2D(_MainTex, float2(along / _PanelWidth, v));
            }
            c *= _Color;
            // pillars: uv.y runs 0..1 across the front face (0 on the sides) - a dark seam where they meet the panels and
            // shaded sides, so they read as standing proud like on the photos
            if (_ShiftFromUV > 0.5) c.rgb *= lerp(0.7, 1.0, saturate(min(IN.uv_MainTex.y, 1 - IN.uv_MainTex.y) / 0.1));
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

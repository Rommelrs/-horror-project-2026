// Invisible "hole" for a fake interior seen through a doorway (e.g. the Emerald Salon stairwell), without cutting the
// building mesh. Order of drawing:
//   1. the interior (Custom/BakedUnlit with render queue 1899) - it sits behind the building wall
//   2. this mask quad in the doorway (queue 1900): writes depth only, no colour, so the interior colour stays
//   3. the building wall (queue 2000): behind the mask in the doorway, so it fails the depth test there and the interior
//      shows through; everywhere else the wall is in front of the interior and covers it as usual
// Anything in front of the doorway (player, enemies) is drawn later and still covers it normally.
Shader "Custom/PortalDepthMask"
{
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert (float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }
    }
}

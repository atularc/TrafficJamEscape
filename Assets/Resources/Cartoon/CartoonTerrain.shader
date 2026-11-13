Shader "Traffic Jam/Cartoon Terrain"
{
    Properties
    {
        _MainTex ("Terrain Pattern", 2D) = "white" {}
        _RimColor ("Curb", Color) = (1, 1, 1, 1)
        _SideColor ("Skirt", Color) = (.5, .5, .5, 1)
        _TileVariation ("Tile Variation", Vector) = (0, 0, 1, 0)
        _RoadColor ("Road Color", Color) = (1, 1, 1, 1)
        _Road ("Road Surface", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert addshadow
        sampler2D _MainTex;
        fixed4 _RimColor;
        fixed4 _SideColor;
        fixed4 _RoadColor;
        half _Road;
        float4 _TileVariation;
        struct Input
        {
            float3 worldPos;
            float4 color : COLOR;
        };

        void surf(Input input, inout SurfaceOutput output)
        {
            float2 uv = input.worldPos.xz * lerp(.59, 1.1, _Road) + _TileVariation.xy;
            fixed3 pattern = tex2D(_MainTex, uv).rgb * _TileVariation.z;
            half mineral = lerp(.88, 1.03, dot(pattern, half3(.25, .60, .15)));
            half3 tile = ((_SideColor.rgb * input.color.r + _RimColor.rgb * input.color.g) * mineral
                + pattern * input.color.b) * input.color.a;
            half3 asphalt = _RoadColor.rgb * (pattern * 1.42);
            output.Albedo = lerp(tile, asphalt, _Road) * .84;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

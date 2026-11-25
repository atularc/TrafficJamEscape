Shader "Traffic Jam/Outline"
{
    // Cartoon outline: draws the back faces pushed out along the normal in a flat colour.
    // Used as an extra material on the animal parts so the game pieces stand out from the scenery.
    Properties
    {
        _Color ("Outline Color", Color) = (.13, .12, .24, 1)
        _Width ("Width (world units)", Float) = .01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 normal = normalize(UnityObjectToWorldNormal(v.normal));
                o.pos = UnityWorldToClipPos(world + normal * _Width);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}

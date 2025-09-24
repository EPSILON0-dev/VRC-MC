Shader "Minecraft/UnlitBlock"
{
    Properties
    {
        _AtlasTex ("Texture", 2D) = "white" {}
        _BlockTex ("Block Texture", 2D) = "white" {}
        _Index ("Chunk Index", float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                float3 localPos : TEXCOORD2;
            };

            sampler2D _AtlasTex;
            float4 _AtlasTex_ST;
            sampler2D _BlockTex;
            float4 _BlockTex_ST;
            float _Index;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                o.uv = TRANSFORM_TEX(v.uv, _AtlasTex);
                o.localPos = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Find the position of the voxel
                i.localPos -= i.normal * 0.2;
                i.localPos = floor(i.localPos);

                // Sample the block texture to get the block ID
                float xPos = (i.localPos.x) + 
                            (i.localPos.z) * 8.0 +
                            (i.localPos.y % 2) * 8.0 * 8.0;
                float yPos = floor(i.localPos.y / 2.0) + _Index * 4.0;
                float2 blockUV = float2((xPos + 0.5) / 128.0, (yPos + 0.5) / 128.0);
                float4 blockSample = tex2D(_BlockTex, blockUV);
                float blockIndex = floor(blockSample.r * 4.0 + 0.5);

                float2 uvOffset = float2(fmod(blockIndex, 2.0), floor(blockIndex / 2.0 - 0.1)) / 2.0;
                float2 uvFinal = i.uv / 2.0 + uvOffset;

                float4 atlasSample = tex2D(_AtlasTex, uvFinal);

                fixed4 col = atlasSample;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}

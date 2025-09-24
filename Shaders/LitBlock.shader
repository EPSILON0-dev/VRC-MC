Shader "Minecraft/LitBlock"
{
    Properties
    {
        _AtlasTex ("Texture", 2DArray) = "" {}
        _BlockTex ("Block Texture", 2D) = "white" {}
        _Index ("Chunk Index", float) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Color ("Color Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma vertex vert
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        TEXTURE2D_ARRAY(_AtlasTex);
        SAMPLER(_BlockTex);
        float _Index;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        struct Input
        {
            float2 uv_AtlasTex;
            float3 worldNormal;
            float3 worldPos;
            float3 localPos;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localPos = v.vertex.xyz; // vertex position in object (model) space
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Calculate local position from world position (approximate)
            float3 localPos = IN.localPos;

            // Adjust localPos by normal * 0.2 and floor it
            localPos -= IN.worldNormal * 0.2;
            localPos = floor(localPos);

            // Calculate block texture UVs
            float xPos = localPos.x + localPos.z * 8.0 + fmod(localPos.y, 2.0) * 64.0;
            float yPos = floor(localPos.y / 2.0) + _Index * 4.0;
            float2 blockUV = float2((xPos + 0.5) / 128.0, (yPos + 0.5) / 128.0);

            // Sample block texture to get block index
            float4 blockSample = tex2D(_BlockTex, blockUV);
            float blockIndex = floor(blockSample.r * 16.0 + 0.5);

            // Sample atlas texture
            fixed4 atlasSample = tex2D(_AtlasTex, uvFinal);

            // Apply color tint
            fixed4 c = atlasSample * _Color;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

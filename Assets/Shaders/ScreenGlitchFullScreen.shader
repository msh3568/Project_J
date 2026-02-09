Shader "ScreenGlitchFullScreen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ScreenGlitch"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float _GlitchStrength;
            float _GlitchHorizontal;
            float _GlitchBlockSize;
            float _GlitchLineJitter;
            float _GlitchColorSplit;
            float _TVPower;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;\r\n                float power = saturate(_TVPower);\r\n                if (power <= 0.0001 && _GlitchStrength <= 0.0001)\r\n                {\r\n                    power = 1.0;\r\n                }\r\n                uv.y = (uv.y - 0.5) * power + 0.5;
                float strength = _GlitchStrength;
                if (strength <= 0.0001)
                {
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                }

                float blockSize = max(_GlitchBlockSize, 4.0);
                float2 blockUV = floor(uv * blockSize) / blockSize;
                float lineNoise = Hash21(float2(_Time.y * _GlitchLineJitter, blockUV.y));
                float hOffset = (lineNoise - 0.5) * _GlitchHorizontal * strength;
                uv.x += hOffset;

                float2 rgbOffset = float2(_GlitchColorSplit * strength, 0.0);
                float r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv + rgbOffset).r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv - rgbOffset).b;
                return half4(r, g, b, 1);
            }
            ENDHLSL
        }
    }
}


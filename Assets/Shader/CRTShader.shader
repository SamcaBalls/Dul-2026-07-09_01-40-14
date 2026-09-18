Shader "Custom/Simple CRT URP"
{
    Properties
    {
        [Header(Textures)]
        _DecalTex ("Decal Texture", 2D) = "black" {}
        _FilmDirtTex ("Dirt Texture", 2D) = "black" {}

        [Header(CRT Toggles)]
        [Toggle] _ScanlineOnOff ("Scanlines", Int) = 1
        [Toggle] _ChromaticAberrationOnOff ("Chromatic Aberration", Int) = 1
        [Toggle] _WhiteNoiseOnOff ("White Noise", Int) = 0
        [Toggle] _MonochormeOnOff ("Monochrome", Int) = 0
        [Toggle] _FilmDirtOnOff ("Film Dirt", Int) = 0
        [Toggle] _MultipleGhostOnOff ("Multiple Ghost", Int) = 0
        [Toggle] _SlippageOnOff ("Slippage", Int) = 0
        [Toggle] _LetterBoxOnOff ("Letter Box", Int) = 0

        [Header(Settings)]
        _ChromaticAberrationStrength("Chromatic Aberration Strength", Range(0, 0.05)) = 0.005
        _FlickeringStrength("Flickering Strength", Float) = 0.005
        _FlickeringCycle("Flickering Cycle", Float) = 10
        _SlippageStrength("Slippage Strength", Float) = 0.05
        _SlippageInterval("Slippage Interval", Float) = 5
        _SlippageScrollSpeed("Slippage Scroll Speed", Float) = 2
        _SlippageNoiseOnOff("Slippage Noise (1=On)", Float) = 1
        _SlippageSize("Slippage Size", Float) = 5
        _MultipleGhostStrength("Multiple Ghost Strength", Float) = 0.02
        _ScreenJumpLevel("Screen Jump Level", Float) = 0
        _LetterBoxType("Letter Box Type (0=Black, 1=Blur)", Int) = 0
    }
    SubShader
    {
        // DŸLEéIT…: U post-processingu musÌme zak·zat Z-Test, jinak m˘ûe b˝t obraz neviditeln˝!
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CRT Pass"

            HLSLPROGRAM
            // Pouûijeme rovnou Vertex shader z URP knihovny Blit (nebudeme ps·t vlastnÌ)
            #pragma vertex Vert
            #pragma fragment frag

            // Knihovny pro URP a VR (Single Pass Stereo)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_DecalTex);
            SAMPLER(sampler_DecalTex);

            TEXTURE2D(_FilmDirtTex);
            SAMPLER(sampler_FilmDirtTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DecalTex_ST;
                int _DecalTexOnOff;
                float2 _DecalTexPos;
                float2 _DecalTexScale;

                float4 _FilmDirtTex_ST;
                int _FilmDirtOnOff;

                int _WhiteNoiseOnOff;
                int _ScanlineOnOff;
                int _MonochormeOnOff;

                int _LetterBoxOnOff;
                int _LetterBoxEdgeBlur;
                int _LetterBoxType;

                float _ScreenJumpLevel;
                float _FlickeringStrength;
                float _FlickeringCycle;

                int _SlippageOnOff;
                float _SlippageStrength;
                float _SlippageInterval;
                float _SlippageScrollSpeed;
                float _SlippageNoiseOnOff;
                float _SlippageSize;

                float _ChromaticAberrationStrength;
                int _ChromaticAberrationOnOff;

                int _MultipleGhostOnOff;
                float _MultipleGhostStrength;
            CBUFFER_END

            float GetRandom(float x)
            {
                return frac(sin(dot(x, float2(12.9898, 78.233))) * 43758.5453);
            }

            float EaseIn(float t0, float t1, float t)
            {
                return 2.0 * smoothstep(t0, 2.0 * t1 - t0, t);
            }

            // Fragment shader vyuûÌv· "Varyings input" ze standardnÌ Blit knihovny
            half4 frag (Varyings input) : SV_Target
            {
                // Zajiöùuje, ûe shader pobÏûÌ spr·vnÏ v obou oËÌch VR headsetu
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                /////Jump noise
                uv.y = frac(uv.y + _ScreenJumpLevel);

                /////Flickering
                float flickeringNoise = GetRandom(_Time.y);
                float flickeringMask = pow(abs(sin(uv.y * _FlickeringCycle + _Time.y)), 10.0);
                uv.x = uv.x + (flickeringNoise * _FlickeringStrength * flickeringMask); 

                /////Slippage
                float scrollSpeed = _Time.x * _SlippageScrollSpeed;
                float slippageMask = pow(abs(sin(uv.y * _SlippageInterval + scrollSpeed)), _SlippageSize);
                float stepMask = round(sin(uv.y * _SlippageInterval + scrollSpeed - 1.0));
                uv.x = uv.x + (_SlippageNoiseOnOff * _SlippageStrength * slippageMask * stepMask) * _SlippageOnOff; 

                /////Chromatic Aberration (PouûÌv·me _BlitTexture a makro s _X pro VR!)
                float red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x - _ChromaticAberrationStrength * _ChromaticAberrationOnOff, uv.y)).r;
                float green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x, uv.y)).g;
                float blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, float2(uv.x + _ChromaticAberrationStrength * _ChromaticAberrationOnOff, uv.y)).b; 
                half4 color = half4(red, green, blue, 1.0);

                /////Multiple Ghost
                half4 ghost1st = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(1.0, 0.0) * _MultipleGhostStrength * _MultipleGhostOnOff);
                half4 ghost2nd = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(1.0, 0.0) * _MultipleGhostStrength * 2.0 * _MultipleGhostOnOff);
                color = color * 0.8 + ghost1st * 0.15 + ghost2nd * 0.05;

                /////Film dirt
                float2 pp = -1.0 + 2.0 * uv;
                float time = _Time.x;
                float aaRad = 0.1;
                float2 nseLookup2 = pp + time * 1000.0;
                float3 nse2 =
                    SAMPLE_TEXTURE2D(_FilmDirtTex, sampler_FilmDirtTex, 0.1 * nseLookup2.xy).xyz +
                    SAMPLE_TEXTURE2D(_FilmDirtTex, sampler_FilmDirtTex, 0.01 * nseLookup2.xy).xyz +
                    SAMPLE_TEXTURE2D(_FilmDirtTex, sampler_FilmDirtTex, 0.004 * nseLookup2.xy).xyz;
                
                float thresh = 0.6;
                float mul1 = smoothstep(thresh - aaRad, thresh + aaRad, nse2.x);
                float mul2 = smoothstep(thresh - aaRad, thresh + aaRad, nse2.y);
                float mul3 = smoothstep(thresh - aaRad, thresh + aaRad, nse2.z);
                
                float seed = SAMPLE_TEXTURE2D(_FilmDirtTex, sampler_FilmDirtTex, float2(time * 0.35, time)).x;
                float result = clamp(0.0, 1.0, seed + 0.7);
                result += 0.06 * EaseIn(19.2, 19.4, time);

                float band = 0.05;
                if(_FilmDirtOnOff == 1)
                {
                    if( 0.3 < seed && 0.3 + band > seed ) color *= mul1 * result;
                    else if( 0.6 < seed && 0.6 + band > seed ) color *= mul2 * result;
                    else if( 0.9 < seed && 0.9 + band > seed ) color *= mul3 * result;
                }

                /////Letter box
                float band_uv = fmod(_BlitTexture_TexelSize.z, 640.0) / _BlitTexture_TexelSize.z / 2.0;
                if(uv.x < band_uv || 1.0 - band_uv < uv.x)
                {
                    float pi = 6.28318530718; 
                    float directions = 16.0; 
                    float quality = 3.0; 
                    
                    half4 samplingColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                    
                    for(float d = 0.0; d < pi; d += pi / directions)
                    {
                        for(float i = 1.0 / quality; i <= 1.0; i += 1.0 / quality)
                        {
                            samplingColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(cos(d), sin(d)) * 0.015 * i);       
                        }
                    }
                    samplingColor /= quality * directions - 15.0;
                    
                    if(_LetterBoxOnOff == 1) color = color;
                    else if(_LetterBoxType == 0) color = half4(0.0, 0.0, 0.0, 1.0);
                    else if(_LetterBoxType == 1) color = samplingColor;
                }

                /////White noise
                if(_WhiteNoiseOnOff == 1)
                {
                    float noiseVal = frac(sin(dot(uv, float2(12.9898, 78.233)) + _Time.x) * 43758.5453);
                    return half4(noiseVal, noiseVal, noiseVal, 1.0); 
                }

                /////Decal texture
                float2 decaluv = uv * _DecalTex_ST.xy + _DecalTex_ST.zw;
                half4 decal = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, (decaluv - _DecalTexPos) * _DecalTexScale) * _DecalTexOnOff;
                color = color * (1.0 - decal.a) + decal;

                /////Scanline
                float scanline = sin((uv.y + _Time.x) * 800.0) * 0.04;
                color -= scanline * _ScanlineOnOff;

                //////Monochorome
                if(_MonochormeOnOff == 1)
                {
                    color.xyz = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                }

                return color;
            }
            ENDHLSL
        }
    }
}
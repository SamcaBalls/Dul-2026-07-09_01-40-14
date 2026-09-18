Shader "Custom/TriplanarPixelated_Lit"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Intensity", Range(0.0, 5.0)) = 1.0
        _TextureScale ("Texture Scale", Float) = 1.0
        _Sharpness ("Blend Sharpness", Range(1, 64)) = 8.0
        _AmbientColor ("Fallback Ambient", Color) = (0.05, 0.05, 0.05, 1.0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Makra pro Single Pass Instanced (nutné pro správné svícení ve VR)
            #pragma multi_compile_instancing
            
            // Komplet URP makra (zapíná Point svìtla, Spot svìtla a Forward+ render)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _TextureScale;
                float _Sharpness;
                float _NormalScale;
                float4 _AmbientColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Inicializace pro VR
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.worldNormal = normalInput.normalWS;

                return output;
            }

            float3 UnpackTriplanarNormal(half4 rawNormal, float scale)
            {
                return UnpackNormalScale(rawNormal, scale);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); // VR setup pro fragment

                float3 worldPos = input.worldPos * _TextureScale;
                float3 geoNormal = normalize(input.worldNormal);

                // 1. Váhy
                float3 blendWeights = pow(abs(geoNormal), _Sharpness);
                blendWeights /= max(0.00001, blendWeights.x + blendWeights.y + blendWeights.z);

                // 2. Albedo
                half4 colX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.yz);
                half4 colY = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.xz);
                half4 colZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, worldPos.xy);
                half4 finalAlbedo = colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;

                // 3. Normal Map
                half4 bumpX = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.yz);
                half4 bumpY = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.xz);
                half4 bumpZ = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, worldPos.xy);

                float3 normalX = UnpackTriplanarNormal(bumpX, _NormalScale);
                float3 normalY = UnpackTriplanarNormal(bumpY, _NormalScale);
                float3 normalZ = UnpackTriplanarNormal(bumpZ, _NormalScale);

                float3 worldNormalX = float3(0, normalX.zy);
                worldNormalX.x = normalX.x * sign(geoNormal.x);
                
                float3 worldNormalY = float3(normalY.x, 0, normalY.y);
                worldNormalY.y = normalY.z * sign(geoNormal.y);

                float3 worldNormalZ = float3(normalZ.xy, 0);
                worldNormalZ.z = normalZ.z * sign(geoNormal.z);

                float3 finalNormal = normalize(
                    worldNormalX * blendWeights.x +
                    worldNormalY * blendWeights.y +
                    worldNormalZ * blendWeights.z +
                    geoNormal
                );

                // 4. Výpoèet svìtel s podporou Forward+
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                
                half3 totalLighting = mainLight.color * saturate(dot(finalNormal, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                // PØÍPRAVA PRO FORWARD+ (Makro LIGHT_LOOP_BEGIN spoléhá na to, že existuje promìnná inputData)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.worldPos;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // KONTROLA _ADDITIONAL_LIGHTS I _FORWARD_PLUS
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    // ZÍSKÁNÍ SVÌTLA: Parametr nesmí být shadowCoord (patøí hlavnímu svìtlu), ale shadowMask.
                    Light addLight = GetAdditionalLight(lightIndex, input.worldPos, half4(1,1,1,1));
                    half NdotL = saturate(dot(finalNormal, addLight.direction));
                    totalLighting += addLight.color * (NdotL * addLight.distanceAttenuation * addLight.shadowAttenuation);
                LIGHT_LOOP_END
                #endif

                // Pøiètení Ambientního prostøedí (Global Illumination / Skybox / Fallback)
                half3 ambient = SampleSH(finalNormal) + _AmbientColor.rgb;
                totalLighting += ambient;

                return half4(finalAlbedo.rgb * totalLighting, finalAlbedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #pragma multi_compile_instancing // VR Shadow fix

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
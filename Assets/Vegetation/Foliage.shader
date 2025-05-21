Shader "Custom/Foliage" {
    Properties {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)

        // Billboarding and vertex inflation
        _Billboarding ("Billboarding", Range(0, 1)) = 0
        _Inflation ("Fluffy Scale", Range(-1, 3)) = 0

        // Subsurface Scattering
        _FresnelScale ("Fresnel Scale", Range(0, 5)) = 0.5
        _FresnelPower ("Fresnel Power", Range(0.25, 4)) = 0.25
        _FresnelColor ("Fresnel Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" }

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM

            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options nolightprobe nolightmap procedural:setup

            // Lighting and shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_SHADOWS 
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_CASCADE

            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic file
            #include "Foliage.hlsl"

            ENDHLSL
        }

        // Enable Shadow Casting
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ColorMask 0

            HLSLPROGRAM

            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options nolightprobe nolightmap procedural:setup

            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic file
            #include "Foliage.hlsl"

            ENDHLSL
        }

        // Enable Depth Priming (SSAO uses Depth as source)
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ColorMask 0
            ZWrite On
            
            HLSLPROGRAM
            
            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options nolightprobe nolightmap procedural:setup

            // Register keywords
            #pragma shader_feature _ALPHATEST_ON
            
            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic files
            #include "Foliage.hlsl"

            ENDHLSL
        }

        // Enable Depth Priming (when SSAO uses DepthNormals as source)
        Pass {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            Cull Off
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            
            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options nolightprobe nolightmap procedural:setup

            // Register keywords
            #pragma shader_feature _ALPHATEST_ON
            
            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic files
            #include "Foliage.hlsl"

            ENDHLSL
        }
    }
}

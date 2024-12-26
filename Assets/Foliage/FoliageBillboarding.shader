Shader "Custom/Foliage"
{
    Properties
    {
        [Header(Visuals)]
        [Space]
        _Color ("Color", Color) = (1,1,1,1)
        _LeafTexture ("Leaf Texture", 2D) = "white" {}

        [Header(Transformations)]
        [Space]
        _Billboarding ("Billboarding", Range(0.0, 1.0)) = 1.0
        _Scale ("Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="AlphaCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off    // Disable backface culling

            HLSLPROGRAM

            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // Enable GPU Instancing and GPU Generation
            #pragma multi_compile GPU_INSTANCING GPU_GENERATION

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

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
            #include "Billboard.hlsl"

            ENDHLSL
        }

        // Enable Shadow Casting
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0 // Disable color writes
            Cull Off    // Disable backface culling

            HLSLPROGRAM

            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // Enable GPU Instancing and GPU Generation
            #pragma multi_compile GPU_INSTANCING GPU_GENERATION

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            // Defines the shadow caster pass in the logic file
            #define SHADOW_CASTER_PASS

            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic file
            #include "Billboard.hlsl"

            ENDHLSL
        }

        // Enable Depth Priming (SSAO uses Depth as source)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            
            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // Enable GPU Instancing and GPU Generation
            #pragma multi_compile GPU_INSTANCING GPU_GENERATION

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            // Register keywords
            #pragma shader_feature _ALPHATEST_ON
            
            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic files
            #include "Billboard.hlsl"

            ENDHLSL
        }

        // Enable Depth Priming (when SSAO uses DepthNormals as source)
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            
            // Signal this shader requires a compute buffer
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 5.0

            // Enable GPU Instancing and GPU Generation
            #pragma multi_compile GPU_INSTANCING GPU_GENERATION

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            // Register keywords
            #pragma shader_feature _ALPHATEST_ON
            
            // Register functions
            #pragma vertex vert
            #pragma fragment frag

            // Include logic files
            #include "Billboard.hlsl"

            ENDHLSL
        }
    }
}

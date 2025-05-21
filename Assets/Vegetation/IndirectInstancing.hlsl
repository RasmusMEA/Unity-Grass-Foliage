#ifndef INDIRECTINSTANCING_HLSL
#define INDIRECTINSTANCING_HLSL

// Custom version of UnityIndirect.cginc
#include "MultiDrawIndirect.hlsl"

// Indirect instancing data
StructuredBuffer<float4x4> _ObjectToWorld;

void setup() {
    #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

    InitIndirectDrawArgs(0);
    uint _InstanceID = GetIndirectInstanceID_Base(unity_InstanceID);

    // Get object to world matrix for the current instance
    unity_ObjectToWorld = _ObjectToWorld[_InstanceID];
    // unity_ObjectToWorld._41_42_43_44 = float4(0.0f, 0.0f, 0.0f, 1.0f);

    // Generate the inverse world to object matrix
    // https://discussions.unity.com/t/drawmeshinstancedindirect-example-comments-and-questions/648955/16
    float3x3 w2oRotation;
    w2oRotation[0] = unity_ObjectToWorld[1].yzx * unity_ObjectToWorld[2].zxy - unity_ObjectToWorld[1].zxy * unity_ObjectToWorld[2].yzx;
    w2oRotation[1] = unity_ObjectToWorld[0].zxy * unity_ObjectToWorld[2].yzx - unity_ObjectToWorld[0].yzx * unity_ObjectToWorld[2].zxy;
    w2oRotation[2] = unity_ObjectToWorld[0].yzx * unity_ObjectToWorld[1].zxy - unity_ObjectToWorld[0].zxy * unity_ObjectToWorld[1].yzx;

    float det = dot(unity_ObjectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -unity_ObjectToWorld._14_24_34);

    unity_WorldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    unity_WorldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    unity_WorldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    unity_WorldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);

    #endif
}

#endif // INDIRECTINSTANCING_HLSL
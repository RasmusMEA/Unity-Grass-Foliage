// Unity built-in shader source (UnityIndirect.cginc). Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)
#ifndef CUSTOM_MULTI_DRAW_INDIRECT_INCLUDED
#define CUSTOM_MULTI_DRAW_INDIRECT_INCLUDED

//Enforce indexed indirect rendering
#ifndef UNITY_INDIRECT_DRAW_ARGS
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#endif

// Command ID
uint unity_BaseCommandID;
uint GetCommandID(uint svDrawID) { return unity_BaseCommandID + svDrawID; }
#define unity_BaseCommandID Use_GetCommandID_function_instead_of_unity_BaseCommandID

// Indexed indirect draw
struct IndirectDrawIndexedArgs
{
    uint indexCountPerInstance;
    uint instanceCount;
    uint startIndex;
    uint baseVertexIndex;
    uint startInstance;
};
void GetIndirectDrawArgs(out IndirectDrawIndexedArgs args, ByteAddressBuffer argsBuffer, uint commandId)
{
    uint offset = commandId * 20;
    args.indexCountPerInstance = argsBuffer.Load(offset + 0);
    args.instanceCount = argsBuffer.Load(offset + 4);
    args.startIndex = argsBuffer.Load(offset + 8);
    args.baseVertexIndex = argsBuffer.Load(offset + 12);
    args.startInstance = argsBuffer.Load(offset + 16);
}
uint GetIndirectInstanceCount(IndirectDrawIndexedArgs args) { return args.instanceCount; }
uint GetIndirectVertexCount(IndirectDrawIndexedArgs args) { return args.indexCountPerInstance; }
#if defined(SHADER_API_VULKAN)
uint GetIndirectInstanceID(IndirectDrawIndexedArgs args, uint svInstanceID) { return svInstanceID - args.startInstance; }
uint GetIndirectInstanceID_Base(IndirectDrawIndexedArgs args, uint svInstanceID) { return svInstanceID; }
#else
uint GetIndirectInstanceID(IndirectDrawIndexedArgs args, uint svInstanceID) { return svInstanceID; }
uint GetIndirectInstanceID_Base(IndirectDrawIndexedArgs args, uint svInstanceID) { return svInstanceID + args.startInstance; }
#endif
uint GetIndirectVertexID(IndirectDrawIndexedArgs args, uint svVertexID) { return svVertexID; }
uint GetIndirectVertexID_Base(IndirectDrawIndexedArgs args, uint svVertexID) { return svVertexID + args.startIndex; }


// Indirect draw ID accessors
#ifdef UNITY_INDIRECT_DRAW_ARGS
ByteAddressBuffer unity_IndirectDrawArgs;
static UNITY_INDIRECT_DRAW_ARGS globalIndirectDrawArgs;

void InitIndirectDrawArgs(uint svDrawID) { GetIndirectDrawArgs(globalIndirectDrawArgs, unity_IndirectDrawArgs, GetCommandID(svDrawID)); }
uint GetIndirectInstanceCount() { return GetIndirectInstanceCount(globalIndirectDrawArgs); }
uint GetIndirectVertexCount() { return GetIndirectVertexCount(globalIndirectDrawArgs); }
uint GetIndirectInstanceID(uint svInstanceID) { return GetIndirectInstanceID(globalIndirectDrawArgs, svInstanceID); }
uint GetIndirectInstanceID_Base(uint svInstanceID) { return GetIndirectInstanceID_Base(globalIndirectDrawArgs, svInstanceID); }
uint GetIndirectVertexID(uint svVertexID) { return GetIndirectVertexID(globalIndirectDrawArgs, svVertexID); }
uint GetIndirectVertexID_Base(uint svVertexID) { return GetIndirectVertexID_Base(globalIndirectDrawArgs, svVertexID); }
#endif

#endif // UNITY_INDIRECT_INCLUDED

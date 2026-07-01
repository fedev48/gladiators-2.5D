using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct GridBlueprintTag    : IComponentData { }

public struct FlowFieldMap : IComponentData
{
    public int    FlowFieldId;
    public float3 Destination;
}

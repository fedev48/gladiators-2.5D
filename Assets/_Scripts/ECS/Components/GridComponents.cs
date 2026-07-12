using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct GridBlueprintTag    : IComponentData { }

public struct FlowFieldMap : IComponentData
{
    public int FlowFieldId;
    public int DestinationCellIndex;
}

public struct FlowFieldPoolSingleton : IComponentData
{
    public NativeList<Entity> Pool;
}

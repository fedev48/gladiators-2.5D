using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public struct GridBlueprintTag    : IComponentData { }

public struct FlowFieldMap : IComponentData
{
    public int flowFieldId;
    public int destinationCellIndex;
}

public struct FlowFieldPoolSingleton : IComponentData
{
    public NativeList<Entity> pool;
}

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct HashedUnit
{
    public Entity entity;
    public float3 position;
    public byte   team;
}

public struct UnitSpatialHashComponents : IComponentData
{
    public NativeParallelMultiHashMap<int2, HashedUnit> hashMap;
    public JobHandle producerHandle;
}

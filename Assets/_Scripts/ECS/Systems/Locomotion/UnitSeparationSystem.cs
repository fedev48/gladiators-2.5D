using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(EntitiesPositionToHashSystem))]
[UpdateBefore(typeof(MoveSystem))]
partial struct UnitSeparationSystem : ISystem
{
    EntityQuery separationQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        separationQuery = SystemAPI.QueryBuilder()
        .WithAllRW<SeparationVelocity>()
        .WithAll<SeparationConfig, LocalTransform>()
        .Build();

        state.RequireForUpdate(separationQuery);
        state.RequireForUpdate<GridConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridConfig config = SystemAPI.GetSingleton<GridConfig>();
        UnitSpatialHashComponents spatialHash = state.EntityManager.GetComponentData<UnitSpatialHashComponents>(
            state.WorldUnmanaged.GetExistingUnmanagedSystem<EntitiesPositionToHashSystem>());

        CalculateSeparationJob separationJob = new CalculateSeparationJob
        {
            unitsPerGridHashMap = spatialHash.hashMap,
            gridConfig          = config
        };
        state.Dependency = separationJob.ScheduleParallel(
            JobHandle.CombineDependencies(state.Dependency, spatialHash.producerHandle));
    }
}

[BurstCompile]
partial struct CalculateSeparationJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int2, HashedUnit> unitsPerGridHashMap;
    public GridConfig gridConfig;

    void Execute(ref SeparationVelocity separationVector, in SeparationConfig separationConfig, in LocalTransform localTransform)
    {
        float3 escapeVector = float3.zero;
        int entitiesCount = 0;

        int2 centralCell = GridSystem.WorldPosToCoords(localTransform.Position, gridConfig);
        int  cellRadius  = (int)math.ceil(separationConfig.radius / gridConfig.cellSize);

        FixedList4096Bytes<int2> cells = GridSystem.GetSurroundingCells(centralCell, cellRadius);

        NativeList<HashedUnit> unitsBuffer = new NativeList<HashedUnit>(64, Allocator.Temp);
        EntitiesPositionToHashSystem.GetUnitsInCells(unitsPerGridHashMap, cells, ref unitsBuffer);

        foreach (HashedUnit other in unitsBuffer)
        {
            float2 distanceVector = other.position.xz - localTransform.Position.xz;
            float  distanceSq     = math.lengthsq(distanceVector);

            if (distanceSq <= 0.0001f || distanceSq > separationConfig.radius * separationConfig.radius) continue;

            float  distance       = math.sqrt(distanceSq);
            float  penetration    = (separationConfig.radius - distance) / separationConfig.radius;
            float2 push           = (distanceVector / distance) * penetration;

            entitiesCount++;
            escapeVector += new float3(push.x, 0, push.y);
        }

        unitsBuffer.Dispose();

        if (entitiesCount > 0) escapeVector = -escapeVector/entitiesCount;

        separationVector.value = escapeVector * separationConfig.strenght;
    }
}

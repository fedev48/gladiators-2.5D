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
            cellSize            = config.cellSize
        };
        state.Dependency = separationJob.ScheduleParallel(
            JobHandle.CombineDependencies(state.Dependency, spatialHash.producerHandle));
    }
}

[BurstCompile]
partial struct CalculateSeparationJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int2, HashedUnit> unitsPerGridHashMap;
    public float cellSize;

    void Execute(ref SeparationVelocity separationVector, in SeparationConfig separationConfig, in LocalTransform localTransform)
    {
        int2 entityCellCoords = (int2)math.floor(localTransform.Position.xz / cellSize);
        float3 escapeVector = float3.zero;
        int entitiesCount = 0;

        foreach (int2 neighbourCell in GridSystem.GetSurroundingCells(entityCellCoords, (int)math.ceil(separationConfig.radius/cellSize)))
        {
            foreach (HashedUnit otherUnit in unitsPerGridHashMap.GetValuesForKey(neighbourCell))
            {
                float2 distanceVector = otherUnit.position.xz - localTransform.Position.xz;
                float distanceSq = math.lengthsq(distanceVector);

                if (distanceSq > 0.0001f && distanceSq < separationConfig.radius*separationConfig.radius)
                {
                    entitiesCount++;
                    float distance    = math.sqrt(distanceSq);
                    float penetration = (separationConfig.radius - distance) / separationConfig.radius;
                    float2 push       = (distanceVector / distance) * penetration;
                    escapeVector += new float3(push.x, 0, push.y);
                }
            }
        }

        if (entitiesCount > 0) escapeVector = -escapeVector/entitiesCount;

        separationVector.value = escapeVector * separationConfig.strenght;
    }
}

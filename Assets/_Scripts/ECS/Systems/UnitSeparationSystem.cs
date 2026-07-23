using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(MoveSystem))]
partial struct UnitSeparationSystem : ISystem
{
    EntityQuery separationQuery;
    const int RESOLUTION_MULT = 2;

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
        float cellSize = config.cellSize/RESOLUTION_MULT;
        int unitCount = separationQuery.CalculateEntityCount();
        NativeParallelMultiHashMap<int2, LocalTransform> unitsPerGridHashMap = new (unitCount, state.WorldUpdateAllocator);

        PopulateUnitsHashMapJob populateJob = new PopulateUnitsHashMapJob
        {
            hashMapWriter = unitsPerGridHashMap.AsParallelWriter(),
            cellSize      = cellSize
        };
        state.Dependency = populateJob.ScheduleParallel(state.Dependency);

        CalculateSeparationJob separationJob = new CalculateSeparationJob
        {
            unitsPerGridHashMap = unitsPerGridHashMap,
            cellSize            = cellSize
        };
        state.Dependency = separationJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(SeparationVelocity), typeof(SeparationConfig))]
partial struct PopulateUnitsHashMapJob : IJobEntity
{
    public NativeParallelMultiHashMap<int2, LocalTransform>.ParallelWriter hashMapWriter;
    public float cellSize;

    void Execute(in LocalTransform localTransform)
    {
        int2 entityCellCoords = (int2)math.floor(localTransform.Position.xz / cellSize);
        hashMapWriter.Add(entityCellCoords, localTransform);
    }
}

[BurstCompile]
partial struct CalculateSeparationJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int2, LocalTransform> unitsPerGridHashMap;
    public float cellSize;

    void Execute(ref SeparationVelocity separationVector, in SeparationConfig separationConfig, in LocalTransform localTransform)
    {
        NativeList<LocalTransform> surroundingUnits = new NativeList<LocalTransform>(Allocator.Temp);
        int2 entityCellCoords = (int2)math.floor(localTransform.Position.xz / cellSize);
        float3 escapeVector = float3.zero;

        foreach (int2 neighbourCell in GridSystem.GetSurroundingCells(entityCellCoords, (int)math.ceil(separationConfig.radius/cellSize)))
        {
            if (!unitsPerGridHashMap.ContainsKey(neighbourCell)) continue;

            foreach(LocalTransform unitPos in unitsPerGridHashMap.GetValuesForKey(neighbourCell))
            {
                surroundingUnits.Add(unitPos);
            }
        }

        int entitiesCount=0;
        foreach(LocalTransform unitPos in surroundingUnits)
        {
            float2 distanceVector = unitPos.Position.xz - localTransform.Position.xz;
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

        if (entitiesCount > 0) escapeVector = -escapeVector/entitiesCount;

        separationVector.Value = escapeVector * separationConfig.speed;
    }
}

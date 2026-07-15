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
        .WithAllRW<SeparationVector>()
        .WithAll<UnitRadius, LocalTransform>()
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

    public static FixedList512Bytes<int2> GetSurroundingCells(int2 cellCoords, int amountOfCellsToCheck)
    {
        FixedList512Bytes<int2> surroundingCoords = new();
        int sideSize = amountOfCellsToCheck*2+1;

        for (int i = 0; i < sideSize*sideSize; i++)
        {
            int dx = (i % sideSize) - amountOfCellsToCheck;
            int dy = (i / sideSize) - amountOfCellsToCheck;
            surroundingCoords.Add(new int2(cellCoords.x + dx, cellCoords.y + dy));
        }

        return surroundingCoords;
    }
}

[BurstCompile]
[WithAll(typeof(SeparationVector), typeof(UnitRadius))]
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

    void Execute(ref SeparationVector separationVector, in UnitRadius unitRadius, in LocalTransform localTransform)
    {
        NativeList<LocalTransform> surroundingUnits = new NativeList<LocalTransform>(Allocator.Temp);
        int2 entityCellCoords = (int2)math.floor(localTransform.Position.xz / cellSize);
        float3 escapeVector = float3.zero;

        foreach (int2 neighbourCell in UnitSeparationSystem.GetSurroundingCells(entityCellCoords, (int)math.ceil(unitRadius.Value/cellSize)))
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
            if (distanceSq > 0.0001f && distanceSq < unitRadius.Value*unitRadius.Value)
            {
                entitiesCount++;
                float distance = math.sqrt(distanceSq);
                float2 push = (distanceVector / distance) / distance;
                escapeVector += new float3(push.x, 0, push.y);
            }
        }

        if (entitiesCount > 0) escapeVector = -escapeVector/entitiesCount;

        separationVector.Value = escapeVector;
    }
}


using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct EntitiesPositionToHashSystem : ISystem
{
    EntityQuery unitsQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        unitsQuery = SystemAPI.QueryBuilder().WithAll<UnitTag, Team, LocalTransform>().Build();

        state.EntityManager.AddComponent<UnitSpatialHashComponents>(state.SystemHandle);
        state.EntityManager.SetComponentData(state.SystemHandle, new UnitSpatialHashComponents
        {
            hashMap = new NativeParallelMultiHashMap<int2, HashedUnit>(1024, Allocator.Persistent)
        });

        state.RequireForUpdate<GridConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();
        float cellSize = gridConfig.cellSize; 
        UnitSpatialHashComponents spatialHash = state.EntityManager.GetComponentData<UnitSpatialHashComponents>(state.SystemHandle);
        spatialHash.hashMap.Clear();

        int unitCount = unitsQuery.CalculateEntityCount();
        if (spatialHash.hashMap.Capacity < unitCount) spatialHash.hashMap.Capacity = unitCount;

        PopulateUnitsHashMapJob populateJob = new PopulateUnitsHashMapJob
        {
            hashMapWriter = spatialHash.hashMap.AsParallelWriter(),
            cellSize      = cellSize
        };
        state.Dependency = populateJob.ScheduleParallel(state.Dependency);

        spatialHash.producerHandle = state.Dependency;
        state.EntityManager.SetComponentData(state.SystemHandle, spatialHash);
    }

    [BurstCompile]
    [WithAll(typeof(UnitTag))]
    [WithNone(typeof(DeathState))]
    partial struct PopulateUnitsHashMapJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2, HashedUnit>.ParallelWriter hashMapWriter;
        public float cellSize;

        void Execute(Entity entity, in LocalTransform localTransform, in Team team)
        {
            int2 entityCellCoords = GridSystem.WorldPosToCoords(localTransform.Position, cellSize);
            hashMapWriter.Add(entityCellCoords, new HashedUnit
            {
                entity   = entity,
                position = localTransform.Position,
                team     = team.value
            });
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        UnitSpatialHashComponents spatialHash = state.EntityManager.GetComponentData<UnitSpatialHashComponents>(state.SystemHandle);
        if (spatialHash.hashMap.IsCreated) spatialHash.hashMap.Dispose();
    }

    //fills units with everything standing in the given cells, so the caller must still filter by distance
    public static void GetUnitsInCells(in NativeParallelMultiHashMap<int2, HashedUnit> hashMap, in FixedList4096Bytes<int2> cells, ref NativeList<HashedUnit> units)
    {
        units.Clear();

        for (int i = 0; i < cells.Length; i++)
        {
            if (!hashMap.TryGetFirstValue(cells[i], out HashedUnit unit, out NativeParallelMultiHashMapIterator<int2> iterator)) continue;

            do units.Add(unit);
            while (hashMap.TryGetNextValue(out unit, ref iterator));
        }
    }
}

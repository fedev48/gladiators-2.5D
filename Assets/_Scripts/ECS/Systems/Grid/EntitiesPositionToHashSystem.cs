
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
    partial struct PopulateUnitsHashMapJob : IJobEntity
    {
        public NativeParallelMultiHashMap<int2, HashedUnit>.ParallelWriter hashMapWriter;
        public float cellSize;

        void Execute(Entity entity, in LocalTransform localTransform, in Team team)
        {
            int2 entityCellCoords = (int2)math.floor(localTransform.Position.xz / cellSize);
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
}

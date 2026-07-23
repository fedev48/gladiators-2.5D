using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct FollowFlowfieldSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldPoolSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FollowFlowfieldJob job = new FollowFlowfieldJob
        {
            flowFieldPool = SystemAPI.GetSingleton<FlowFieldPoolSingleton>().Pool,
            cellsLookup   = SystemAPI.GetBufferLookup<CellComponents>(isReadOnly: true),
            config        = SystemAPI.GetSingleton<GridConfig>()
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
partial struct FollowFlowfieldJob : IJobEntity
{
    [ReadOnly] public NativeList<Entity> flowFieldPool;
    [ReadOnly] public BufferLookup<CellComponents> cellsLookup;
    public GridConfig config;

    void Execute(in UsingPathfinding usingPathfinding, in LocalTransform localTransform, in MoveSpeed moveSpeed, ref DesiredVelocity desiredVelocity)
    {
        int2 cellCoords = GridSystem.WorldPosToCoords(localTransform.Position, config);
        if (!GridSystem.CheckIfCoordsIsInBounds(cellCoords, config)) return;

        Entity flowfieldContainer = flowFieldPool[usingPathfinding.flowFieldId];
        DynamicBuffer<CellComponents> cells = cellsLookup[flowfieldContainer];

        CellComponents cellUnitIsOn = cells[GridSystem.CoordsToIndex(cellCoords.x, cellCoords.y, config)];

        if (cellUnitIsOn.cost == GridSystem.WALL_COST &&
            !AvoidWall(cells, cellCoords, localTransform.Position, ref cellUnitIsOn))
            return; //no walkable neighbour with an escaping vector: keep previous direction

        desiredVelocity.Value = new float3(cellUnitIsOn.movingVector.x, 0, cellUnitIsOn.movingVector.y) * moveSpeed.Value;
    }

    
    bool AvoidWall(DynamicBuffer<CellComponents> cells, int2 cellCoords, float3 position, ref CellComponents cellUnitIsOn)
    {
        FixedList4096Bytes<int2> surroundingCellsCoords = GridSystem.GetSurroundingCells(cellCoords, 1, skipCentralCell: true);
        float bestDistSq = float.MaxValue;
        bool  found      = false;

        for (int i = 0; i < surroundingCellsCoords.Length; i++)
        {
            if (!GridSystem.CheckIfCoordsIsInBounds(surroundingCellsCoords[i], config)) continue;

            int flatIndex = GridSystem.CoordsToIndex(surroundingCellsCoords[i].x, surroundingCellsCoords[i].y, config);
            CellComponents neighbour = cells[flatIndex];
            if (neighbour.cost == GridSystem.WALL_COST || neighbour.bestCost < 0) continue;

            float3 cellCenter = GridSystem.FlatIndexToWorldPosition(flatIndex, config);
            float2 toCenter   = new float2(cellCenter.x - position.x, cellCenter.z - position.z);
            float  distSq     = math.lengthsq(toCenter);

            if (distSq < bestDistSq)
            {
                bestDistSq   = distSq;
                cellUnitIsOn = neighbour;
                found        = true;
            }
        }

        return found;
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(MoveToDestinationSystem))]
public partial struct StateWanderSystem : ISystem
{
    const float ARRIVE_DISTANCE = 2f;
    const int CELL_RADIUS = 5;
    const int MAX_PICK_TRIES = 10;

    Unity.Mathematics.Random random;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridBlueprintTag>();
        random = Unity.Mathematics.Random.CreateFromIndex(1234);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();
        DynamicBuffer<CellComponents> blueprintCells = SystemAPI.GetBuffer<CellComponents>(SystemAPI.GetSingletonEntity<GridBlueprintTag>());

        foreach ((RefRO<LocalTransform> transform,
                  RefRW<MoveDestination> destination,
                  RefRO<Team> team,
                  Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>,
                            RefRW<MoveDestination>,
                            RefRO<Team>>()
                .WithPresent<MoveDestination>()
                .WithEntityAccess())
        {
            if (team.ValueRO.value != Teams.ENEMY) continue;

            if (SystemAPI.IsComponentEnabled<MoveDestination>(entity))
            {
                float3 toDestination = destination.ValueRO.value - transform.ValueRO.Position;
                toDestination.y = 0f;
                if (math.length(toDestination) > ARRIVE_DISTANCE) continue;
            }

            int2 entityCell = GridSystem.WorldPosToCoords(transform.ValueRO.Position, gridConfig);
            FixedList4096Bytes<int2> surroundingCells = GridSystem.GetSurroundingCells(entityCell, CELL_RADIUS, roundedCorners: true);

            if (!TryPickWalkableCell(surroundingCells, blueprintCells, gridConfig, out int2 randomCell)) continue;

            destination.ValueRW.value = GridSystem.CoordsToWorldPosition(randomCell.x, randomCell.y, gridConfig);
            SystemAPI.SetComponentEnabled<MoveDestination>(entity, true);
        }
    }

    bool TryPickWalkableCell(in FixedList4096Bytes<int2> candidates, in DynamicBuffer<CellComponents> cells, GridConfig config, out int2 cell)
    {
        for (int i = 0; i < MAX_PICK_TRIES; i++)
        {
            int2 candidate = candidates[random.NextInt(0, candidates.Length)];
            if (!GridSystem.CheckIfCoordsIsInBounds(candidate, config)) continue;
            if (cells[GridSystem.CoordsToIndex(candidate.x, candidate.y, config)].cost == GridSystem.WALL_COST) continue;

            cell = candidate;
            return true;
        }

        cell = default;
        return false;
    }
}

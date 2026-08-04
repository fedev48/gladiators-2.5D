using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(FollowFlowfieldSystem))]
public partial struct MoveToDestinationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridBlueprintTag>(); //to check for walls
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();
        DynamicBuffer<CellComponents> blueprintCells = SystemAPI.GetBuffer<CellComponents>(SystemAPI.GetSingletonEntity<GridBlueprintTag>());

        foreach ((RefRW<LocalTransform> transform,
                  RefRO<MoveDestination> destination,
                  RefRO<MovementConfig> config,
                  RefRW<DesiredVelocity> moveDir,
                  RefRW<MoveSpeed> moveSpeed,
                  RefRW<NeedsPathfinding> needsPathfinding,
                  Entity entity) in
            SystemAPI.Query<RefRW<LocalTransform>,
                            RefRO<MoveDestination>,
                            RefRO<MovementConfig>,
                            RefRW<DesiredVelocity>,
                            RefRW<MoveSpeed>,
                            RefRW<NeedsPathfinding>>()
                .WithNone<MovementBlocked>()
                .WithPresent<NeedsPathfinding>()
                .WithEntityAccess())
        {
            float3 target = destination.ValueRO.value;

            int2 desiredCell  = GridSystem.WorldPosToCoords(target, gridConfig);
            int2 walkableCell = FindNearestWalkableCell(blueprintCells, desiredCell, gridConfig);
            if (!walkableCell.Equals(desiredCell))
            {
                desiredCell = walkableCell;
                target      = GridSystem.CoordsToWorldPosition(desiredCell.x, desiredCell.y, gridConfig);
            }

            int2 lastRequestedCell = GridSystem.WorldPosToCoords(needsPathfinding.ValueRO.destination, gridConfig);

            if (!desiredCell.Equals(lastRequestedCell))
            {
                needsPathfinding.ValueRW.destination = GridSystem.CoordsToWorldPosition(desiredCell.x, desiredCell.y, gridConfig);
                SystemAPI.SetComponentEnabled<NeedsPathfinding>(entity, true);
            }

            float3 toTarget = target - transform.ValueRO.Position;
            toTarget.y = 0f;

            float  dist         = math.length(toTarget);
            float  acceleration = config.ValueRO.acceleration;
            float  currentSpeed = moveSpeed.ValueRO.value;

            float stoppingDist = currentSpeed * currentSpeed / (2f * acceleration);

            if (dist > stoppingDist + 0.05f)
                currentSpeed = math.min(currentSpeed + acceleration * dt, config.ValueRO.maxSpeed);
            else
                currentSpeed = math.max(currentSpeed - acceleration * dt, 0f);

            moveSpeed.ValueRW.value = currentSpeed;

            bool isFollowingFlowField = SystemAPI.IsComponentEnabled<UsingPathfinding>(entity);
            if (!isFollowingFlowField)
            {
                float3 direction = math.normalizesafe(toTarget);
                moveDir.ValueRW.value = currentSpeed > 0.01f ? direction * currentSpeed : float3.zero;
            }

            transform.ValueRW.Rotation = quaternion.identity;
        }

        //destination disabled, decelerate to a stop and drop any pathfinding in course
        foreach ((RefRO<MovementConfig> config,
                  RefRW<DesiredVelocity> moveDir,
                  RefRW<MoveSpeed> moveSpeed,
                  Entity entity) in
            SystemAPI.Query<RefRO<MovementConfig>,
                            RefRW<DesiredVelocity>,
                            RefRW<MoveSpeed>>()
                .WithDisabled<MoveDestination>()
                .WithNone<MovementBlocked>()
                .WithPresent<NeedsPathfinding, UsingPathfinding>()
                .WithEntityAccess())
        {
            moveSpeed.ValueRW.value = math.max(moveSpeed.ValueRO.value - config.ValueRO.acceleration * dt, 0f);
            moveDir.ValueRW.value   = float3.zero;

            SystemAPI.SetComponentEnabled<NeedsPathfinding>(entity, false);
            SystemAPI.SetComponentEnabled<UsingPathfinding>(entity, false);
        }
    }

    static int2 FindNearestWalkableCell(DynamicBuffer<CellComponents> cells, int2 cell, GridConfig config)
    {
        const int MAX_SEARCH_RADIUS = 10;

        cell = math.clamp(cell, int2.zero, new int2(config.width - 1, config.height - 1)); //offsets can land outside the grid

        if (cells[GridSystem.CoordsToIndex(cell.x, cell.y, config)].cost != GridSystem.WALL_COST) return cell;

        for (int radius = 1; radius <= MAX_SEARCH_RADIUS; radius++)
        {
            int2 best       = cell;
            int  bestDistSq = int.MaxValue;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (math.max(math.abs(dx), math.abs(dy)) != radius) continue;
                    int2 candidate = new int2(cell.x + dx, cell.y + dy);
                    if (!GridSystem.CheckIfCoordsIsInBounds(candidate, config)) continue;
                    if (cells[GridSystem.CoordsToIndex(candidate.x, candidate.y, config)].cost == GridSystem.WALL_COST) continue;

                    int distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best       = candidate;
                    }
                }
            }

            if (bestDistSq != int.MaxValue) return best;
        }

        return cell;
    }
}

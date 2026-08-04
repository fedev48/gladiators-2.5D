using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(EntitiesPositionToHashSystem))]
partial struct BlackboardSensorSystem : ISystem
{
    EntityQuery sensorQuery;
    ComponentLookup<LocalTransform> transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        sensorQuery = SystemAPI.QueryBuilder()
            .WithAllRW<FSMBlackBoard, TargetingState>()
            .WithAll<TargetingConfig, Team, LocalTransform>()
            .WithPresent<HasTarget, LastAttacker>()
            .Build();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);

        state.RequireForUpdate(sensorQuery);
        state.RequireForUpdate<GridConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();
        UnitSpatialHashComponents spatialHash = state.EntityManager.GetComponentData<UnitSpatialHashComponents>(
            state.WorldUnmanaged.GetExistingUnmanagedSystem<EntitiesPositionToHashSystem>());

        transformLookup.Update(ref state);

        SenseTargetsJob senseJob = new SenseTargetsJob
        {
            unitsPerGridHashMap = spatialHash.hashMap,
            transformLookup     = transformLookup,
            cellSize            = gridConfig.cellSize,
            deltaTime           = SystemAPI.Time.DeltaTime
        };
        state.Dependency = senseJob.ScheduleParallel(
            JobHandle.CombineDependencies(state.Dependency, spatialHash.producerHandle));
    }
}

[BurstCompile]
[WithPresent(typeof(HasTarget), typeof(LastAttacker))]
partial struct SenseTargetsJob : IJobEntity
{
    const int   MAX_CELL_RADIUS = 10;
    const float STAGGER_STEP    = 0.6180339f;

    [ReadOnly] public NativeParallelMultiHashMap<int2, HashedUnit> unitsPerGridHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> transformLookup;
    public float cellSize;
    public float deltaTime;

    void Execute(
        Entity entity,
        in LocalTransform localTransform,
        in Team team,
        in TargetingConfig config,
        ref TargetingState targetingState,
        ref FSMBlackBoard blackboard,
        EnabledRefRW<HasTarget> hasTarget,
        ref LastAttacker lastAttacker,
        EnabledRefRW<LastAttacker> attackerPending)
    {
        float3 position = localTransform.Position;

        if (targetingState.scanCooldown < 0f)
            targetingState.scanCooldown = config.scanInterval * math.frac(entity.Index * STAGGER_STEP);
        else
            targetingState.scanCooldown -= deltaTime;

        targetingState.lockRemaining = math.max(targetingState.lockRemaining - deltaTime, 0f);

        bool targetValid = hasTarget.ValueRO && transformLookup.HasComponent(blackboard.target);
        if (targetValid) blackboard.targetLocation = transformLookup[blackboard.target].Position;

        if (attackerPending.ValueRO)
        {
            attackerPending.ValueRW = false;

            bool canRetaliate = !targetValid || targetingState.lockRemaining <= 0f;
            if (canRetaliate && transformLookup.HasComponent(lastAttacker.entity))
            {
                blackboard.target            = lastAttacker.entity;
                blackboard.targetLocation    = transformLookup[lastAttacker.entity].Position;
                targetingState.lockRemaining = config.attackerLockDuration;
                targetValid                  = true;
            }

            lastAttacker.entity = Entity.Null;
            lastAttacker.damage = 0f;
        }

        int   searchCellRadius = math.min(config.searchCellRadius, MAX_CELL_RADIUS);
        float searchRadius     = searchCellRadius * cellSize;
        float searchRadiusSq   = searchRadius * searchRadius;

        float currentDistanceSq = float.MaxValue;
        if (targetValid)
        {
            currentDistanceSq = math.lengthsq(blackboard.targetLocation.xz - position.xz);
            float leashRadius = searchRadius * config.retentionMultiplier;
            if (currentDistanceSq > leashRadius * leashRadius) targetValid = false;
        }

        hasTarget.ValueRW = targetValid;

        if (targetingState.scanCooldown > 0f) return;
        targetingState.scanCooldown = config.scanInterval;

        Entity bestCandidate   = Entity.Null;
        float3 bestPosition    = float3.zero;
        float  bestDistanceSq  = searchRadiusSq;
        int    hostilesInRange = 0;

        int2 unitCell = (int2)math.floor(position.xz / cellSize);
        foreach (int2 neighbourCell in GridSystem.GetSurroundingCells(unitCell, searchCellRadius, roundedCorners: true))
        {
            foreach (HashedUnit candidate in unitsPerGridHashMap.GetValuesForKey(neighbourCell))
            {
                if (candidate.team == team.value) continue;

                float distanceSq = math.lengthsq(candidate.position.xz - position.xz);
                if (distanceSq > searchRadiusSq) continue;

                hostilesInRange++;

                if (distanceSq >= bestDistanceSq) continue;
                bestDistanceSq = distanceSq;
                bestCandidate  = candidate.entity;
                bestPosition   = candidate.position;
            }
        }

        blackboard.enemiesSurrounding = hostilesInRange;

        if (bestCandidate == Entity.Null) return;
        if (targetValid && targetingState.lockRemaining > 0f) return;

        // candidate only steals the slot if it is close enough
        float improvement = 1f - config.switchImprovement;
        if (targetValid && bestDistanceSq > currentDistanceSq * improvement * improvement) return;

        blackboard.target         = bestCandidate;
        blackboard.targetLocation = bestPosition;
        hasTarget.ValueRW         = true;
    }
}

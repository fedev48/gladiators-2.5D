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
            .WithAllRW<FSMBlackBoard>()
            .WithAll<BlackboardSensorConfigAndState, Team, LocalTransform>()
            .WithPresent<HasTarget, LastAttacker>()
            .Build();

        transformLookup = state.GetComponentLookup<LocalTransform>(true);

        state.RequireForUpdate(sensorQuery);
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<UnitSpatialHashComponents>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();
        UnitSpatialHashComponents spatialHash = SystemAPI.GetSingleton<UnitSpatialHashComponents>();

        transformLookup.Update(ref state);

        SenseTargetsJob senseJob = new SenseTargetsJob
        {
            unitsPerGridHashMap = spatialHash.hashMap,
            transformLookup     = transformLookup,
            gridConfig          = gridConfig,
            deltaTime           = SystemAPI.Time.DeltaTime
        };

        state.Dependency = senseJob.ScheduleParallel(JobHandle.CombineDependencies(state.Dependency, spatialHash.producerHandle));
    }
}

[BurstCompile]
[WithPresent(typeof(HasTarget), typeof(LastAttacker))]
partial struct SenseTargetsJob : IJobEntity
{

    [ReadOnly] public NativeParallelMultiHashMap<int2, HashedUnit> unitsPerGridHashMap;
    [ReadOnly] public ComponentLookup<LocalTransform> transformLookup;
    public GridConfig gridConfig;
    public float deltaTime;

    void Execute(
        in LocalTransform localTransform,
        in Team team,
        ref BlackboardSensorConfigAndState blackboardConfigAndState,
        ref FSMBlackBoard blackboard,
        EnabledRefRW<HasTarget> hasTarget,
        ref LastAttacker lastAttacker)
    {
        if (transformLookup.TryGetComponent(blackboard.target, out LocalTransform transform)) blackboard.targetLocation = transform.Position;
        else blackboard.target = Entity.Null;
        CountSurroundingUnits(localTransform, team, blackboardConfigAndState, ref blackboard);

        SetHasTargetFlag(blackboard.target, hasTarget);

        if (!TryResetTimer(ref blackboardConfigAndState)) return;


        if (CheckRetaliation(ref blackboardConfigAndState, ref blackboard, ref lastAttacker, hasTarget)) return;

        // bool mustReplaceTarget = ShouldReleaseTarget(localTransform, blackboardConfigAndState, blackboard);

        TryGetNewTarget(localTransform, team, blackboardConfigAndState, ref blackboard, hasTarget);

    }

    private static void SetHasTargetFlag(Entity entity, EnabledRefRW<HasTarget> hasTarget) => hasTarget.ValueRW = entity != Entity.Null;

    private bool CheckRetaliation(ref BlackboardSensorConfigAndState blackboardConfigAndState, ref FSMBlackBoard blackboard, ref LastAttacker lastAttacker, EnabledRefRW<HasTarget> hasTarget)
    {
        DecayRetaliationDamage(ref blackboardConfigAndState, ref lastAttacker);
        if (HasEnoughDamageToRetaliate(blackboardConfigAndState, lastAttacker) && transformLookup.HasComponent(lastAttacker.entity))
        {
            // lastAttacker.accumulatedDamage -= blackboardConfigAndState.retaliationDamageThreshold;
            blackboard.target = lastAttacker.entity;
            SetHasTargetFlag(lastAttacker.entity, hasTarget);
            return true;
        }

        return false;
    }

    private bool ShouldReleaseTarget(LocalTransform localTransform, BlackboardSensorConfigAndState blackboardConfigAndState, FSMBlackBoard blackboard)
    {
        if (transformLookup.TryGetComponent(blackboard.target, out LocalTransform targetTransform))
        {
            float releasDistance = blackboardConfigAndState.searchRadiusForTarget * blackboardConfigAndState.distanceTargetReleaseMultiplier;

            if (math.distancesq(localTransform.Position, targetTransform.Position) < releasDistance * releasDistance)
            {
                return false;
            }
        }

        return true;
    }

    private void CountSurroundingUnits(LocalTransform localTransform, Team team, BlackboardSensorConfigAndState blackboardConfigAndState, ref FSMBlackBoard blackboard)
    {
        NativeList<HashedUnit> enemiesSurrounding = new NativeList<HashedUnit>(64, Allocator.Temp);
        FillUnitsBuffer(localTransform, blackboardConfigAndState.searchRadiusForSurrouded, ref enemiesSurrounding);

        int enemyCount = 0;
        foreach (HashedUnit candidate in enemiesSurrounding)
        {
            if (candidate.team == team.value) continue;
            if (math.distancesq(localTransform.Position,candidate.position) > blackboardConfigAndState.searchRadiusForSurrouded*blackboardConfigAndState.searchRadiusForSurrouded) continue; //needed because of grid quantization

            enemyCount++;
        }

        enemiesSurrounding.Dispose();

        blackboard.enemiesSurrounding = enemyCount;
    }

    private bool TryGetNewTarget(LocalTransform localTransform, Team team, BlackboardSensorConfigAndState blackboardConfigAndState, ref FSMBlackBoard blackboard, EnabledRefRW<HasTarget> hasTarget)
    {
        NativeList<HashedUnit> unitsInRadius = new NativeList<HashedUnit>(64, Allocator.Temp);
        FillUnitsBuffer(localTransform, blackboardConfigAndState.searchRadiusForTarget, ref unitsInRadius);

        Entity closerEntity = Entity.Null;
        float closerDistanceSq = float.MaxValue;
        float searchRadiusSq = blackboardConfigAndState.searchRadiusForTarget * blackboardConfigAndState.searchRadiusForTarget;

        foreach (HashedUnit candidate in unitsInRadius)
        {
            if (candidate.team == team.value) continue;

            float candidateDistanceSq = math.distancesq(localTransform.Position, candidate.position);

            if (candidateDistanceSq > searchRadiusSq) continue;

            if (candidateDistanceSq < closerDistanceSq)
            {
                closerDistanceSq = candidateDistanceSq;
                closerEntity = candidate.entity;
            }
        }

        unitsInRadius.Dispose();

        if (!ShouldReleaseTarget(localTransform, blackboardConfigAndState, blackboard) && !IsWorthSwitching(localTransform, blackboardConfigAndState, blackboard, closerDistanceSq)) return false;

        blackboard.target = closerEntity;
        SetHasTargetFlag(closerEntity, hasTarget);
        return closerEntity != Entity.Null;
    }

    private void FillUnitsBuffer(LocalTransform localTransform, float radius, ref NativeList<HashedUnit> unitsBuffer)
    {
        int2 centralCell = GridSystem.WorldPosToCoords(localTransform.Position, gridConfig);
        int  cellRadius  = (int)math.ceil(radius / gridConfig.cellSize);

        FixedList4096Bytes<int2> cells = GridSystem.GetSurroundingCells(centralCell, cellRadius, true);

        EntitiesPositionToHashSystem.GetUnitsInCells(unitsPerGridHashMap, cells, ref unitsBuffer);
    }

    private bool IsWorthSwitching(LocalTransform localTransform, BlackboardSensorConfigAndState blackboardConfigAndState, FSMBlackBoard blackboard, float candidateDistanceSq)
    {
        if (!transformLookup.TryGetComponent(blackboard.target, out LocalTransform targetTransform)) return true;

        float targetDistance = math.distance(localTransform.Position, targetTransform.Position);

        return math.sqrt(candidateDistanceSq) < targetDistance - blackboardConfigAndState.distanceDifferenceToSwitchTarget;
    }

    bool TryResetTimer(ref BlackboardSensorConfigAndState config)
    {
        if (config.clock<config.scanInterval)
        {
            config.clock+=deltaTime;
            return false;
        }
        else
        {
            config.clock = 0;
            return true;
        }
    }


    void DecayRetaliationDamage(ref BlackboardSensorConfigAndState config, ref LastAttacker lastAttacker)
    {
        lastAttacker.accumulatedDamage = math.max(0f, lastAttacker.accumulatedDamage - config.retaliationDamageDecay * config.scanInterval);
    }

    bool HasEnoughDamageToRetaliate(BlackboardSensorConfigAndState config, LastAttacker lastAttacker) => lastAttacker.accumulatedDamage >= config.retaliationDamageThreshold;


}

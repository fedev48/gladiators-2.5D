using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

partial struct StateTransitionSystem : ISystem
{
    ComponentLookup<PhysicsCollider> colliderLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        colliderLookup = state.GetComponentLookup<PhysicsCollider>(isReadOnly: true);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        colliderLookup.Update(ref state);
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        foreach ((
            RefRO<FSMState> fSMState,
            RefRO<FSMBlackBoard> fSMBlackBoard,
            RefRO<Health> health,
            RefRO<LocalTransform> transform,
            DynamicBuffer<StateTransition> transitions,
            DynamicBuffer<ChangeStateRequest> requests,
            Entity entity) in
        SystemAPI.Query<RefRO<FSMState>,
                        RefRO<FSMBlackBoard>,
                        RefRO<Health>,
                        RefRO<LocalTransform>,
                        DynamicBuffer<StateTransition>,
                        DynamicBuffer<ChangeStateRequest>>()
            .WithPresent<ChangeStateRequest>()
            .WithEntityAccess())
        {
            foreach (StateTransition transition in transitions)
            {
                if (transition.fromState != fSMState.ValueRO.current) continue;

                if (!CheckConditions(transition,
                                     fSMState.ValueRO,
                                     health.ValueRO,
                                     fSMBlackBoard.ValueRO,
                                     transform.ValueRO.Position,
                                     entity,
                                     collisionWorld,
                                     colliderLookup)) continue;

                requests.Add(new ChangeStateRequest { targetState = transition.toState, priority = transition.priority });
                SystemAPI.SetBufferEnabled<ChangeStateRequest>(entity, true);
            }
        }
    }

    static bool CheckConditions(
        in StateTransition transition,
        in FSMState fSMState,
        in Health health,
        in FSMBlackBoard fSMBlackBoard,
        float3 selfPosition,
        Entity self,
        in CollisionWorld collisionWorld,
        in ComponentLookup<PhysicsCollider> colliderLookup)
    {
        StateCondition conditions = transition.conditions;

        if (Has(conditions, StateCondition.StateFinished)      && !StateFinished(fSMState))                                                    return false;
        if (Has(conditions, StateCondition.HasTarget)          && !HasTarget(fSMBlackBoard))                                                   return false;
        if (Has(conditions, StateCondition.NoTarget)           && !NoTarget(fSMBlackBoard))                                                    return false;
        if (Has(conditions, StateCondition.TargetInRange)      && !TargetInRange(fSMBlackBoard, selfPosition, transition.rangeThreshold))      return false;
        if (Has(conditions, StateCondition.TargetOutOfRange)   && !TargetOutOfRange(fSMBlackBoard, selfPosition, transition.rangeThreshold))   return false;
        if (Has(conditions, StateCondition.HealthBelow)        && !HealthBelow(health, transition.healthThreshold))                            return false;
        if (Has(conditions, StateCondition.EnemiesAroundAbove) && !EnemiesAroundAbove(fSMBlackBoard, transition.enemiesThreshold))             return false;
        if (Has(conditions, StateCondition.TargetVisible)      && !TargetVisible(fSMBlackBoard, selfPosition, self, collisionWorld, colliderLookup)) return false;

        return true;
    }

   
    static bool Has(StateCondition conditions, StateCondition flag) => (conditions & flag) != 0;
    // & bitwise operator means the value of the bit has to be 1 in both values to be 1 in the result (any not condition for the transition will be 0)

    static bool StateFinished (in FSMState fSMState) => fSMState.stateDuration >= 0 && fSMState.timeInState >= fSMState.stateDuration;

    static bool HealthBelow (in Health health, float threshold) => health.value < threshold;

    static bool HasTarget (in FSMBlackBoard fSMBlackBoard) => fSMBlackBoard.target != Entity.Null;

    static bool NoTarget (in FSMBlackBoard fSMBlackBoard) => !HasTarget(fSMBlackBoard);

    static bool TargetInRange (in FSMBlackBoard fSMBlackBoard, float3 selfPosition, float threshold) => HasTarget(fSMBlackBoard) && math.lengthsq(fSMBlackBoard.targetLocation - selfPosition) < threshold * threshold;

    static bool TargetOutOfRange (in FSMBlackBoard fSMBlackBoard, float3 selfPosition, float threshold) => HasTarget(fSMBlackBoard) && !TargetInRange(fSMBlackBoard, selfPosition, threshold);

    static bool EnemiesAroundAbove (in FSMBlackBoard fSMBlackBoard, int threshold) => fSMBlackBoard.enemiesSurrounding > threshold;

    static bool TargetVisible(
        in FSMBlackBoard fSMBlackBoard,
        float3 selfPosition,
        Entity self,
        in CollisionWorld collisionWorld,
        in ComponentLookup<PhysicsCollider> colliderLookup)
    {
        if (!HasTarget(fSMBlackBoard)) return false;

        uint ownLayer = 0;
        if (colliderLookup.HasComponent(self) && colliderLookup[self].IsValid)
            ownLayer = colliderLookup[self].Value.Value.GetCollisionFilter().BelongsTo;

        RaycastInput ray = new RaycastInput
        {
            Start  = selfPosition,
            End    = fSMBlackBoard.targetLocation,
            Filter = new CollisionFilter
            {
                BelongsTo    = ~0u,
                CollidesWith = ~ownLayer,
                GroupIndex   = 0
            }
        };

        if (!collisionWorld.CastRay(ray, out RaycastHit hit)) return true;

        return hit.Entity == fSMBlackBoard.target;
    }
}

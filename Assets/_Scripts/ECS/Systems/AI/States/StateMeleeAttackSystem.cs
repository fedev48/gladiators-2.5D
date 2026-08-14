using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateAfter(typeof(StateManagerSystem))]
partial struct StateMeleeAttackSystem : ISystem
{
    TypeIndex fleeState;
    TypeIndex deathState;

    ComponentLookup<Health>            healthLookup;
    ComponentLookup<RecievingDamage>   damageLookup;
    ComponentLookup<KnockbackVelocity> knockbackLookup;
    ComponentLookup<LastAttacker>      lastAttackerLookup;
    ComponentLookup<IsOneShot>         oneShotLookup;
    ComponentLookup<CameraFacingData>  cameraLookup;
    ComponentLookup<Team>              teamLookup;
    BufferLookup<AnimationClipData>    clipsLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();

        fleeState = TypeManager.GetTypeIndex<FollowState>();

        healthLookup       = state.GetComponentLookup<Health>(isReadOnly: true);
        damageLookup       = state.GetComponentLookup<RecievingDamage>(isReadOnly: false);
        knockbackLookup    = state.GetComponentLookup<KnockbackVelocity>(isReadOnly: false);
        lastAttackerLookup = state.GetComponentLookup<LastAttacker>(isReadOnly: false);
        oneShotLookup      = state.GetComponentLookup<IsOneShot>(isReadOnly: false);
        cameraLookup       = state.GetComponentLookup<CameraFacingData>(isReadOnly: true);
        teamLookup         = state.GetComponentLookup<Team>(isReadOnly: true);
        clipsLookup        = state.GetBufferLookup<AnimationClipData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);
        damageLookup.Update(ref state);
        knockbackLookup.Update(ref state);
        lastAttackerLookup.Update(ref state);
        oneShotLookup.Update(ref state);
        cameraLookup.Update(ref state);
        teamLookup.Update(ref state);
        clipsLookup.Update(ref state);

        float deltaTime = SystemAPI.Time.DeltaTime;
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

        foreach (
            (RefRW<MeleeAttackState> meleeAttackState,
            RefRO<LocalTransform> localTransform,
            RefRO<FSMBlackBoard> fSMBlackBoard,
            RefRO<VisualEntity> visualEntity,
            RefRW<FSMState> fSMState,
            EnabledRefRO<MoveDestination> isApproaching,
            Entity entity)
        in SystemAPI.Query<
                RefRW<MeleeAttackState>,
                RefRO<LocalTransform> ,
                RefRO<FSMBlackBoard>,
                RefRO<VisualEntity>,
                RefRW<FSMState>,
                EnabledRefRO<MoveDestination>>().
                WithPresent<MoveDestination>().
                WithEntityAccess())
        {

            if (fSMBlackBoard.ValueRO.target == Entity.Null) continue;

            float3 targetPosition = fSMBlackBoard.ValueRO.targetLocation;
            float3 currentEntityPosition = localTransform.ValueRO.Position;
            float hitDistance = isApproaching.ValueRO
                ? meleeAttackState.ValueRO.stopDistance
                : meleeAttackState.ValueRO.attackRange;

            float3 toTarget = targetPosition - currentEntityPosition;
            toTarget.y = 0f;
            
            bool isAttacking = meleeAttackState.ValueRW.duration != 0f;

            bool isTargetInReach = IsTargetInReach(toTarget, hitDistance);

            if (isTargetInReach)
            {
                state.EntityManager.SetComponentData(entity, new MoveDestination { value = float3.zero });
                state.EntityManager.SetComponentEnabled<MoveDestination>(entity, false);
            }
            else if (meleeAttackState.ValueRO.elapsed >= meleeAttackState.ValueRO.duration - meleeAttackState.ValueRO.recovery)
            {
                state.EntityManager.SetComponentData(entity, new MoveDestination { value = targetPosition });
                state.EntityManager.SetComponentEnabled<MoveDestination>(entity, true);
                continue;
            }

            Entity visual = visualEntity.ValueRO.value;

            if (!isAttacking) //sets the start of the attack
            {
                ResetAnimationValues(meleeAttackState, fSMState, toTarget, visual);
                continue;
            }

            if (meleeAttackState.ValueRO.elapsed >= meleeAttackState.ValueRO.duration)
            {
                meleeAttackState.ValueRW.duration = 0f; //this acts as the flag to indicate the current attack has ended. We set the duration of the attack based on the animation in ResetAnimationValues, and put it in 0 at the end
            }

            meleeAttackState.ValueRW.elapsed += deltaTime;

            if (meleeAttackState.ValueRO.elapsed < meleeAttackState.ValueRO.hitTime) continue;
            if (meleeAttackState.ValueRO.hitLanded) continue;
            meleeAttackState.ValueRW.hitLanded = true;

            // if (!isTargetInReach) continue; //erase once the units take y into account for calculate damage

            if (meleeAttackState.ValueRO.hitRadius == 0)
            {
                AttackActions.ResolveHit(
                    fSMBlackBoard.ValueRO.target,
                    entity,
                    toTarget,
                    meleeAttackState.ValueRO.damage,
                    meleeAttackState.ValueRO.knockbackStrength,
                    ref healthLookup,
                    ref damageLookup,
                    ref knockbackLookup,
                    ref lastAttackerLookup);

                continue;
            }

            AreaHit(entity, teamLookup[entity].value, currentEntityPosition, meleeAttackState.ValueRO, collisionWorld, ref hits);
        }

        hits.Dispose();
    }

    private void ResetAnimationValues(RefRW<MeleeAttackState> meleeAttackState, RefRW<FSMState> fSMState, float3 toTarget, Entity visual)
    {
        float duration = 0f;
        float hitTime = 0f;

        if (TryReproducingAttackAnimation(visual, toTarget, out AnimationClipData clip))
        {
            duration = clip.frameCount / clip.fps;
            hitTime = math.min(clip.hitFrame / clip.fps, duration);
        }

        duration += meleeAttackState.ValueRO.recovery;

        meleeAttackState.ValueRW.elapsed = 0f;
        meleeAttackState.ValueRW.duration = duration;
        meleeAttackState.ValueRW.hitTime = hitTime;
        meleeAttackState.ValueRW.hitLanded = false;
        fSMState.ValueRW.stateDuration = fSMState.ValueRO.timeInState + duration;
    }

    void AreaHit(
        Entity attacker,
        byte attackerTeam,
        float3 attackerPosition,
        in MeleeAttackState attack,
        in CollisionWorld collisionWorld,
        ref NativeList<DistanceHit> hits)
    {
        AttackActions.QueryHits(collisionWorld, attackerPosition, attack.hitRadius, CollisionFilter.Default, attacker, ref hits);

        foreach (DistanceHit hit in hits)
        {
            if (!teamLookup.HasComponent(hit.Entity) || teamLookup[hit.Entity].value == attackerTeam) continue;

            AttackActions.ResolveHit(
                hit.Entity,
                attacker,
                hit.Position - attackerPosition,
                attack.damage,
                attack.knockbackStrength,
                ref healthLookup,
                ref damageLookup,
                ref knockbackLookup,
                ref lastAttackerLookup);
        }
    }


    bool TryReproducingAttackAnimation(Entity visualEntity, float3 toTarget, out AnimationClipData clip)
    {
        clip = default;

        if (!clipsLookup.HasBuffer(visualEntity) || !oneShotLookup.HasComponent(visualEntity)) return false;

        quaternion invRotation    = quaternion.identity;
        bool       fourDirections = true;

        if (cameraLookup.HasComponent(visualEntity))
        {
            CameraFacingData facingData = cameraLookup[visualEntity];
            invRotation    = facingData.invRotation;
            fourDirections = facingData.fourDirections;
        }

        AnimationDirection direction = AnimationActions.FacingDirection(math.mul(invRotation, toTarget), fourDirections);

        if (!AnimationActions.TryGetClip(Animation.Attack, direction, clipsLookup[visualEntity], out clip)) return false;
        if (clip.fps <= 0f) return false;

        oneShotLookup[visualEntity] = new IsOneShot { animation = Animation.Attack, animationDirection = direction };
        oneShotLookup.SetComponentEnabled(visualEntity, true);

        return true;
    }

    bool IsTargetInReach(float3 toTarget, float hitDistance)
    {
        if (math.lengthsq(toTarget) > hitDistance * hitDistance) return false;

        return true;
    }

   

}

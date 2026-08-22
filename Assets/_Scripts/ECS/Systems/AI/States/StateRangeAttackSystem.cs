using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(StateManagerSystem))]
partial struct StateRangeAttackSystem : ISystem
{
    ComponentLookup<IsOneShot>        oneShotLookup;
    ComponentLookup<CameraFacingData> cameraLookup;
    BufferLookup<AnimationClipData>   clipsLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();

        oneShotLookup = state.GetComponentLookup<IsOneShot>(isReadOnly: false);
        cameraLookup  = state.GetComponentLookup<CameraFacingData>(isReadOnly: true);
        clipsLookup   = state.GetBufferLookup<AnimationClipData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        oneShotLookup.Update(ref state);
        cameraLookup.Update(ref state);
        clipsLookup.Update(ref state);

        EntitiesReferences refs = SystemAPI.GetSingleton<EntitiesReferences>();
        BulletConfig bulletConfig = SystemAPI.GetComponent<BulletConfig>(refs.bulletPrefabEnemyEntity);

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (
            (RefRW<RangeAttack> rangeAttack,
            RefRO<LocalTransform> localTransform,
            RefRO<FSMBlackBoard> fSMBlackBoard,
            RefRO<BulletSpellConfig> bulletSpellConfig,
            RefRO<VisualEntity> visualEntity,
            RefRW<FSMState> fSMState,
            Entity entity)
        in SystemAPI.Query<
                RefRW<RangeAttack>,
                RefRO<LocalTransform>,
                RefRO<FSMBlackBoard>,
                RefRO<BulletSpellConfig>,
                RefRO<VisualEntity>,
                RefRW<FSMState>>().
                WithPresent<MoveDestination>().
                WithEntityAccess())
        {
            if (fSMBlackBoard.ValueRO.target == Entity.Null) continue;

            float3 targetPosition = fSMBlackBoard.ValueRO.targetLocation;
            float3 currentEntityPosition = localTransform.ValueRO.Position;

            float3 toTarget = targetPosition - currentEntityPosition;
            toTarget.y = 0f;

            float idealRange = IdealRange(bulletConfig, bulletSpellConfig.ValueRO.fireAngle, rangeAttack.ValueRO.straightShotRange);

            bool isAttacking = rangeAttack.ValueRW.duration != 0f;

            bool isAtFiringDistance = IsAtFiringDistance(toTarget, idealRange, rangeAttack.ValueRO.distanceTolerance);

            if (isAtFiringDistance)
            {
                state.EntityManager.SetComponentData(entity, new MoveDestination { value = float3.zero });
                state.EntityManager.SetComponentEnabled<MoveDestination>(entity, false);
            }
            else if (rangeAttack.ValueRO.elapsed >= rangeAttack.ValueRO.duration - rangeAttack.ValueRO.recovery)
            {
                state.EntityManager.SetComponentData(entity, new MoveDestination { value = FiringPosition(targetPosition, toTarget, idealRange) });
                state.EntityManager.SetComponentEnabled<MoveDestination>(entity, true);
                continue;
            }

            Entity visual = visualEntity.ValueRO.value;

            if (!isAttacking) //sets the start of the attack
            {
                ResetAnimationValues(rangeAttack, fSMState, toTarget, visual);
                continue;
            }

            if (rangeAttack.ValueRO.elapsed >= rangeAttack.ValueRO.duration)
            {
                rangeAttack.ValueRW.duration = 0f; // 0 means the current attack ended
            }

            rangeAttack.ValueRW.elapsed += deltaTime;

            if (rangeAttack.ValueRO.elapsed < rangeAttack.ValueRO.shotTime) continue;
            if (rangeAttack.ValueRO.shotFired) continue;
            rangeAttack.ValueRW.shotFired = true;

            state.EntityManager.SetComponentData(entity, new FireBulletEvent { direction = math.normalizesafe(toTarget) });
            state.EntityManager.SetComponentEnabled<FireBulletEvent>(entity, true);
        }
    }

    private void ResetAnimationValues(RefRW<RangeAttack> rangeAttack, RefRW<FSMState> fSMState, float3 toTarget, Entity visual)
    {
        float duration = 0f;
        float shotTime = 0f;

        AnimationDirection direction = AnimationActions.ResolveDirection(visual, toTarget, ref cameraLookup);

        if (AnimationActions.TryPlayOneShot(visual, Animation.Attack, direction, ref clipsLookup, ref oneShotLookup, out AnimationClipData clip))
        {
            duration = clip.frameCount / clip.fps;
            shotTime = math.min(clip.hitFrame / clip.fps, duration);
        }

        duration += rangeAttack.ValueRO.recovery;

        rangeAttack.ValueRW.elapsed   = 0f;
        rangeAttack.ValueRW.duration  = duration;
        rangeAttack.ValueRW.shotTime  = shotTime;
        rangeAttack.ValueRW.shotFired = false;
        fSMState.ValueRW.stateDuration = fSMState.ValueRO.timeInState + duration;
    }

    static bool IsAtFiringDistance(float3 toTarget, float idealRange, float tolerance)
    {
        return math.abs(math.length(toTarget) - idealRange) <= tolerance;
    }

    static float3 FiringPosition(float3 targetPosition, float3 toTarget, float idealRange)
    {
        return targetPosition + math.normalizesafe(-toTarget, math.forward()) * idealRange;
    }

    //x = v^2 * sin(2a) / g, assuming the bullet lands at the height it was fired from
    static float IdealRange(in BulletConfig bulletConfig, float fireAngle, float straightShotRange)
    {
        float gravity = SimConstants.GRAVITY * bulletConfig.gravityScale;
        if (gravity <= 0f) return straightShotRange;

        float angle = math.radians(fireAngle);
        return bulletConfig.speed * bulletConfig.speed * math.sin(2f * angle) / gravity;
    }
}

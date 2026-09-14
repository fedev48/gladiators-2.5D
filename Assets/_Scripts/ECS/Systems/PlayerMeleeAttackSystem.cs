using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateAfter(typeof(InputReaderSystem))]
partial struct PlayerMeleeAttackSystem : ISystem
{
    const float DAMAGE             = 10f;
    const float KNOCKBACK_STRENGTH = 5f;

    ComponentLookup<Team>              teamLookup;
    ComponentLookup<Health>            healthLookup;
    ComponentLookup<RecievingDamage>   damageLookup;
    ComponentLookup<KnockbackVelocity> knockbackLookup;
    ComponentLookup<LastAttacker>      lastAttackerLookup;
    ComponentLookup<LocalTransform>    transformLookup;
    ComponentLookup<PhysicsCollider>   colliderLookup;
    ComponentLookup<Parent>            parentLookup;
    BufferLookup<AnimationClipData>    clipsLookup;
    ComponentLookup<IsOneShot>         oneShotLookup;
    ComponentLookup<CameraFacingData>  cameraLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        teamLookup         = state.GetComponentLookup<Team>(isReadOnly: true);
        healthLookup       = state.GetComponentLookup<Health>(isReadOnly: true);
        damageLookup       = state.GetComponentLookup<RecievingDamage>();
        knockbackLookup    = state.GetComponentLookup<KnockbackVelocity>();
        lastAttackerLookup = state.GetComponentLookup<LastAttacker>();
        transformLookup    = state.GetComponentLookup<LocalTransform>();
        colliderLookup     = state.GetComponentLookup<PhysicsCollider>(isReadOnly: true);
        parentLookup       = state.GetComponentLookup<Parent>(isReadOnly: true);
        clipsLookup        = state.GetBufferLookup<AnimationClipData>(isReadOnly: true);
        oneShotLookup      = state.GetComponentLookup<IsOneShot>();
        cameraLookup       = state.GetComponentLookup<CameraFacingData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        teamLookup.Update(ref state);
        healthLookup.Update(ref state);
        damageLookup.Update(ref state);
        knockbackLookup.Update(ref state);
        lastAttackerLookup.Update(ref state);
        transformLookup.Update(ref state);
        colliderLookup.Update(ref state);
        parentLookup.Update(ref state);
        clipsLookup.Update(ref state);
        oneShotLookup.Update(ref state);
        cameraLookup.Update(ref state);

        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(16, Allocator.Temp);

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<MeleeHitbox> hitbox, RefRW<MeleeAttackEvent> attack, RefRO<Team> team, RefRO<VisualEntity> visual, Entity entity) in
                SystemAPI.Query<RefRO<MeleeHitbox>, RefRW<MeleeAttackEvent>, RefRO<Team>, RefRO<VisualEntity>>().WithEntityAccess())
        {
            if (attack.ValueRO.elapsed == 0f)
            {
                attack.ValueRW.hitTime = PlayAnimation(visual.ValueRO.value, attack.ValueRO.direction, out float duration);
                attack.ValueRW.duration = duration;
                BlockMovement(ref state, entity, duration);
            }

            attack.ValueRW.elapsed += deltaTime;

            if (attack.ValueRO.elapsed >= attack.ValueRO.duration)
                SystemAPI.SetComponentEnabled<MeleeAttackEvent>(entity, false);

            if (attack.ValueRO.elapsed < attack.ValueRO.hitTime) continue;
            if (attack.ValueRO.hitLanded) continue;
            attack.ValueRW.hitLanded = true;

            Entity hitboxEntity = hitbox.ValueRO.value;

            RigidTransform hitboxWorld = RotateCollider(entity, hitboxEntity, attack.ValueRO.direction);
            ApplyDamage(entity, hitboxEntity, team.ValueRO.value, hitboxWorld, collisionWorld, ref hits);
        }
    }

    float PlayAnimation(Entity visualEntity, float3 direction, out float duration)
    {
        duration = 0f;
        AnimationDirection facing = AnimationActions.ResolveDirection(visualEntity, direction, ref cameraLookup);

        if (!AnimationActions.TryPlayOneShot(visualEntity, Animation.Attack, facing, ref clipsLookup, ref oneShotLookup, out AnimationClipData clip))
            return 0f;

        duration = clip.frameCount / clip.fps;
        return math.min(clip.hitFrame / clip.fps, duration);
    }

    void BlockMovement(ref SystemState state, Entity player, float duration)
    {
        float remainingTime = SystemAPI.GetComponent<MovementBlocked>(player).remainingTime;
        SystemAPI.SetComponent(player, new MovementBlocked { remainingTime = math.max(remainingTime, duration) });
        SystemAPI.SetComponentEnabled<MovementBlocked>(player, true);
    }

    RigidTransform RotateCollider(Entity player, Entity hitboxEntity, float3 direction)
    {
        quaternion rotation = quaternion.RotateY(math.atan2(-direction.z, direction.x));
        float3 playerPosition = transformLookup[player].Position;

        LocalTransform hitboxTransform = transformLookup[hitboxEntity];
        hitboxTransform.Rotation = rotation;
        if (!parentLookup.HasComponent(hitboxEntity)) hitboxTransform.Position = playerPosition;
        transformLookup[hitboxEntity] = hitboxTransform;

        return new RigidTransform(rotation, playerPosition);
    }

    void ApplyDamage(Entity player, Entity hitboxEntity, byte playerTeam, RigidTransform hitboxWorld, in CollisionWorld collisionWorld, ref NativeList<DistanceHit> hits)
    {
        hits.Clear();
        collisionWorld.CalculateDistance(new ColliderDistanceInput(colliderLookup[hitboxEntity].Value, 0f, hitboxWorld), ref hits);

        float3 playerPosition = transformLookup[player].Position;

        foreach (DistanceHit hit in hits)
        {
            if (!teamLookup.HasComponent(hit.Entity) || teamLookup[hit.Entity].value == playerTeam) continue;

            AttackActions.ResolveHit(
                hit.Entity,
                player,
                transformLookup[hit.Entity].Position - playerPosition,
                DAMAGE,
                KNOCKBACK_STRENGTH,
                ref healthLookup,
                ref damageLookup,
                ref knockbackLookup,
                ref lastAttackerLookup);
        }
    }
}

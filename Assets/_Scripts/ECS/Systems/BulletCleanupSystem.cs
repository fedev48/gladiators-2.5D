using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
public partial struct BulletCleanupSystem : ISystem
{
    //test explosion, same overlap the old input double-click shockwave used
    const float EXPLOSION_RADIUS   = 1f;
    const float EXPLOSION_STRENGTH = 10f;
    const float EXPLOSION_DAMAGE   = 10f;

    private ComponentLookup<KnockbackVelocity> knockbackLookup;
    private ComponentLookup<RecievingDamage>   damageLookup;
    private ComponentLookup<Health>            healthLookup;
    private ComponentLookup<LastAttacker>      lastAttackerLookup;

    public void OnCreate(ref SystemState state)
    {
        knockbackLookup    = state.GetComponentLookup<KnockbackVelocity>(isReadOnly: false);
        damageLookup       = state.GetComponentLookup<RecievingDamage>(isReadOnly: false);
        healthLookup       = state.GetComponentLookup<Health>(isReadOnly: true);
        lastAttackerLookup = state.GetComponentLookup<LastAttacker>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        knockbackLookup.Update(ref state);
        damageLookup.Update(ref state);
        healthLookup.Update(ref state);
        lastAttackerLookup.Update(ref state);

        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

        foreach ((RefRO<LocalTransform> transform, RefRO<BulletConfig> config, Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<BulletConfig>>()
                .WithAll<BulletDestroyTag>()
                .WithEntityAccess())
        {
            Explode(transform.ValueRO.Position, config.ValueRO.owner, collisionWorld, ref hits);
            ecb.DestroyEntity(entity);
        }

        hits.Dispose();
    }

    void Explode(float3 center, Entity owner, in CollisionWorld collisionWorld, ref NativeList<DistanceHit> hits)
    {
        AttackActions.QueryHits(collisionWorld, center, EXPLOSION_RADIUS, CollisionFilter.Default, owner, ref hits);

        foreach (DistanceHit hit in hits)
        {
            AttackActions.ResolveHit(
                hit.Entity,
                owner,
                hit.Position - center,
                EXPLOSION_DAMAGE,
                EXPLOSION_STRENGTH,
                ref healthLookup,
                ref damageLookup,
                ref knockbackLookup,
                ref lastAttackerLookup);
        }
    }
}

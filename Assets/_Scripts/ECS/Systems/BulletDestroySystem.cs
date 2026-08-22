using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[BurstCompile]
public partial struct BulletDestroySystem : ISystem
{
    private ComponentLookup<BulletConfig> bulletLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        bulletLookup = state.GetComponentLookup<BulletConfig>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRW<BulletConfig> config, Entity entity) in
            SystemAPI.Query<RefRW<BulletConfig>>().WithEntityAccess())
        {
            config.ValueRW.lifetime -= dt;
            if (config.ValueRO.lifetime <= 0f)
                ecb.SetComponentEnabled<BulletDestroyTag>(entity, true);
        }

        bulletLookup.Update(ref state);

        state.Dependency = new BulletTriggerJob
        {
            bulletLookup = bulletLookup,
            ecb          = ecb
        }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
    }

    [BurstCompile]
    private struct BulletTriggerJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<BulletConfig> bulletLookup;
        public EntityCommandBuffer ecb;

        public void Execute(TriggerEvent triggerEvent)
        {
            TryDestroy(triggerEvent.EntityA, triggerEvent.EntityB);
            TryDestroy(triggerEvent.EntityB, triggerEvent.EntityA);
        }

        void TryDestroy(Entity bullet, Entity other)
        {
            if (!bulletLookup.HasComponent(bullet)) return;
            if (bulletLookup[bullet].owner == other) return;

            ecb.SetComponentEnabled<BulletDestroyTag>(bullet, true);
        }
    }
}

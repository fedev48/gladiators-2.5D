using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
// [UpdateAfter(typeof(PhysicsSimulationGroup))]
[BurstCompile]
public partial struct BulletDestroySystem : ISystem
{
    private ComponentLookup<BulletConfig> bulletLookup;

    public void OnCreate(ref SystemState state)
    {
        bulletLookup = state.GetComponentLookup<BulletConfig>(isReadOnly: true);
    }

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

        var simulation = SystemAPI.GetSingleton<SimulationSingleton>();

        foreach (TriggerEvent triggerEvent in simulation.AsSimulation().TriggerEvents)
        {
            TryDestroy(triggerEvent.EntityA, triggerEvent.EntityB, ecb);
            TryDestroy(triggerEvent.EntityB, triggerEvent.EntityA, ecb);
        }
    }

    void TryDestroy(Entity bullet, Entity other, EntityCommandBuffer ecb)
    {
        if (!bulletLookup.HasComponent(bullet)) return;
        if (bulletLookup[bullet].owner == other) return;

        ecb.SetComponentEnabled<BulletDestroyTag>(bullet, true);
    }
}

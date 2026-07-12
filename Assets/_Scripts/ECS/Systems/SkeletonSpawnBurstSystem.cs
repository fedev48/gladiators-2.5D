using Unity.Burst;
using Unity.Entities;

[UpdateAfter(typeof(InputReaderSystem))]
[UpdateBefore(typeof(SkeletonSpawnSystem))]
[BurstCompile]
public partial struct SkeletonSpawnBurstSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<SkeletonSpawnBurst> burst,
                  RefRO<SkeletonSpellConfig> spellConfig,
                  Entity entity) in
            SystemAPI.Query<RefRW<SkeletonSpawnBurst>, RefRO<SkeletonSpellConfig>>()
                .WithEntityAccess())
        {
            burst.ValueRW.timer -= dt;

            if (burst.ValueRO.timer > 0f)
                continue;

            SystemAPI.SetComponent(entity, new SummonSkeletonEvent { count = 1 });
            SystemAPI.SetComponentEnabled<SummonSkeletonEvent>(entity, true);

            burst.ValueRW.remaining--;
            burst.ValueRW.timer = spellConfig.ValueRO.interval;

            if (burst.ValueRO.remaining <= 0)
                SystemAPI.SetComponentEnabled<SkeletonSpawnBurst>(entity, false);
        }
    }
}

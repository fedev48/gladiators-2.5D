using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[UpdateBefore(typeof(VelocityComposerSystem))]
[BurstCompile]
partial struct KnockbackDecaySystem : ISystem
{
    const float baseDecayRate = 8f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (RefRW<KnockbackVelocity> knockback in SystemAPI.Query<RefRW<KnockbackVelocity>>())
        {
            float rate = baseDecayRate / knockback.ValueRO.durationMultiplier;
            float3 value = knockback.ValueRO.Value * math.exp(-rate * deltaTime);
            knockback.ValueRW.Value = math.lengthsq(value) > 0.01f ? value : float3.zero;
        }
    }
}

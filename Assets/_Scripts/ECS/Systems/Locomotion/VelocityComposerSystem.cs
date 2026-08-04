using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[UpdateBefore(typeof(MoveSystem))]
[UpdateAfter(typeof(UnitSeparationSystem))]

partial struct VelocityComposerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((  RefRW<CurrentVelocity> currentVelocity,
                    RefRO<DesiredVelocity> desiredVelocity,
                    RefRO<KnockbackVelocity> knockbackVelocity,
                    RefRO<SeparationVelocity> separationVelocity,
                    RefRO<MoveSpeed> moveSpeed,
                    EnabledRefRO<MovementBlocked> movementBlocked) in
                        SystemAPI.Query<RefRW<CurrentVelocity>,
                        RefRO<DesiredVelocity>,
                        RefRO<KnockbackVelocity>,
                        RefRO<SeparationVelocity>,
                        RefRO<MoveSpeed>,
                        EnabledRefRO<MovementBlocked>>()
                        .WithPresent<MovementBlocked>())
        {
            float3 desired = movementBlocked.ValueRO ? float3.zero : desiredVelocity.ValueRO.value;

            float3 knockback = knockbackVelocity.ValueRO.Value * knockbackVelocity.ValueRO.multiplier;
            if (math.lengthsq(knockback) > 0.01f)
            {
                currentVelocity.ValueRW.value = knockback + separationVelocity.ValueRO.value;
            }
            else
            {
                float3 resultantVelocity = desired + separationVelocity.ValueRO.value;
                if (math.lengthsq(resultantVelocity) > moveSpeed.ValueRO.value * moveSpeed.ValueRO.value) resultantVelocity = math.normalize(resultantVelocity) * moveSpeed.ValueRO.value;//clamp
                currentVelocity.ValueRW.value = resultantVelocity;
            }
        }
    }
}

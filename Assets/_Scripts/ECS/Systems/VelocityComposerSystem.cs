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
            float3 desired = movementBlocked.ValueRO ? float3.zero : desiredVelocity.ValueRO.Value;

            if (math.lengthsq(knockbackVelocity.ValueRO.Value) > 0.01f)
            {
                currentVelocity.ValueRW.Value = knockbackVelocity.ValueRO.Value + separationVelocity.ValueRO.Value;
            }
            else
            {
                float3 resultantVelocity = desired + separationVelocity.ValueRO.Value;
                if (math.lengthsq(resultantVelocity) > moveSpeed.ValueRO.Value * moveSpeed.ValueRO.Value) resultantVelocity = math.normalize(resultantVelocity) * moveSpeed.ValueRO.Value;//clamp
                currentVelocity.ValueRW.Value = resultantVelocity;
            }
        }
    }
}

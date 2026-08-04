using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (currentVelocity, velocity) in
            SystemAPI.Query<RefRO<CurrentVelocity>, RefRW<PhysicsVelocity>>()
                .WithAll<UnitTag>())
        {
            float3 vel = currentVelocity.ValueRO.value;
            velocity.ValueRW.Linear  = new float3(vel.x, 0f, vel.z);
            velocity.ValueRW.Angular = float3.zero;
        }
    }
}

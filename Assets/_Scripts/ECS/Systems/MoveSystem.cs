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
        foreach (var (moveDir, moveSpeed, velocity) in
            SystemAPI.Query<RefRO<MoveDirection>, RefRO<MoveSpeed>, RefRW<PhysicsVelocity>>()
                .WithAll<UnitTag>())
        {
            float3 vel = moveDir.ValueRO.Value * moveSpeed.ValueRO.Value;
            velocity.ValueRW.Linear  = new float3(vel.x, 0f, vel.z);
            velocity.ValueRW.Angular = float3.zero;
        }
    }
}

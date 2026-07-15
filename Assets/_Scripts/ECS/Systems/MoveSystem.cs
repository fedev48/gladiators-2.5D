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
                .WithAll<UnitTag>()
                .WithNone<SeparationVector>())
        {
            float3 vel = moveDir.ValueRO.Value * moveSpeed.ValueRO.Value;
            velocity.ValueRW.Linear  = new float3(vel.x, 0f, vel.z);
            velocity.ValueRW.Angular = float3.zero;
        }

        foreach (var (moveDir, separation, moveSpeed, velocity) in
            SystemAPI.Query<RefRO<MoveDirection>, RefRO<SeparationVector>, RefRO<MoveSpeed>, RefRW<PhysicsVelocity>>()
                .WithAll<UnitTag>())
        {
            float3 dir = moveDir.ValueRO.Value + separation.ValueRO.Value;
            if (math.lengthsq(dir) > 1f) dir = math.normalize(dir); //separation can't push past max speed

            float3 vel = dir * moveSpeed.ValueRO.Value;
            velocity.ValueRW.Linear  = new float3(vel.x, 0f, vel.z);
            velocity.ValueRW.Angular = float3.zero;
        }
    }
}

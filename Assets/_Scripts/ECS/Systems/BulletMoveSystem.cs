using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct BulletMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<PhysicsVelocity> physicsVelocity, RefRW<BulletConfig> config) in
            SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<BulletConfig>>())
        {
            config.ValueRW.velocity.y -= SimConstants.GRAVITY * config.ValueRO.gravityScale * deltaTime;

            physicsVelocity.ValueRW.Linear  = config.ValueRO.velocity;
            physicsVelocity.ValueRW.Angular = float3.zero;
        }
    }
}

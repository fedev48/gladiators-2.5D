using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(MovementAnimationSystem))]
public partial struct SkeletonEmergeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRW<LocalTransform> transform,
                  RefRW<SkeletonSpawnData> spawnData,
                  RefRO<PhysicsCollider> collider,
                  RefRW<PhysicsVelocity> velocity,
                  Entity entity) in
                 SystemAPI.Query<RefRW<LocalTransform>,
                                 RefRW<SkeletonSpawnData>,
                                 RefRO<PhysicsCollider>,
                                 RefRW<PhysicsVelocity>>()
                     .WithAll<SpawnState>()
                     .WithEntityAccess())
        {
            if (spawnData.ValueRO.height == 0f)
            {
                float height = collider.ValueRO.Value.Value.CalculateAabb(RigidTransform.identity).Max.y
                             - collider.ValueRO.Value.Value.CalculateAabb(RigidTransform.identity).Min.y;
                spawnData.ValueRW.height = height;
                transform.ValueRW.Position = spawnData.ValueRO.surfacePos - new float3(0f, height, 0f);
            }

            velocity.ValueRW.Linear  = float3.zero;
            velocity.ValueRW.Angular = float3.zero;
            transform.ValueRW.Rotation = quaternion.identity;

            float3 current = transform.ValueRO.Position;
            float  targetY = spawnData.ValueRO.surfacePos.y;
            float  newY    = math.min(current.y + spawnData.ValueRO.height * deltaTime, targetY);
            float  shake   = math.sin(time * 40f) * 0.04f;

            transform.ValueRW.Position = new float3(
                spawnData.ValueRO.surfacePos.x + shake,
                newY,
                spawnData.ValueRO.surfacePos.z
            );

            if (newY >= targetY)
            {
                transform.ValueRW.Position = spawnData.ValueRO.surfacePos;
                state.EntityManager.SetComponentEnabled<SpawnState>(entity, false);
                state.EntityManager.SetComponentEnabled<FollowState>(entity, true);
                state.EntityManager.SetComponentEnabled<ShouldSnapToFloorTag>(entity, true);
            }
        }

        // Animación de emerge — solo si el skeleton tiene SpriteAnimationState
        foreach (var animState in
            SystemAPI.Query<RefRW<SpriteAnimationState>>()
                .WithAll<SpawnState, SkeletonTag>())
        {
            animState.ValueRW.currentAnimation = 8; // emerge
        }
    }
}

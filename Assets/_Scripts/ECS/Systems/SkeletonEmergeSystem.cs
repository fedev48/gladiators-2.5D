using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(MovementAnimRequestSystem))]
public partial struct SkeletonEmergeSystem : ISystem
{
    ComponentLookup<AnimRequest> _animRequestLookup;

    public void OnCreate(ref SystemState state)
    {
        _animRequestLookup = state.GetComponentLookup<AnimRequest>(isReadOnly: false);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _animRequestLookup.Update(ref state);

        float deltaTime = SystemAPI.Time.DeltaTime;
        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach ((RefRW<LocalTransform> transform,
                  RefRW<SkeletonSpawnData> spawnData,
                  RefRW<PhysicsVelocity> velocity,
                  RefRO<VisualEntity> visual,
                  Entity entity) in
                 SystemAPI.Query<RefRW<LocalTransform>,
                                 RefRW<SkeletonSpawnData>,
                                 RefRW<PhysicsVelocity>,
                                 RefRO<VisualEntity>>()
                     .WithAll<SpawnState>()
                     .WithEntityAccess())
        {
            Entity visualEntity = visual.ValueRO.Value;
            if (_animRequestLookup.HasComponent(visualEntity))
                _animRequestLookup.GetRefRW(visualEntity).ValueRW.role = Animation.Emerge;

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
    }
}

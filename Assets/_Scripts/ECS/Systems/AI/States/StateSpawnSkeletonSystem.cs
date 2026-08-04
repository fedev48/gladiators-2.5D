using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
[UpdateBefore(typeof(StateManagerSystem))]
[UpdateAfter(typeof(MovementAnimRequestSystem))]
public partial struct StateSpawnSkeletonSystem : ISystem
{
    ComponentLookup<AnimRequest> animRequestLookup;
    TypeIndex followState;

    public void OnCreate(ref SystemState state)
    {
        animRequestLookup = state.GetComponentLookup<AnimRequest>(isReadOnly: false);

        followState = TypeManager.GetTypeIndex<FollowState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        animRequestLookup.Update(ref state);

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
            Entity visualEntity = visual.ValueRO.value;
            if (animRequestLookup.HasComponent(visualEntity))
                animRequestLookup.GetRefRW(visualEntity).ValueRW.role = Animation.Emerge;

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
                ExitState(ref state, entity);
            }
        }
    }

    void ExitState(ref SystemState state, Entity entity)
    {
        state.EntityManager.SetComponentEnabled<ShouldSnapToFloorTag>(entity, true);
        state.EntityManager.SetComponentEnabled<ChangeStateRequest>(entity, true);
        DynamicBuffer<ChangeStateRequest> requests =  state.EntityManager.GetBuffer<ChangeStateRequest>(entity);

        requests.Add(new ChangeStateRequest
        {
            targetState = followState,
            priority = 1
        });

    }
}

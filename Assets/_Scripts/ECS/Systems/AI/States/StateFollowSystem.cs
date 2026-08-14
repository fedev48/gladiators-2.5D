using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(StateManagerSystem))]
[UpdateBefore(typeof(MoveToDestinationSystem))]
public partial struct StateFollowSystem : ISystem
{
    const float PLAYER_IDLE_SECONDS = 0.5f; //time the player must stay in the same cell before skeletons spread to their formation offsets

    int2  lastPlayerCell;
    float playerStillTime;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        lastPlayerCell = new int2(int.MinValue, int.MinValue);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        GridConfig gridConfig = SystemAPI.GetSingleton<GridConfig>();

        float3 playerPos = float3.zero;
        foreach (RefRO<LocalTransform> playerTransform in
            SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
        {
            playerPos = playerTransform.ValueRO.Position;
        }

        int2 playerCell = GridSystem.WorldPosToCoords(playerPos, gridConfig);
        if (!playerCell.Equals(lastPlayerCell))
        {
            lastPlayerCell  = playerCell;
            playerStillTime = 0f;
        }
        else
        {
            playerStillTime += dt;
        }
        bool playerIsIdle = playerStillTime >= PLAYER_IDLE_SECONDS;

        foreach ((RefRO<LocalTransform> transform,
                  RefRW<SkeletonSpawnData> spawnData,
                  RefRW<MoveDestination> destination,
                  Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>,
                            RefRW<SkeletonSpawnData>,
                            RefRW<MoveDestination>>()
                .WithAll<FollowState>()
                .WithNone<MovementBlocked>()
                .WithPresent<MoveDestination>()
                .WithEntityAccess())
        {
            if (spawnData.ValueRO.followOffset.Equals(float3.zero))
                spawnData.ValueRW.followOffset = transform.ValueRO.Position - playerPos;

            destination.ValueRW.value = playerIsIdle ? playerPos + spawnData.ValueRO.followOffset : playerPos;
            SystemAPI.SetComponentEnabled<MoveDestination>(entity, true);
        }
    }
}

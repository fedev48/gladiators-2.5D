using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct SkeletonFollowSystem : ISystem
{
    [BurstCompile]
    public readonly void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        float3 playerPos = float3.zero;
        foreach (RefRO<LocalTransform> playerTransform in
            SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
        {
            playerPos = playerTransform.ValueRO.Position;
        }

        foreach ((RefRW<LocalTransform> transform,
                  RefRW<SkeletonSpawnData> spawnData,
                  RefRO<SkeletonConfig> config,
                  RefRW<MoveDirection> moveDir,
                  RefRW<MoveSpeed> moveSpeed) in
            SystemAPI.Query<RefRW<LocalTransform>,
                            RefRW<SkeletonSpawnData>,
                            RefRO<SkeletonConfig>,
                            RefRW<MoveDirection>,
                            RefRW<MoveSpeed>>()
                .WithAll<FollowState>())
        {
            if (spawnData.ValueRO.followOffset.Equals(float3.zero))
                spawnData.ValueRW.followOffset = transform.ValueRO.Position - playerPos;

            float3 target   = playerPos + spawnData.ValueRO.followOffset;
            float3 toTarget = target - transform.ValueRO.Position;
            toTarget.y = 0f;

            float  dist         = math.length(toTarget);
            float3 direction    = math.normalizesafe(toTarget);
            float  acceleration = config.ValueRO.acceleration;
            float  currentSpeed = moveSpeed.ValueRO.Value;

            float stoppingDist = currentSpeed * currentSpeed / (2f * acceleration);

            if (dist > stoppingDist + 0.05f)
                currentSpeed = math.min(currentSpeed + acceleration * dt, config.ValueRO.maxSpeed);
            else
                currentSpeed = math.max(currentSpeed - acceleration * dt, 0f);

            moveDir.ValueRW.Value   = currentSpeed > 0.01f ? direction : float3.zero;
            moveSpeed.ValueRW.Value = currentSpeed;

            transform.ValueRW.Rotation = quaternion.identity;
        }
    }
}

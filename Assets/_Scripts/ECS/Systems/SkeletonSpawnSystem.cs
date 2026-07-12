using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(InputReaderSystem))]
[BurstCompile]
public partial struct SkeletonSpawnSystem : ISystem
{
    private Unity.Mathematics.Random random;
    private int groundMask;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        random = Unity.Mathematics.Random.CreateFromIndex((uint)System.DateTime.Now.Ticks);
        groundMask = LayerMask.GetMask("Ground");
    }

    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences refs = SystemAPI.GetSingleton<EntitiesReferences>();
        float prefabScale = SystemAPI.GetComponent<LocalTransform>(refs.skeletonPrefabEntity).Scale;

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRO<LocalTransform> transform,
                  RefRO<SkeletonSpellConfig> spellConfig,
                  RefRO<SummonSkeletonEvent> summonEvent,
                  Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<SkeletonSpellConfig>, RefRO<SummonSkeletonEvent>>()
                .WithAll<SummonSkeletonEvent>()
                .WithEntityAccess())
        {
            float3 playerPos = transform.ValueRO.Position;
            SkeletonConfig prefabConfig = SystemAPI.GetComponent<SkeletonConfig>(refs.skeletonPrefabEntity);
            int count = math.max(1, summonEvent.ValueRO.count);

            for (int i = 0; i < count; i++)
            {
                if (!TryGetSpawnPosition(playerPos, spellConfig.ValueRO.minRadius, spellConfig.ValueRO.maxRadius, groundMask, ref random, out float3 spawnPos)) continue;

                float acceleration = random.NextFloat(prefabConfig.accelerationMin, prefabConfig.accelerationMax);

                Entity skeleton = ecb.Instantiate(refs.skeletonPrefabEntity);
                ecb.SetComponent(skeleton, LocalTransform.FromPositionRotationScale(spawnPos, quaternion.identity, prefabScale));
                ecb.SetComponent(skeleton, new SkeletonConfig
                {
                    accelerationMin = prefabConfig.accelerationMin,
                    accelerationMax = prefabConfig.accelerationMax,
                    maxSpeed        = prefabConfig.maxSpeed,
                    acceleration    = acceleration
                });
                ecb.AddComponent(skeleton, new SkeletonSpawnData { surfacePos = spawnPos });
            }

            state.EntityManager.SetComponentEnabled<SummonSkeletonEvent>(entity, false);
        }
    }

    private static bool TryGetSpawnPosition(float3 playerPos, float minRadius, float maxRadius, int groundMask, ref Unity.Mathematics.Random random, out float3 result)
    {
        const int maxAttempts = 10;

        for (int i = 0; i < maxAttempts; i++)
        {
            float angle = random.NextFloat(0f, math.PI * 2f);
            float dist  = random.NextFloat(minRadius, maxRadius);

            float3 candidate = playerPos + new float3(math.cos(angle) * dist, 0f, math.sin(angle) * dist);

            if (Physics.Raycast(candidate + new float3(0f, 10f, 0f), Vector3.down, out RaycastHit hit, 20f, groundMask))
            {
                result = new float3(hit.point.x, hit.point.y, hit.point.z);
                return true;
            }
        }

        result = float3.zero;
        return false;
    }
}

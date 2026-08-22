using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(InputReaderSystem))]
public partial struct SkeletonSpawnSystem : ISystem
{
    struct CorpseCandidate
    {
        public Entity entity;
        public float3 position;
    }

    private Unity.Mathematics.Random random;
    private int groundMask;
    private uint wallLayerMask;
    const float SIGHT_HEIGHT_OFFSET = 1f;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        state.RequireForUpdate<Unity.Physics.PhysicsWorldSingleton>();
        random = Unity.Mathematics.Random.CreateFromIndex(1234);
        groundMask = LayerMask.GetMask("Ground");
        wallLayerMask = (uint)(1 << LayerMask.NameToLayer("Walls"));
    }

    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences refs = SystemAPI.GetSingleton<EntitiesReferences>();
        float prefabScale = SystemAPI.GetComponent<LocalTransform>(refs.skeletonPrefabEntity).Scale;

        Unity.Physics.Aabb prefabAabb = SystemAPI.GetComponent<Unity.Physics.PhysicsCollider>(refs.skeletonPrefabEntity).Value.Value.CalculateAabb(RigidTransform.identity);
        float boundsMinY = prefabAabb.Min.y * prefabScale;
        float boundsMaxY = prefabAabb.Max.y * prefabScale;
        float skeletonHeight = boundsMaxY - boundsMinY;
        Unity.Physics.CollisionWorld collisionWorld = SystemAPI.GetSingleton<Unity.Physics.PhysicsWorldSingleton>().CollisionWorld;
        float3 sightOffset = new float3(0f, SIGHT_HEIGHT_OFFSET, 0f);

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
            MovementConfig prefabMovement = SystemAPI.GetComponent<MovementConfig>(refs.skeletonPrefabEntity);
            int count = math.max(1, summonEvent.ValueRO.count);

            float maxRadiusSq = spellConfig.ValueRO.maxRadius * spellConfig.ValueRO.maxRadius;

            NativeList<CorpseCandidate> corpseCandidates = new(Allocator.Temp);

            foreach ((RefRO<LocalTransform> corpseTransform, Entity corpseEntity) in
                SystemAPI.Query<RefRO<LocalTransform>>()
                    .WithAll<CorpseTag>()
                    .WithEntityAccess())
            {
                float3 corpsePos = corpseTransform.ValueRO.Position;
                if (math.distancesq(corpsePos, playerPos) > maxRadiusSq) continue;

                corpseCandidates.Add(new CorpseCandidate { entity = corpseEntity, position = corpsePos });
            }

            for (int i = 0; i < count && corpseCandidates.Length > 0; i++)
            {
                int pick = random.NextInt(0, corpseCandidates.Length);
                CorpseCandidate corpse = corpseCandidates[pick];
                corpseCandidates.RemoveAtSwapBack(pick);

                bool reachable = TryGetGroundPosition(corpse.position, groundMask, out float3 spawnPos)
                    && GridSystem.HasLineOfSight(playerPos + sightOffset, spawnPos + sightOffset, collisionWorld, wallLayerMask);

                if (!reachable)
                {
                    i--;
                    continue;
                }

                Entity despawnRequest = ecb.CreateEntity();
                ecb.AddComponent(despawnRequest, new CorpseDespawRequest { corpseEntity = corpse.entity });

                float acceleration = random.NextFloat(prefabConfig.accelerationMin, prefabConfig.accelerationMax);

                //standing: bottom of the collider on the ground. buried: top of the collider at ground level
                float3 surfacePos = new float3(spawnPos.x, spawnPos.y - boundsMinY, spawnPos.z);
                float3 startPos   = new float3(spawnPos.x, spawnPos.y - boundsMaxY, spawnPos.z);

                Entity skeleton = ecb.Instantiate(refs.skeletonPrefabEntity);
                ecb.SetComponent(skeleton, LocalTransform.FromPositionRotationScale(startPos, quaternion.identity, prefabScale));
                ecb.SetComponent(skeleton, new MovementConfig
                {
                    acceleration = acceleration,
                    maxSpeed     = prefabMovement.maxSpeed
                });
                ecb.AddComponent(skeleton, new SkeletonSpawnData { surfacePos = surfacePos, height = skeletonHeight });
            }

            corpseCandidates.Dispose();
            state.EntityManager.SetComponentEnabled<SummonSkeletonEvent>(entity, false);
        }
    }

    private static bool TryGetGroundPosition(float3 candidate, int groundMask, out float3 result)
    {
        if (Physics.Raycast(candidate + new float3(0f, 10f, 0f), Vector3.down, out RaycastHit hit, 20f, groundMask))
        {
            result = new float3(hit.point.x, hit.point.y, hit.point.z);
            return true;
        }

        result = float3.zero;
        return false;
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(InputReaderSystem))]
public partial struct SkeletonSpawnSystem : ISystem
{
    private Unity.Mathematics.Random random;
    private int groundMask;
    private uint wallLayerMask;
    const float SIGHT_HEIGHT_OFFSET = 1f;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<CellComponentsForCorpseCount>();
        state.RequireForUpdate<GridBlueprintTag>();
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
        float skeletonHeight = prefabAabb.Max.y - prefabAabb.Min.y;
        GridConfig config = SystemAPI.GetSingleton<GridConfig>();
        Entity blueprintEntity = SystemAPI.GetSingletonEntity<GridBlueprintTag>();
        DynamicBuffer<CellComponents> cells = SystemAPI.GetBuffer<CellComponents>(blueprintEntity);
        DynamicBuffer<CellComponentsForCorpseCount> corpses = SystemAPI.GetSingletonBuffer<CellComponentsForCorpseCount>();
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

            
            int2 playerCell = GridSystem.WorldPosToCoords(playerPos, config);
            int cellRadius = (int)math.ceil(spellConfig.ValueRO.maxRadius / config.cellSize);
            FixedList4096Bytes<int2> surroundingCells = GridSystem.GetSurroundingCells(playerCell, cellRadius, roundedCorners: true);

            NativeList<int> cellsWithCorpses = new(Allocator.Temp);

            foreach (int2 coords in surroundingCells)
            {
                if (!GridSystem.CheckIfCoordsIsInBounds(coords, config)) continue;

                int cellIndex = GridSystem.CoordsToIndex(coords.x, coords.y, config);
                if (cells[cellIndex].cost == GridSystem.WALL_COST) continue;
                if (corpses[cellIndex].currentBuriedBodies <= 0) continue;

                cellsWithCorpses.Add(cellIndex);
            }

            for (int i = 0; i < count && cellsWithCorpses.Length > 0; i++)
            {
                int pick = random.NextInt(0, cellsWithCorpses.Length);
                int cellIndex = cellsWithCorpses[pick];

                float3 cellOrigin = GridSystem.FlatIndexToWorldPosition(cellIndex, config);
                float2 pointInCell = random.NextFloat2(float2.zero, new float2(config.cellSize, config.cellSize));
                float3 candidate = new float3(cellOrigin.x + pointInCell.x, playerPos.y, cellOrigin.z + pointInCell.y);

                bool reachable = TryGetGroundPosition(candidate, groundMask, out float3 spawnPos)
                    && GridSystem.HasLineOfSight(playerPos + sightOffset, spawnPos + sightOffset, collisionWorld, wallLayerMask);

                if (!reachable)
                {
                    cellsWithCorpses.RemoveAtSwapBack(pick);
                    i--;
                    continue;
                }

                corpses.ElementAt(cellIndex).currentBuriedBodies--;
                if (corpses[cellIndex].currentBuriedBodies <= 0) cellsWithCorpses.RemoveAtSwapBack(pick); 

                float acceleration = random.NextFloat(prefabConfig.accelerationMin, prefabConfig.accelerationMax);

                Entity skeleton = ecb.Instantiate(refs.skeletonPrefabEntity);
                ecb.SetComponent(skeleton, LocalTransform.FromPositionRotationScale(spawnPos - new float3(0f, skeletonHeight, 0f), quaternion.identity, prefabScale));
                ecb.SetComponent(skeleton, new MovementConfig
                {
                    acceleration = acceleration,
                    maxSpeed     = prefabMovement.maxSpeed
                });
                ecb.AddComponent(skeleton, new SkeletonSpawnData { surfacePos = spawnPos, height = skeletonHeight });
            }

            cellsWithCorpses.Dispose();
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

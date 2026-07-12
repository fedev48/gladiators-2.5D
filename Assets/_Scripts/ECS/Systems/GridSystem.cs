using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup))]
partial struct GridSystem : ISystem
{
    public const int WALL_COST = int.MaxValue;
    uint wallLayerMask;

    public void OnCreate(ref SystemState state)
    {
        wallLayerMask = (uint)(1 << UnityEngine.LayerMask.NameToLayer("Walls"));
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach ((RefRO<GridConfig> config, Entity entity) in
            SystemAPI.Query<RefRO<GridConfig>>()
                .WithAll<IsBlueprintPendingTag>()
                .WithEntityAccess())
        {
            
            Entity gridEntity = entityCommandBuffer.CreateEntity();

            entityCommandBuffer.AddComponent(gridEntity, new FlowFieldMap { FlowFieldId = 0, DestinationCellIndex = -1 }); //-1: the blueprint has no real destination, 0 would be a valid cell
            entityCommandBuffer.AddComponent(gridEntity, new GridBlueprintTag());
            entityCommandBuffer.SetName(gridEntity, "GridBlueprint");

            var buffer = entityCommandBuffer.AddBuffer<CellComponents>(gridEntity);
            
            PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
             
            for (int y = 0; y < config.ValueRO.height; y++)
            {
                for (int x = 0; x < config.ValueRO.width; x++)
                {
                    int cost = 1;
                   
                    if (IsOnWall(CoordsToWorldPosition(x, y, config.ValueRO),collisionWorld, wallLayerMask, config.ValueRO.cellSize))
                    {
                        cost = WALL_COST;
                    }

                    buffer.Add(new CellComponents
                    {
                        cost = cost,
                        bestCost = -1,
                    });

                }
            }

            entityCommandBuffer.SetComponentEnabled<IsBlueprintPendingTag>(entity, false);
        }
        entityCommandBuffer.Playback(state.EntityManager);
        entityCommandBuffer.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    public static bool IsOnWall(float3 position, CollisionWorld collisionWorld, uint wallLayerMask,  float size = 0)
    {
        float3 centeredPosition;
        if (size!=0)
        {
            centeredPosition = new float3(position.x + size * 0.5f, position.y, position.z + size * 0.5f);
        }
        else
        {
            centeredPosition = position;
        }
        
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        CollisionFilter filter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = wallLayerMask,
            GroupIndex = 0
        };

        bool isWall = collisionWorld.OverlapSphere(centeredPosition, size * 0.5f, ref hits, filter);
        hits.Dispose();
        return isWall;
    }

    public static float3 CoordsToWorldPosition(int x, int y, GridConfig gridConfig)
    {
        return new float3(x * gridConfig.cellSize, 0, y * gridConfig.cellSize);
    }

    public static float3 FlatIndexToWorldPosition(int cellFlatIndex, GridConfig gridConfig)
    {
        int2 coords = IndexToCoords(cellFlatIndex, gridConfig);
        return new float3(CoordsToWorldPosition(coords.x, coords.y, gridConfig));
    }

    public static int2 WorldPosToCoords (float3 position, GridConfig gridConfig)
    {
        return new int2 ((int)(position.x/gridConfig.cellSize), (int)(position.z/gridConfig.cellSize));
    }

    public static int WorldPosToIndex(float3 position, GridConfig gridConfig)
    {
        int2 positionToCoords = WorldPosToCoords(position, gridConfig);
        return CoordsToIndex(positionToCoords.x, positionToCoords.y, gridConfig);
    }

    public static bool CheckIfCoordsIsInBounds(int2 cell, GridConfig config)
    {
        if (cell.x < 0 || cell.x >= config.width || cell.y < 0 || cell.y >= config.height) return false;
        return true;
    }

    public static int CoordsToIndex(int x, int y, GridConfig config) => y * config.width + x;

    public static int2 IndexToCoords(int index, GridConfig config) => new(index % config.width, index / config.width);

    
    public static FixedList128Bytes<int2> GetSurroundingCells(int2 cellCoords) //FixedList128Bytes instead of a regular array for burst compilation
    {
        FixedList128Bytes<int2> surroundingCoords = new();

        for (int i = 0; i < 9; i++)
        {
            int dx = (i % 3) - 1;
            int dy = (i / 3) - 1;
            if (dx == 0 && dy == 0) continue; //skip the central cell
            surroundingCoords.Add(new int2(cellCoords.x + dx, cellCoords.y + dy)); //starting with -1,-1
        }

        return surroundingCoords; //this is just a coords list
    }
}



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
        state.EntityManager.AddComponent<CellComponentsForCorpseCount>(state.SystemHandle);//creates a buffer bcause CellComponentsForCorpseCount is IBufferElementData
        state.RequireForUpdate<GridConfig>();

    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        DynamicBuffer<CellComponentsForCorpseCount> bufferCorpses = state.EntityManager.GetBuffer<CellComponentsForCorpseCount>(state.SystemHandle);
        PhysicsWorldSingleton physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorldSingleton.CollisionWorld;
        foreach ((RefRO<GridConfig> config, Entity entity) in
            SystemAPI.Query<RefRO<GridConfig>>()
                .WithAll<IsBlueprintPendingTag>()
                .WithEntityAccess())
        {
            
            Entity gridEntity = entityCommandBuffer.CreateEntity();

            entityCommandBuffer.AddComponent(gridEntity, new FlowFieldMap { flowFieldId = 0, destinationCellIndex = -1 }); //-1: the blueprint has no real destination, 0 would be a valid cell
            entityCommandBuffer.AddComponent(gridEntity, new GridBlueprintTag());
            entityCommandBuffer.SetName(gridEntity, "GridBlueprint");

            var buffer = entityCommandBuffer.AddBuffer<CellComponents>(gridEntity);
        

             
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

                    bufferCorpses.Add(new CellComponentsForCorpseCount {currentBuriedBodies = 3});

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

    public static bool HasLineOfSight(float3 from, float3 to, in CollisionWorld collisionWorld, uint wallLayerMask)
    {
        RaycastInput ray = new RaycastInput
        {
            Start  = from,
            End    = to,
            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = wallLayerMask, GroupIndex = 0 }
        };

        return !collisionWorld.CastRay(ray);
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

    public static int2 WorldPosToCoords (float3 position, float cellSize) => (int2)math.floor(position.xz / cellSize);

    public static int2 WorldPosToCoords (float3 position, GridConfig gridConfig) => WorldPosToCoords(position, gridConfig.cellSize);
    

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


    public static FixedList4096Bytes<int2> GetSurroundingCells(int2 cellCoords, int amountOfCellsToCheck, bool roundedCorners = false, bool skipCentralCell = false) //FixedList4096Bytes fits up to amountOfCellsToCheck = 10
    {
        FixedList4096Bytes<int2> surroundingCoords = new();
        int sideSize = amountOfCellsToCheck*2+1;

        for (int i = 0; i < sideSize*sideSize; i++)
        {
            int dx = (i % sideSize) - amountOfCellsToCheck;
            int dy = (i / sideSize) - amountOfCellsToCheck;

            if (skipCentralCell && dx == 0 && dy == 0) continue;

            if (roundedCorners && amountOfCellsToCheck > 1) //for a circular (kind ) area
            {
                int adx = math.abs(dx);
                int ady = math.abs(dy);
                if (ady == amountOfCellsToCheck && adx > amountOfCellsToCheck - 2) continue; //drop 2 cells per side
                if (ady == amountOfCellsToCheck - 1 && adx > amountOfCellsToCheck - 1) continue; //drop 1 cell per side
            }

            surroundingCoords.Add(new int2(cellCoords.x + dx, cellCoords.y + dy));
        }

        return surroundingCoords; //this is just a coords list
    }

    //each shadow packs: xy = direction to the wall, z = distance to it, w = cosine of its half angle
    public static NativeList<float4> BuildWallShadows(in FixedList4096Bytes<int2> surroundingCells, float2 originCellCenter, in GridConfig config, in DynamicBuffer<CellComponents> cells, float shadowWidthMult)
    {
        NativeList<float4> wallShadows = new(Allocator.Temp);

        foreach (int2 coords in surroundingCells)
        {
            if (!CheckIfCoordsIsInBounds(coords, config)) continue;
            if (cells[CoordsToIndex(coords.x, coords.y, config)].cost != WALL_COST) continue;

            float2 toWall = (new float2(coords.x, coords.y) + 0.5f) * config.cellSize - originCellCenter;
            float wallDistance = math.length(toWall);
            if (wallDistance < 0.0001f) continue;

            float shadowHalfAngle = math.atan(config.cellSize * 0.5f / wallDistance) * shadowWidthMult;
            wallShadows.Add(new float4(toWall / wallDistance, wallDistance, math.cos(shadowHalfAngle)));
        }

        return wallShadows;
    }

    public static bool IsBehindWall(float2 toCell, NativeList<float4> wallShadows)
    {
        float cellDistance = math.length(toCell);
        if (cellDistance < 0.0001f) return false;

        float2 direction = toCell / cellDistance;

        foreach (float4 shadow in wallShadows)
        {
            if (cellDistance <= shadow.z) continue;
            if (math.dot(direction, shadow.xy) > shadow.w) return true;
        }

        return false;
    }
}



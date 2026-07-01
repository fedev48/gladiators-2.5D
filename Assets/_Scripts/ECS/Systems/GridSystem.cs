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

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    { 
        EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach ((RefRO<GridConfig> config, Entity entity) in
            SystemAPI.Query<RefRO<GridConfig>>()
                .WithAll<IsBlueprintPendingTag>()
                .WithEntityAccess())
        {
            
            Entity gridEntity = entityCommandBuffer.CreateEntity();

            entityCommandBuffer.AddComponent(gridEntity, new FlowFieldMap { FlowFieldId = 0, Destination = Unity.Mathematics.float3.zero });
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
                   
                    if (IsOnWall(GridToWorldPosition(x, y, config.ValueRO),collisionWorld, wallLayerMask, config.ValueRO.cellSize))
                    {
                        cost = WALL_COST;
                    }

                    buffer.Add(new CellComponents 
                    { 
                        cost = cost, 
                        bestCost = -1, 
                        x = x+1, 
                        y = y+1     
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

    public static float3 GridToWorldPosition(int x, int y, GridConfig gridConfig)
    {
        return new float3 (x*gridConfig.cellSize, 0 , y*gridConfig.cellSize);
    }
}



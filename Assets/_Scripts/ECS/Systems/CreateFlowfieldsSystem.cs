using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup))]
partial struct CreateFlowfieldsSystem : ISystem
{
    uint wallLayerMask;
    NativeQueue<Entity> flowFieldPool;
    const int MAX_FLOW_FIELDS = 50;

    struct RequestData
    {
        public Entity entity;
        public float3 destination;
    }

    public void OnCreate(ref SystemState state)
    {
        wallLayerMask = (uint)(1 << UnityEngine.LayerMask.NameToLayer("Walls"));
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridBlueprintTag>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<NeedsPathfinding>().Build());

        flowFieldPool = new NativeQueue<Entity>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        Entity blueprintEntity = SystemAPI.GetSingletonEntity<GridBlueprintTag>();

        NativeList<RequestData> newFlowFieldRequests = new(Allocator.Temp);

       
        foreach ((RefRO<LocalTransform>  transform,
                  RefRW<UsingPathfinding> usingPathfinding,
                  RefRO<NeedsPathfinding> needsPathfinding,
                  Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRW<UsingPathfinding>, RefRO<NeedsPathfinding>>()
                .WithAll<NeedsPathfinding>()
                .WithPresent<UsingPathfinding>()
                .WithEntityAccess())
        {
            float3 fromPoint = transform.ValueRO.Position;
            float3 toPoint   = needsPathfinding.ValueRO.Destination;
            state.EntityManager.SetComponentEnabled<NeedsPathfinding>(entity, false); //we always must have this disabled after running this query

            var rayInput = new RaycastInput
            {
                Start  = fromPoint,
                End    = toPoint,
                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = wallLayerMask, GroupIndex = 0 }
            };

            bool hasLineOfSight    = !collisionWorld.CastRay(rayInput);
            bool destinationOnWall = GridSystem.IsOnWall(toPoint, collisionWorld, wallLayerMask);
            bool skipPathfinding   = hasLineOfSight || destinationOnWall;

            if (skipPathfinding)
            {
                state.EntityManager.SetComponentEnabled<UsingPathfinding>(entity, false);
                continue; // no pathfinding needed because of line of sight o invalid target, en este punto la unidad no sigue ningun path asi que lo desactivamos por si era un override
            }

            bool flowFieldToTargetExists = false;
            foreach (RefRO<FlowFieldMap> flowFieldMap in SystemAPI.Query<RefRO<FlowFieldMap>>().WithAbsent<GridBlueprintTag>())
            {
                if (flowFieldMap.ValueRO.Destination.Equals(toPoint))
                {
                    usingPathfinding.ValueRW.flowFieldId = flowFieldMap.ValueRO.FlowFieldId;
                    state.EntityManager.SetComponentEnabled<UsingPathfinding>(entity, true);
                    flowFieldToTargetExists = true;
                    break;
                }
            }

            if (flowFieldToTargetExists) continue; //a previous FF has this target

            newFlowFieldRequests.Add(new RequestData { entity = entity, destination = toPoint });
        }

        ResolveNewFlowFieldRequests(ref state, blueprintEntity, newFlowFieldRequests);

        newFlowFieldRequests.Dispose();
    }

    void ResolveNewFlowFieldRequests(ref SystemState state, Entity blueprintEntity, NativeList<RequestData> newFlowFieldRequests)
    {
        
        NativeHashMap<float3, int> destinationToId = new(newFlowFieldRequests.Length, Allocator.Temp);

        for (int i = 0; i < newFlowFieldRequests.Length; i++)
        {
            float3 destination = newFlowFieldRequests[i].destination;
            if (destinationToId.ContainsKey(destination)) continue; // destination alaready processed

            int assignedId;

            if (flowFieldPool.Count >= MAX_FLOW_FIELDS)
            {
                Entity recycledFlowField = flowFieldPool.Dequeue();
                assignedId = state.EntityManager.GetComponentData<FlowFieldMap>(recycledFlowField).FlowFieldId;
                state.EntityManager.SetComponentData(recycledFlowField, new FlowFieldMap { FlowFieldId = assignedId, Destination = destination });
                // todo: calculate paths
                flowFieldPool.Enqueue(recycledFlowField);
            }
            else
            {
                assignedId = flowFieldPool.Count;
                Entity newFlowField = state.EntityManager.Instantiate(blueprintEntity);
                state.EntityManager.RemoveComponent<GridBlueprintTag>(newFlowField);
                state.EntityManager.SetComponentData(newFlowField, new FlowFieldMap { FlowFieldId = assignedId, Destination = destination });
                // todo: calculate paths
                flowFieldPool.Enqueue(newFlowField);
            }

            destinationToId.Add(destination, assignedId);
        }

        
        for (int i = 0; i < newFlowFieldRequests.Length; i++)
        {
            Entity entity      = newFlowFieldRequests[i].entity;
            float3 destination = newFlowFieldRequests[i].destination;
            int    flowFieldId = destinationToId[destination];

            state.EntityManager.SetComponentData(entity, new UsingPathfinding { flowFieldId = flowFieldId });
            state.EntityManager.SetComponentEnabled<UsingPathfinding>(entity, true);
        }

        destinationToId.Dispose();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        flowFieldPool.Dispose();
    }
}

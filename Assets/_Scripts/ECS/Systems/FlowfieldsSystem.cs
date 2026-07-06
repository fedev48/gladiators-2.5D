using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup))]
partial struct FlowfieldsSystem : ISystem
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
                continue; // no pathfinding needed because of line of sight o invalid target, at this point we need to disable the tag for the pathfinding following in case this is an override and the PF is no longer needed
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
                CalculateIntegrationField(ref state, recycledFlowField);
                CalculateVectorField(ref state, recycledFlowField);
                flowFieldPool.Enqueue(recycledFlowField);
            }
            else
            {
                assignedId = flowFieldPool.Count;
                Entity newFlowField = state.EntityManager.Instantiate(blueprintEntity);
                state.EntityManager.RemoveComponent<GridBlueprintTag>(newFlowField);
                state.EntityManager.SetComponentData(newFlowField, new FlowFieldMap { FlowFieldId = assignedId, Destination = destination });
                CalculateIntegrationField(ref state, newFlowField);
                CalculateVectorField(ref state, newFlowField);
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

    void CalculateIntegrationField(ref SystemState state, Entity flowFieldParentEntity)
    {
        GridConfig config = SystemAPI.GetSingleton<GridConfig>();
        FlowFieldMap flowFieldMap = SystemAPI.GetComponent<FlowFieldMap>(flowFieldParentEntity);
        DynamicBuffer<CellComponents> cellComponents = SystemAPI.GetBuffer<CellComponents>(flowFieldParentEntity);
        int2 targetCell = GridSystem.WorldPosToGrid(flowFieldMap.Destination, config);
        int flatTargetIndex = GridSystem.CoordsToIndex(targetCell.x, targetCell.y, config);
        ref CellComponents cell = ref cellComponents.ElementAt(flatTargetIndex);
        cell.bestCost = 0;
        cell.cost = 0;
        NativeQueue<int> nativeQueueCoords = new(Allocator.Temp);
        nativeQueueCoords.Enqueue(flatTargetIndex);

        int securityCount = 0;

        while (nativeQueueCoords.Count!=0 && securityCount<10000)
        {
            int nextIndex = nativeQueueCoords.Dequeue();
            ProcessCurrentGridNeighbours(cellComponents, nextIndex, GridSystem.IndexToCoords(nextIndex, config), nativeQueueCoords, config);
            securityCount++;
        }
        nativeQueueCoords.Dispose();
    }

    void CalculateVectorField(ref SystemState state, Entity flowFieldParentEntity)
    {
        DynamicBuffer<CellComponents> cellComponents = SystemAPI.GetBuffer<CellComponents>(flowFieldParentEntity);
        GridConfig config = SystemAPI.GetSingleton<GridConfig>();

        for (int i = 0; i < cellComponents.Length; i++)
        {
            int2[] surroundingCellsCoords = GetSurroundingCells(GridSystem.IndexToCoords(i, config));
            int lowestCost = 10000;
            int lowestCostCell = -1;

            for (int j = 0; j < surroundingCellsCoords.Length; j++)
            {
                if (!CheckIfIndexIsInBounds(surroundingCellsCoords[j], config)) continue;
                int flatTargetIndex = GridSystem.CoordsToIndex(surroundingCellsCoords[j].x, surroundingCellsCoords[j].y, config);
                ref CellComponents cell = ref cellComponents.ElementAt(flatTargetIndex);
                if (cell.bestCost >= 0 && cell.bestCost<lowestCost)
                {
                    lowestCost = cell.bestCost;
                    lowestCostCell = flatTargetIndex;
                }
            }

            ref CellComponents targetCell = ref cellComponents.ElementAt(i);

            if (lowestCostCell != -1 && targetCell.cost != 0)
            {
                targetCell.movingVector = CalculateMoveVectorForField(i, lowestCostCell, config);
            }

            
        }
    }

    
    float2 CalculateMoveVectorForField(int originFlatIndex, int destinyFlatIndex, GridConfig gridConfig)
    {
        float3 cellFloat3 = GridSystem.FlatIndexToWorldPosition(destinyFlatIndex, gridConfig) - GridSystem.FlatIndexToWorldPosition(originFlatIndex, gridConfig);

        return new float2 (cellFloat3.x, cellFloat3.z);
    }
    private void ProcessCurrentGridNeighbours(DynamicBuffer<CellComponents> cellComponents, int currentIndex, int2 targetCell, NativeQueue<int> nativeQueueCoords, GridConfig config)
    {
        int2[] surroundingCellsCoords = GetSurroundingCells(targetCell);

        foreach (int2 cell in surroundingCellsCoords)
        {
            if (!CheckIfIndexIsInBounds(cell, config)) continue;
            ref CellComponents cellElement = ref cellComponents.ElementAt(GridSystem.CoordsToIndex(cell.x, cell.y, config)); //grab references from coords
            if (cellElement.cost == GridSystem.WALL_COST || cellElement.bestCost != -1) continue;
            cellElement.bestCost = cellComponents[currentIndex].bestCost + cellElement.cost;
            nativeQueueCoords.Enqueue(GridSystem.CoordsToIndex(cell.x, cell.y, config)); //Sets the neighbours as new targets to continue processing, maybe could be done in the while to make it more clear
        }
    }

    private bool CheckIfIndexIsInBounds(int2 cell, GridConfig config)
    {
        if (cell.x < 0 || cell.x >= config.width || cell.y < 0 || cell.y >= config.height) return false;
        return true;
    }

    int2[] GetSurroundingCells(int2 cellCoords)
    {
        
        int2[] surroundingCoords = new int2[8];

        int write = 0;

        for (int i = 0; i < 9; i++)
        {
            int dx = (i % 3) - 1;
            int dy = (i / 3) - 1;
            if (dx == 0 && dy == 0) continue; //skip the target itself, that's why we also need a separated write value, cant use the iteration variable
            surroundingCoords[write++] = new(cellCoords.x + dx, cellCoords.y + dy); //starting with -1,-1
        }

        return surroundingCoords; //this is just a coords list
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        flowFieldPool.Dispose();
    }


}

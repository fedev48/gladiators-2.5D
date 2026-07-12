using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup))]
partial struct FlowfieldsSystem : ISystem
{
    uint wallLayerMask;
    NativeList<Entity> flowFieldPool;
    NativeHashMap<int, int> destinationToFieldId; //destination cell index -> flowFieldId, kept in sync with the pool
    int nextRecycleIndex;
    int fieldsCalculatedThisFrame;
    const int MAX_FLOW_FIELDS = 500;
    const int MAX_FRAME_CALCULATION_BUDGET = 5;

    struct RequestData
    {
        public Entity entity;
        public int destinationCellIndex;
    }

    public void OnCreate(ref SystemState state)
    {
        wallLayerMask = (uint)(1 << UnityEngine.LayerMask.NameToLayer("Walls"));
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridBlueprintTag>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<NeedsPathfinding>().Build());

        flowFieldPool = new NativeList<Entity>(MAX_FLOW_FIELDS, Allocator.Persistent);
        destinationToFieldId = new NativeHashMap<int, int>(MAX_FLOW_FIELDS, Allocator.Persistent);
        state.EntityManager.AddComponentData(state.SystemHandle, new FlowFieldPoolSingleton { Pool = flowFieldPool });
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {   
        
        CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        GridConfig gridConfigSingleton = SystemAPI.GetSingleton<GridConfig>();
        Entity blueprintEntity = SystemAPI.GetSingletonEntity<GridBlueprintTag>();
        flowFieldPool = SystemAPI.GetSingletonRW<FlowFieldPoolSingleton>().ValueRW.Pool; //RW registers the write dependency so jobs reading the pool complete before we mutate it

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
                state.EntityManager.SetComponentEnabled<NeedsPathfinding>(entity, false);
                continue; // no pathfinding needed because of line of sight o invalid target, at this point we need to disable the tag for the pathfinding following in case this is an override and the PF is no longer needed
            }

            int destinationCellIndex = GridSystem.WorldPosToIndex(toPoint, gridConfigSingleton);

            if (destinationToFieldId.TryGetValue(destinationCellIndex, out int existingFieldId)) //a previous FF has this target
            {
                usingPathfinding.ValueRW.flowFieldId = existingFieldId;
                state.EntityManager.SetComponentEnabled<UsingPathfinding>(entity, true);
                state.EntityManager.SetComponentEnabled<NeedsPathfinding>(entity, false);
                continue;
            }

            newFlowFieldRequests.Add(new RequestData { entity = entity, destinationCellIndex = destinationCellIndex });
        }

        ResolveNewFlowFieldRequests(ref state, blueprintEntity, newFlowFieldRequests, gridConfigSingleton);

        newFlowFieldRequests.Dispose();
    }

    void ResolveNewFlowFieldRequests(ref SystemState state, Entity blueprintEntity, NativeList<RequestData> newFlowFieldRequests, GridConfig gridConfigSingleton)
    {
        fieldsCalculatedThisFrame = 0;

        for (int i = 0; i < newFlowFieldRequests.Length; i++)
        {
            int destinationCellIndex = newFlowFieldRequests[i].destinationCellIndex;
            if (destinationToFieldId.ContainsKey(destinationCellIndex)||fieldsCalculatedThisFrame>=MAX_FRAME_CALCULATION_BUDGET) continue; // destination alaready processed or out of budget
            fieldsCalculatedThisFrame++;
            int assignedId;

            if (flowFieldPool.Length >= MAX_FLOW_FIELDS)
            {
                Entity recycledFlowField = flowFieldPool[nextRecycleIndex];
                nextRecycleIndex = (nextRecycleIndex + 1) % MAX_FLOW_FIELDS;
                FlowFieldMap recycledMap = state.EntityManager.GetComponentData<FlowFieldMap>(recycledFlowField);
                assignedId = recycledMap.FlowFieldId;
                destinationToFieldId.Remove(recycledMap.DestinationCellIndex); //the recycled field no longer serves its old destination
                state.EntityManager.SetComponentData(recycledFlowField, new FlowFieldMap { FlowFieldId = assignedId, DestinationCellIndex = destinationCellIndex });
                CalculateIntegrationField(ref state, recycledFlowField, gridConfigSingleton, true);
                CalculateVectorField(ref state, recycledFlowField, gridConfigSingleton);
            }
            else
            {
                assignedId = flowFieldPool.Length;
                Entity newFlowField = state.EntityManager.Instantiate(blueprintEntity);
                state.EntityManager.RemoveComponent<GridBlueprintTag>(newFlowField);
                state.EntityManager.SetComponentData(newFlowField, new FlowFieldMap { FlowFieldId = assignedId, DestinationCellIndex = destinationCellIndex });
                CalculateIntegrationField(ref state, newFlowField, gridConfigSingleton);
                CalculateVectorField(ref state, newFlowField, gridConfigSingleton);
                flowFieldPool.Add(newFlowField);
            }

            destinationToFieldId.Add(destinationCellIndex, assignedId);
        }


        for (int i = 0; i < newFlowFieldRequests.Length; i++)
        {
            Entity entity      = newFlowFieldRequests[i].entity;

            if (!destinationToFieldId.TryGetValue(newFlowFieldRequests[i].destinationCellIndex, out int flowFieldId)) continue;//in case this target wasn't processed in this budget cycle

            state.EntityManager.SetComponentData(entity, new UsingPathfinding { flowFieldId = flowFieldId });
            state.EntityManager.SetComponentEnabled<NeedsPathfinding>(entity, false);
            state.EntityManager.SetComponentEnabled<UsingPathfinding>(entity, true);
        }
    }

    void CalculateIntegrationField(ref SystemState state, Entity flowFieldParentEntity, GridConfig gridConfigSingleton, bool isRecycling = false)
    {
        FlowFieldMap flowFieldMap = SystemAPI.GetComponent<FlowFieldMap>(flowFieldParentEntity);
        DynamicBuffer<CellComponents> cellComponents = SystemAPI.GetBuffer<CellComponents>(flowFieldParentEntity);
        if (isRecycling)
        {
            for (int i = 0; i < cellComponents.Length; i++)
            {
                cellComponents.ElementAt(i).bestCost = -1;
                cellComponents.ElementAt(i).movingVector = float2.zero;
            }
        }
        int flatTargetIndex = flowFieldMap.DestinationCellIndex;
        ref CellComponents cell = ref cellComponents.ElementAt(flatTargetIndex);
        cell.bestCost = 0;
        NativeQueue<int> nativeQueueCoords = new(Allocator.Temp);
        nativeQueueCoords.Enqueue(flatTargetIndex);

        int securityCount = 0;

        while (nativeQueueCoords.Count!=0 && securityCount<cellComponents.Length*8)
        {
            int nextIndex = nativeQueueCoords.Dequeue();
            ProcessCurrentGridNeighbours(cellComponents, nextIndex, GridSystem.IndexToCoords(nextIndex, gridConfigSingleton), nativeQueueCoords, gridConfigSingleton);
            securityCount++;
        }
        nativeQueueCoords.Dispose();
    }

    void CalculateVectorField(ref SystemState state, Entity flowFieldParentEntity ,GridConfig gridConfigSingleton)
    {
        DynamicBuffer<CellComponents> cellComponents = SystemAPI.GetBuffer<CellComponents>(flowFieldParentEntity);

        for (int i = 0; i < cellComponents.Length; i++)
        {
            int2 centralCoords = GridSystem.IndexToCoords(i, gridConfigSingleton);
            FixedList128Bytes<int2> surroundingCellsCoords = GridSystem.GetSurroundingCells(centralCoords);
            int lowestCost = 10000;
            int lowestCostCell = -1;

            for (int j = 0; j < surroundingCellsCoords.Length; j++)
            {
                if (!GridSystem.CheckIfCoordsIsInBounds(surroundingCellsCoords[j], gridConfigSingleton)) continue;

                if (DiagonalCrossesWallCorner(cellComponents, centralCoords, surroundingCellsCoords[j], gridConfigSingleton)) continue;

                int flatTargetIndex = GridSystem.CoordsToIndex(surroundingCellsCoords[j].x, surroundingCellsCoords[j].y, gridConfigSingleton);
                ref CellComponents cell = ref cellComponents.ElementAt(flatTargetIndex);
                if (cell.bestCost >= 0 && cell.bestCost<=lowestCost)
                {
                    lowestCost = cell.bestCost;
                    lowestCostCell = flatTargetIndex;
                }
            }

            ref CellComponents targetCell = ref cellComponents.ElementAt(i);

            if (lowestCostCell != -1 && targetCell.bestCost != 0)
            {
                targetCell.movingVector = CalculateMoveVectorForField(i, lowestCostCell, gridConfigSingleton);
            }

            
        }
    }

    
    float2 CalculateMoveVectorForField(int originFlatIndex, int destinyFlatIndex, GridConfig gridConfig)
    {
        float3 cellFloat3 = GridSystem.FlatIndexToWorldPosition(destinyFlatIndex, gridConfig) - GridSystem.FlatIndexToWorldPosition(originFlatIndex, gridConfig);

        return math.normalize(new float2 (cellFloat3.x, cellFloat3.z));
    }
    private void ProcessCurrentGridNeighbours(DynamicBuffer<CellComponents> currentFlowfieldList, int centralFlatIndex, int2 centralCellCoords, NativeQueue<int> nativeQueueCoords, GridConfig config)
    {
        
        FixedList128Bytes<int2> surroundingCellsCoords = GridSystem.GetSurroundingCells(centralCellCoords);

        for (int i = 0; i < surroundingCellsCoords.Length; i++)
        {
            int2 currentNeighbourCoords = surroundingCellsCoords[i];
            if (!GridSystem.CheckIfCoordsIsInBounds(currentNeighbourCoords, config)) continue;

            int flatIndexCurrentNeighbour = GridSystem.CoordsToIndex(currentNeighbourCoords.x, currentNeighbourCoords.y, config);
            ref CellComponents currentNeighbourEntity = ref currentFlowfieldList.ElementAt(flatIndexCurrentNeighbour);
            if (currentNeighbourEntity.cost == GridSystem.WALL_COST) continue;

            bool isDiagonal = i < 4 ? (i % 2 == 0) : (i % 2 != 0);

            if (DiagonalCrossesWallCorner(currentFlowfieldList, centralCellCoords, currentNeighbourCoords, config)) continue;

            int candidateCost = currentFlowfieldList[centralFlatIndex].bestCost + currentNeighbourEntity.cost * (isDiagonal ? 14 : 10);

            if (currentNeighbourEntity.bestCost != -1 && currentNeighbourEntity.bestCost <= candidateCost) continue; //the allows to add again cells that were calculated in a more expensive path

            currentNeighbourEntity.bestCost = candidateCost;
            nativeQueueCoords.Enqueue(flatIndexCurrentNeighbour);
        }
    }

  
    static bool DiagonalCrossesWallCorner(DynamicBuffer<CellComponents> cells, int2 centralCoords, int2 neighbourCoords, GridConfig config)
    {
        int dx = neighbourCoords.x - centralCoords.x;
        int dy = neighbourCoords.y - centralCoords.y;
        if (dx == 0 || dy == 0) return false; //not a diagonal

        int sideA = GridSystem.CoordsToIndex(centralCoords.x + dx, centralCoords.y, config);
        int sideB = GridSystem.CoordsToIndex(centralCoords.x, centralCoords.y + dy, config);
        return cells[sideA].cost == GridSystem.WALL_COST ||
               cells[sideB].cost == GridSystem.WALL_COST;
    }

    

    public void OnDestroy(ref SystemState state)
    {
        flowFieldPool.Dispose();
        destinationToFieldId.Dispose();
    }


}

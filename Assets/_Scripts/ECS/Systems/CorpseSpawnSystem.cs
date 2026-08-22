using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct CorpseSpawnSystem : ISystem
{
    ComponentLookup<AnimRequest> animRequestLookup;
    Random random;

    public void OnCreate(ref SystemState state)
    {
        animRequestLookup = state.GetComponentLookup<AnimRequest>(isReadOnly: false);
        random = Random.CreateFromIndex(5678);
        state.RequireForUpdate<EntitiesReferences>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        animRequestLookup.Update(ref state);

        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        float deltaTime = SystemAPI.Time.DeltaTime;
        float time      = (float)SystemAPI.Time.ElapsedTime;

        ProcessSpawnRequests(ref state, ref ecb);
        ProcessDespawnRequests(ref state, ref ecb);
        UpdateEmerging(ref state, ref ecb, deltaTime, time);
        UpdateSinking(ref state, ref ecb, deltaTime);
    }

    void ProcessSpawnRequests(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        Entity corpsePrefab = SystemAPI.GetSingleton<EntitiesReferences>().corpsPrefabEntity;
        if (corpsePrefab == Entity.Null) return;

        CorpseConfig config = SystemAPI.GetComponent<CorpseConfig>(corpsePrefab);
        float prefabScale   = SystemAPI.GetComponent<LocalTransform>(corpsePrefab).Scale;

        foreach ((RefRO<CorpseSpawRequest> request, Entity requestEntity) in
                 SystemAPI.Query<RefRO<CorpseSpawRequest>>().WithEntityAccess())
        {
            float groundY = request.ValueRO.position.y;

            //resting: bottom of the bounds on the ground. buried: top of the bounds at ground level
            float3 surfacePos = new float3(request.ValueRO.position.x, groundY - config.boundsMinY, request.ValueRO.position.z);
            float  height     = config.boundsMaxY - config.boundsMinY;
            float3 startPos   = new float3(surfacePos.x, groundY - config.boundsMaxY, surfacePos.z);

            float tiltX = math.radians(random.NextFloat(-config.maxTiltAngleX, config.maxTiltAngleX));
            float tiltZ = math.radians(random.NextFloat(-config.maxTiltAngleZ, config.maxTiltAngleZ));
            quaternion rotation = math.mul(quaternion.RotateZ(tiltZ), quaternion.RotateX(tiltX));

            Entity corpse = ecb.Instantiate(corpsePrefab);
            ecb.SetComponent(corpse, LocalTransform.FromPositionRotationScale(startPos, rotation, prefabScale));
            ecb.SetComponent(corpse, new CorpseSpawnData { surfacePos = surfacePos, height = height });
            ecb.SetComponentEnabled<CorpseEmerging>(corpse, true);

            ecb.DestroyEntity(requestEntity);
        }
    }

    void ProcessDespawnRequests(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        foreach ((RefRO<CorpseDespawRequest> request, Entity requestEntity) in
                 SystemAPI.Query<RefRO<CorpseDespawRequest>>().WithEntityAccess())
        {
            Entity corpse = request.ValueRO.corpseEntity;
            ecb.DestroyEntity(requestEntity);

            if (!SystemAPI.HasComponent<CorpseTag>(corpse)) continue;

            ecb.SetComponentEnabled<CorpseTag>(corpse, false);
            ecb.SetComponentEnabled<CorpseEmerging>(corpse, false);
            ecb.SetComponentEnabled<CorpseSinking>(corpse, true);
        }
    }

    void UpdateEmerging(ref SystemState state, ref EntityCommandBuffer ecb, float deltaTime, float time)
    {
        foreach ((RefRW<LocalTransform> transform,
                  RefRO<CorpseSpawnData> spawnData,
                  RefRO<CorpseConfig> config,
                  Entity entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<CorpseSpawnData>, RefRO<CorpseConfig>>()
                     .WithAll<CorpseEmerging>()
                     .WithEntityAccess())
        {
            RequestAnimation(ref state, entity);

            float targetY = spawnData.ValueRO.surfacePos.y;
            float newY    = math.min(transform.ValueRO.Position.y + spawnData.ValueRO.height * config.ValueRO.emergeSpeed * deltaTime, targetY);
            float shake   = math.sin(time * 40f) * 0.04f;

            transform.ValueRW.Position = new float3(
                spawnData.ValueRO.surfacePos.x + shake,
                newY,
                spawnData.ValueRO.surfacePos.z
            );

            if (newY < targetY) continue;

            transform.ValueRW.Position = spawnData.ValueRO.surfacePos;
            ecb.SetComponentEnabled<CorpseEmerging>(entity, false);
            ecb.SetComponentEnabled<CorpseTag>(entity, true);
        }
    }

    void UpdateSinking(ref SystemState state, ref EntityCommandBuffer ecb, float deltaTime)
    {
        foreach ((RefRW<LocalTransform> transform,
                  RefRO<CorpseSpawnData> spawnData,
                  RefRO<CorpseConfig> config,
                  Entity entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRO<CorpseSpawnData>, RefRO<CorpseConfig>>()
                     .WithAll<CorpseSinking>()
                     .WithEntityAccess())
        {
            RequestAnimation(ref state, entity);

            float targetY = spawnData.ValueRO.surfacePos.y - spawnData.ValueRO.height;
            float newY    = math.max(transform.ValueRO.Position.y - spawnData.ValueRO.height * config.ValueRO.emergeSpeed * deltaTime, targetY);

            transform.ValueRW.Position = new float3(transform.ValueRO.Position.x, newY, transform.ValueRO.Position.z);

            if (newY > targetY) continue;

            ecb.DestroyEntity(entity);
        }
    }

    void RequestAnimation(ref SystemState state, Entity corpse)
    {
        if (!SystemAPI.HasComponent<VisualEntity>(corpse)) return;

        Entity visualEntity = SystemAPI.GetComponent<VisualEntity>(corpse).value;
        if (!animRequestLookup.HasComponent(visualEntity)) return;

        animRequestLookup.GetRefRW(visualEntity).ValueRW.role = Animation.Emerge;
    }
}

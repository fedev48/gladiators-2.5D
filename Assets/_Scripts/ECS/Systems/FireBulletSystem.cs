using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(InputReaderSystem))]
[BurstCompile]
public partial struct FireBulletSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EntitiesReferences>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntitiesReferences refs = SystemAPI.GetSingleton<EntitiesReferences>();

        EntityCommandBuffer ecb = SystemAPI
            .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRO<LocalTransform> transform,
                  RefRO<FireBulletEvent> fireBulletEvent,
                  RefRO<BulletSpellConfig> spellConfig,
                  Entity entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRO<FireBulletEvent>, RefRO<BulletSpellConfig>>()
                .WithEntityAccess())
        {
            Entity bulletPrefab = SystemAPI.HasComponent<PlayerTag>(entity)
                ? refs.bulletPrefabEntity
                : refs.bulletPrefabEnemyEntity;

            BulletConfig prefabConfig = SystemAPI.GetComponent<BulletConfig>(bulletPrefab);

            float3 direction = fireBulletEvent.ValueRO.direction;
            float3 aim       = math.normalizesafe(new float3(direction.x, 0f, direction.z));
            float  angle     = math.radians(spellConfig.ValueRO.fireAngle);

            prefabConfig.velocity = (aim * math.cos(angle) + math.up() * math.sin(angle)) * prefabConfig.speed;
            prefabConfig.owner    = entity;

            float3 spawnPos = transform.ValueRO.Position + new float3(0f, 1f, 0f);

            Entity orb = ecb.Instantiate(bulletPrefab);
            ecb.SetComponent(orb, LocalTransform.FromPosition(spawnPos));
            ecb.SetComponent(orb, prefabConfig);

            SystemAPI.SetComponentEnabled<FireBulletEvent>(entity, false);
        }
    }
}

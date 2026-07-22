using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    [SerializeField] float playerSpeed = 5f;
    [SerializeField] float skeletonSpawnMinRadius = 3f;
    [SerializeField] float skeletonSpawnMaxRadius = 8f;
    [SerializeField] int skeletonSpawnCount = 3;
    [SerializeField] float skeletonSpawnInterval = 0.3f;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerTag());
            AddComponent(entity, new UnitTag());
            AddComponent(entity, new MoveSpeed     { Value = authoring.playerSpeed });
            AddComponent(entity, new MoveDirection {});
            AddComponent(entity, new UnitMovementAnimTag());
            Entity visualEntity = GetEntity(authoring.GetComponentInChildren<SpriteAnimatorAuthoring>(), TransformUsageFlags.Dynamic);
            AddComponent(entity, new VisualEntity { Value = visualEntity });
            AddComponent(entity, new ShouldSnapToFloorTag());
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
            AddComponent(entity, new SummonSkeletonEvent());
            AddComponent(entity, new BulletSpellConfig());
            AddComponent(entity, new FireBulletEvent());
            SetComponentEnabled<FireBulletEvent>(entity, false);
            AddComponent(entity, new SkeletonSpellConfig
            {
                minRadius    = authoring.skeletonSpawnMinRadius,
                maxRadius    = authoring.skeletonSpawnMaxRadius,
                spawnCount   = authoring.skeletonSpawnCount,
                interval     = authoring.skeletonSpawnInterval
            });
            SetComponentEnabled<SummonSkeletonEvent>(entity, false);
        }
    }
}

public struct SkeletonSpellConfig : IComponentData
{
    public float minRadius;
    public float maxRadius;
    public int   spawnCount;
    public float interval;
}

public struct PlayerTag           : IComponentData {}
public struct ShouldSnapToFloorTag: IComponentData, IEnableableComponent {}
public struct SummonSkeletonEvent : IComponentData, IEnableableComponent { public int count; }
public struct BulletSpellConfig   : IComponentData {}
public struct FireBulletEvent     : IComponentData, IEnableableComponent { public float3 direction; }

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct PlayerFreezeRotationBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var mass in SystemAPI.Query<RefRW<PhysicsMass>>().WithAll<PlayerTag>())
            mass.ValueRW.InverseInertia = float3.zero;
    }
}

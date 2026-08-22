using System;
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
    [SerializeField, Range(0f, 89f)] float bulletFireAngle = 0f;
    [SerializeField] float knockbackMultiplier = 1f;
    [SerializeField] float knockbackDurationMultiplier = 1f;
    [SerializeField] int health;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlayerTag());
            AddComponent(entity, new UnitTag());
            AddComponent(entity, new Team { value = Teams.ALLY });
            AddComponent(entity, new UnitMovementAnimTag());
            Entity visualEntity = GetEntity(authoring.GetComponentInChildren<SpriteAnimatorAuthoring>(), TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new AffectedByGrativy());
            
            AddComponent(entity, new SummonSkeletonEvent());
            AddComponent(entity, new BulletSpellConfig { fireAngle = authoring.bulletFireAngle });
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
            AddComponent(entity, new VisualEntity           { value = visualEntity });
            AddComponent(entity, new PhysicsGravityFactor   { Value = 0f });
            AddComponent(entity, new MoveSpeed              { value = authoring.playerSpeed });
            AddComponent(entity, new Health                 { value = authoring.health });
            AddComponent(entity, new KnockbackVelocity
            {
                multiplier      = authoring.knockbackMultiplier,
                durationMultiplier = authoring.knockbackDurationMultiplier
            });
            
            //Movement components
            AddComponent(entity, new CurrentVelocity {});

            AddComponent(entity, new DesiredVelocity {});
            AddComponent(entity, new SeparationVelocity {});
            AddComponent(entity, new MovementBlocked());
            AddComponent(entity, new DeathState {});
            SetComponentEnabled<MovementBlocked>(entity, false);
            SetComponentEnabled<DeathState>(entity, false);
            AddComponent(entity, new RecievingDamage());
            SetComponentEnabled<RecievingDamage>(entity, false);

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
public struct BulletSpellConfig   : IComponentData { public float fireAngle; }
public struct SummonSkeletonEvent : IComponentData, IEnableableComponent { public int count; }
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

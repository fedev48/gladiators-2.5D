using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class EnemyAuthoring : MonoBehaviour
{
    public float acceleration = 4f;
    public float maxSpeed     = 3f;
    public float separationRadius = 1f;
    public float separationSpeed  = 15f;
    public float knockbackMultiplier = 1f;
    public float knockbackDurationMultiplier = 1f;
    public int   health = 10;

    [Header("Targeting")]
    public int   targetSearchCellRadius    = 5;
    public float targetScanInterval        = 0.5f;
    public float targetRetentionMultiplier = 1.3f;
    public float targetSwitchImprovement   = 0.25f;
    public float attackerLockDuration      = 1.5f;


    public class Baker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Team { value = Teams.ENEMY });
            AddComponent(entity, new UnitTag());
            AddComponent(entity, new MovementBlocked());
            SetComponentEnabled<MovementBlocked>(entity, false);
            AddComponent(entity, new Health { value = authoring.health });
            AddComponent(entity, new RecievingDamage());
            SetComponentEnabled<RecievingDamage>(entity, false);
            AddComponent(entity, new CurrentVelocity {});
            AddComponent(entity, new DesiredVelocity {});
            AddComponent(entity, new KnockbackVelocity
            {
                multiplier      = authoring.knockbackMultiplier,
                durationMultiplier = authoring.knockbackDurationMultiplier
            });
            AddComponent(entity, new MoveSpeed {});
            AddComponent(entity, new MovementConfig
            {
                acceleration = authoring.acceleration,
                maxSpeed     = authoring.maxSpeed
            });
            AddComponent(entity, new MoveDestination());
            SetComponentEnabled<MoveDestination>(entity, false);

            AddComponent(entity, new ShouldSnapToFloorTag());
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });

            //animation system
            Entity visualEntity = GetEntity(authoring.GetComponentInChildren<SpriteAnimatorAuthoring>(), TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMovementAnimTag());
            AddComponent(entity, new VisualEntity { value = visualEntity });

            //pathfinding tags
            AddComponent(entity, new NeedsPathfinding());
            AddComponent(entity, new UsingPathfinding());
            AddComponent(entity, new SeparationConfig
            {
                radius = authoring.separationRadius,
                strenght  = authoring.separationSpeed
            });
            AddComponent(entity, new SeparationVelocity());
            SetComponentEnabled<NeedsPathfinding>(entity, false);
            SetComponentEnabled<UsingPathfinding> (entity, false);

            AddComponent(entity, new FSMBlackBoard());
            //targeting
            AddComponent(entity, new TargetingConfig
            {
                searchCellRadius     = authoring.targetSearchCellRadius,
                scanInterval         = authoring.targetScanInterval,
                retentionMultiplier  = authoring.targetRetentionMultiplier,
                switchImprovement    = authoring.targetSwitchImprovement,
                attackerLockDuration = authoring.attackerLockDuration
            });
            AddComponent(entity, new TargetingState { scanCooldown = -1f });
            AddComponent(entity, new HasTarget());
            SetComponentEnabled<HasTarget>(entity, false);
            AddComponent(entity, new LastAttacker());
            SetComponentEnabled<LastAttacker>(entity, false);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct UnitFreezeRotationBakingSystem : ISystem
{
    public readonly void OnUpdate(ref SystemState state)
    {
        foreach (RefRW<PhysicsMass> mass in SystemAPI.Query<RefRW<PhysicsMass>>().WithAll<UnitTag>())
            mass.ValueRW.InverseInertia = float3.zero;
    }
}

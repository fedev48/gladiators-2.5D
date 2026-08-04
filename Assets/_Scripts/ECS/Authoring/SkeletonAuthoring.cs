using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class SkeletonAuthoring : MonoBehaviour
{
    public float accelerationMin = 2f;
    public float accelerationMax = 8f;
    public float maxSpeed        = 4f;
    public float separationRadius = 2f;
    public float separationStrenght  = 8f;
    public float knockbackMultiplier = 1f;
    public float knockbackDurationMultiplier = 1f;
    public int   health = 10;

    [Header("Targeting")]
    public int   targetSearchCellRadius    = 5;
    public float targetScanInterval        = 0.5f;
    public float targetRetentionMultiplier = 1.3f;
    public float targetSwitchImprovement   = 0.25f;
    public float attackerLockDuration      = 1.5f;


    public class Baker : Baker<SkeletonAuthoring>
    {
        public override void Bake(SkeletonAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Team { value = Teams.ALLY });
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
            AddComponent(entity, new MovementConfig { maxSpeed = authoring.maxSpeed });
            AddComponent(entity, new MoveDestination());
            SetComponentEnabled<MoveDestination>(entity, false);

            AddComponent(entity, new ShouldSnapToFloorTag());
            SetComponentEnabled<ShouldSnapToFloorTag>(entity, false);

            AddComponent(entity, new SkeletonConfig
            {
                accelerationMin = authoring.accelerationMin,
                accelerationMax = authoring.accelerationMax
            });
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
                strenght  = authoring.separationStrenght
            });
            AddComponent(entity, new SeparationVelocity());
            SetComponentEnabled<NeedsPathfinding>(entity, false);
            SetComponentEnabled<UsingPathfinding> (entity, false);

            //state machine tags
            AddComponent(entity, new SpawnState());
            AddComponent(entity, new FollowState());
            AddComponent(entity, new MeleeAttackState());
            SetComponentEnabled<FollowState> (entity, false);
            SetComponentEnabled<MeleeAttackState> (entity, false);
            SetComponentEnabled<SpawnState>  (entity, true);

            AddComponent(entity, new FSMState { current = TypeManager.GetTypeIndex<SpawnState>(), stateDuration = -1 });
            AddComponent(entity, new FSMBlackBoard());
            AddBuffer<ChangeStateRequest>(entity);
            SetComponentEnabled<ChangeStateRequest>(entity, false);

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

public struct SkeletonConfig : IComponentData
{
    public float accelerationMin;
    public float accelerationMax;
}

public struct SkeletonSpawnData : IComponentData
{
    public float  height;
    public float3 surfacePos;
    public float3 followOffset;
}

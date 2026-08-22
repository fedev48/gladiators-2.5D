using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class SkeletonAuthoring : MonoBehaviour
{
    [Header("Unit Config")]
    public float accelerationMin = 2f;
    public float accelerationMax = 8f;
    public float maxSpeed        = 4f;
    public float separationRadius = 2f;
    public float separationStrenght  = 8f;
    public float knockbackMultiplier = 1f;
    public float knockbackDurationMultiplier = 1f;
    public int   health = 10;

    [Header("BlackBoard Filling")]
    public int   targetSearchRadius                 = 5;
    public int   targetSearchRadiuosForSurrounded   = 5;
    public float targetScanInterval                 = 0.5f;
    public float targetRetentionMultiplier          = 1.3f;
    public float targetSwitchImprovement            = 0.25f;
    public float attackerLockDuration               = 1.5f;
    public float attackerDamageThreshold            = 5f;
    public float attackerDamageDecay                = 2f;

    [Header("Melee Attack")]
    public float attackRange       = 1.5f;
    public float attackHitRadius   = 1f;
    public float attackDamage      = 3f;
    public float attackKnockback   = 20f;
    public float attackRecovery    = 0.5f;



    public class Baker : Baker<SkeletonAuthoring>
    {
        const float STOP_DISTANCE_FACTOR = 0.8f; 
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

            AddComponent(entity, new AffectedByGrativy());
            SetComponentEnabled<AffectedByGrativy>(entity, false);

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
                radius    = authoring.separationRadius,
                strenght  = authoring.separationStrenght
            });
            AddComponent(entity, new SeparationVelocity());
            SetComponentEnabled<NeedsPathfinding>(entity, false);
            SetComponentEnabled<UsingPathfinding> (entity, false);

            //state machine tags
            AddComponent(entity, new MeleeAttackState
            {
                attackRange         = authoring.attackRange,
                stopDistance        = authoring.attackRange * STOP_DISTANCE_FACTOR,
                hitRadius           = authoring.attackHitRadius,
                damage              = authoring.attackDamage,
                knockbackStrength   = authoring.attackKnockback,
                recovery            = authoring.attackRecovery,
            });
            
            AddComponent(entity, new SpawnState());
            AddComponent(entity, new FollowState());
            AddComponent(entity, new DeathState
            {
                elapsed = -1
            });
            SetComponentEnabled<FollowState> (entity, false);
            SetComponentEnabled<MeleeAttackState> (entity, false);
            SetComponentEnabled<DeathState> (entity, false);
            SetComponentEnabled<SpawnState>  (entity, true);

            AddComponent(entity, new FSMState { current = TypeManager.GetTypeIndex<SpawnState>(), stateDuration = -1 });
            AddComponent(entity, new FSMBlackBoard());
            AddBuffer<ChangeStateRequest>(entity);
            SetComponentEnabled<ChangeStateRequest>(entity, false);

            //blackboard
            AddComponent(entity, new BlackboardSensorConfigAndState
            {
                
                clock                            = -1f,
                searchRadiusForTarget            = authoring.targetSearchRadius,
                searchRadiusForSurrouded         = authoring.targetSearchRadiuosForSurrounded,
                scanInterval                     = authoring.targetScanInterval,
                distanceTargetReleaseMultiplier  = authoring.targetRetentionMultiplier,
                distanceDifferenceToSwitchTarget = authoring.targetSwitchImprovement,
                retaliationDamageThreshold       = authoring.attackerDamageThreshold,
                retaliationDamageDecay           = authoring.attackerDamageDecay

            });

            AddComponent(entity, new HasTarget());
            SetComponentEnabled<HasTarget>(entity, false);
            AddComponent(entity, new LastAttacker());
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

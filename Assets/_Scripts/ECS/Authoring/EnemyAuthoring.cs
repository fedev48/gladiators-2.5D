using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class EnemyAuthoring : MonoBehaviour
{
    [Header("Unit Config")]
    public float acceleration = 4f;
    public float maxSpeed     = 3f;
    public float separationRadius = 1f;
    public float separationSpeed  = 15f;
    public float knockbackMultiplier = 1f;
    public float knockbackDurationMultiplier = 1f;
    public int   health = 10;
    public bool leavesCorpseInCell = false;

    [Header("BlackboardFilling")]
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

    [Header("Range Attack")]
    public float bulletFireAngle       = 45f;
    public float rangeAttackRecovery   = 5f;
    public float rangeDistanceTolerance = 1f;
    public float straightShotRange      = 8f;


    public class Baker : Baker<EnemyAuthoring>
    {
        const float STOP_DISTANCE_FACTOR = 0.8f;

        public override void Bake(EnemyAuthoring authoring)
        {
            //unit config
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new Team { value = Teams.ENEMY });
            AddComponent(entity, new UnitTag());
            AddComponent(entity, new LeavesCorpseInCellTag {});
            SetComponentEnabled<LeavesCorpseInCellTag> (entity, authoring.leavesCorpseInCell);
            AddComponent(entity, new KnockbackVelocity
            {
                multiplier      = authoring.knockbackMultiplier,
                durationMultiplier = authoring.knockbackDurationMultiplier
            });
            AddComponent(entity, new Health { value = authoring.health });
            AddComponent(entity, new MovementConfig
            {
                acceleration = authoring.acceleration,
                maxSpeed     = authoring.maxSpeed
            });
            AddComponent(entity, new AffectedByGrativy());
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
            //state
            AddComponent(entity, new MovementBlocked());
            SetComponentEnabled<MovementBlocked>(entity, false);
            AddComponent(entity, new RecievingDamage());
            SetComponentEnabled<RecievingDamage>(entity, false);
            AddComponent(entity, new CurrentVelocity {});
            AddComponent(entity, new DesiredVelocity {});
            AddComponent(entity, new MoveSpeed {});
           
            AddComponent(entity, new MoveDestination());
            SetComponentEnabled<MoveDestination>(entity, false);


            //animation system
            Entity visualEntity = GetEntity(authoring.GetComponentInChildren<SpriteAnimatorAuthoring>(), TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMovementAnimTag());
            AddComponent(entity, new VisualEntity { value = visualEntity });

            //pathfinding tags
            AddComponent(entity, new NeedsPathfinding());
            AddComponent(entity, new UsingPathfinding());
            AddComponent(entity, new SeparationConfig
            {
                radius  = authoring.separationRadius,
                strenght  = authoring.separationSpeed
            });
            AddComponent(entity, new SeparationVelocity());
            SetComponentEnabled<NeedsPathfinding>(entity, false);
            SetComponentEnabled<UsingPathfinding> (entity, false);

            //FSM
            AddComponent(entity, new MeleeAttackState
            {
                attackRange       = authoring.attackRange,
                stopDistance      = authoring.attackRange * STOP_DISTANCE_FACTOR,
                hitRadius         = authoring.attackHitRadius,
                damage            = authoring.attackDamage,
                knockbackStrength = authoring.attackKnockback,
                recovery          = authoring.attackRecovery,
            });
            AddComponent(entity, new RangeAttack
            {
                distanceTolerance = authoring.rangeDistanceTolerance,
                straightShotRange = authoring.straightShotRange,
                recovery          = authoring.rangeAttackRecovery,
            });
            AddComponent(entity, new BulletSpellConfig { fireAngle = authoring.bulletFireAngle });
            AddComponent(entity, new FireBulletEvent());
            SetComponentEnabled<FireBulletEvent>(entity, false);

            AddComponent(entity, new WanderState());
            AddComponent(entity, new DeathState
            {
                elapsed = -1
            });
            SetComponentEnabled<MeleeAttackState>(entity, false);
            SetComponentEnabled<RangeAttack>     (entity, false);
            SetComponentEnabled<DeathState>       (entity, false);
            SetComponentEnabled<WanderState>     (entity, true);

            AddComponent(entity, new FSMState { current = TypeManager.GetTypeIndex<WanderState>(), stateDuration = -1 });
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
            AddComponent(entity, new LastAttacker());
            SetComponentEnabled<HasTarget>   (entity, false);
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

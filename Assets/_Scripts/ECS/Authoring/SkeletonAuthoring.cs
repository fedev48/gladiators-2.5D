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


    public class Baker : Baker<SkeletonAuthoring>
    {
        public override void Bake(SkeletonAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SkeletonTag());
            AddComponent(entity, new UnitTag());
            AddComponent(entity, new MoveDirection {});
            AddComponent(entity, new MoveSpeed {});
            
            AddComponent(entity, new ShouldSnapToFloorTag());
            SetComponentEnabled<ShouldSnapToFloorTag>(entity, false);
            
            AddComponent(entity, new SkeletonConfig
            {
                accelerationMin = authoring.accelerationMin,
                accelerationMax = authoring.accelerationMax,
                maxSpeed        = authoring.maxSpeed
            });
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });

            //animation system 
            Entity visualEntity = GetEntity(authoring.GetComponentInChildren<SpriteAnimatorAuthoring>(), TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMovementAnimTag());
            AddComponent(entity, new VisualEntity { Value = visualEntity });

            //pathfinding tags
            AddComponent(entity, new NeedsPathfinding());
            AddComponent(entity, new UsingPathfinding());
            AddComponent(entity, new UnitRadius{ Value = authoring.separationRadius});
            AddComponent(entity, new SeparationVector());
            SetComponentEnabled<NeedsPathfinding>(entity, false);
            SetComponentEnabled<UsingPathfinding> (entity, false);

            //state machine tags
            AddComponent(entity, new SpawnState());
            AddComponent(entity, new FollowState());
            AddComponent(entity, new AttackState());
            SetComponentEnabled<FollowState> (entity, false);
            SetComponentEnabled<AttackState> (entity, false);
            SetComponentEnabled<SpawnState>  (entity, true);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct SkeletonFreezeRotationBakingSystem : ISystem
{
    public readonly void OnUpdate(ref SystemState state)
    {
        foreach (var mass in SystemAPI.Query<RefRW<PhysicsMass>>().WithAll<SkeletonTag>())
            mass.ValueRW.InverseInertia = float3.zero;
    }
}

public struct SkeletonConfig : IComponentData
{
    public float accelerationMin;
    public float accelerationMax;
    public float maxSpeed;
    public float acceleration;
}

public struct SkeletonTag : IComponentData {}

public struct SkeletonSpawnData : IComponentData
{
    public float  height;
    public float3 surfacePos;
    public float3 followOffset;
}

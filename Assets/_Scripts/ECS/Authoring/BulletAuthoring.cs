using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class BulletAuthoring : MonoBehaviour
{
    [SerializeField] float speed = 12f;
    [SerializeField] float lifetime = 10f;
    [SerializeField, Range(0f, 10f)] float gravityScale = 1f;

    public class Baker : Baker<BulletAuthoring>
    {
        public override void Bake(BulletAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new BulletConfig
            {
                speed        = authoring.speed,
                lifetime     = authoring.lifetime,
                gravityScale = authoring.gravityScale
            });
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
            AddComponent(entity, new BulletDestroyTag());
            SetComponentEnabled<BulletDestroyTag>(entity, false);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct BulletCollisionResponseBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var collider in SystemAPI.Query<RefRW<PhysicsCollider>>().WithAll<BulletConfig>())
        {
            collider.ValueRW.Value.Value.SetCollisionResponse(CollisionResponsePolicy.RaiseTriggerEvents);
        }
    }
}

public struct BulletDestroyTag : IComponentData, IEnableableComponent {}

public struct BulletConfig : IComponentData
{
    public float  speed;
    public float  lifetime;
    public float  gravityScale;
    public float3 velocity;
    public Entity owner;
}

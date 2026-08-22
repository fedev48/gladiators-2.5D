using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CorpseAuthoring : MonoBehaviour
{
    [SerializeField] float maxTiltAngleX;
    [SerializeField] float maxTiltAngleZ;
    [SerializeField] float emergeSpeed = 1f;

    public class Baker : Baker<CorpseAuthoring>
    {
        public override void Bake(CorpseAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new CorpseTag());
            SetComponentEnabled<CorpseTag>(entity, false);

            AddComponent(entity, new CorpseSpawnData());

            GetVerticalBounds(authoring, out float boundsMinY, out float boundsMaxY);

            AddComponent(entity, new CorpseConfig
            {
                maxTiltAngleX = authoring.maxTiltAngleX,
                maxTiltAngleZ = authoring.maxTiltAngleZ,
                boundsMinY    = boundsMinY,
                boundsMaxY    = boundsMaxY,
                emergeSpeed   = authoring.emergeSpeed
            });

            AddComponent(entity, new CorpseEmerging());
            SetComponentEnabled<CorpseEmerging>(entity, false);

            AddComponent(entity, new CorpseSinking());
            SetComponentEnabled<CorpseSinking>(entity, false);

            SpriteAnimatorAuthoring spriteAnimator = authoring.GetComponentInChildren<SpriteAnimatorAuthoring>();
            if (spriteAnimator != null) AddComponent(entity, new VisualEntity { value = GetEntity(spriteAnimator, TransformUsageFlags.Dynamic) });
        }

        //vertical extents relative to the pivot, so the system can align the top or the bottom with the ground
        static void GetVerticalBounds(CorpseAuthoring authoring, out float minY, out float maxY)
        {
            Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                minY = 0f;
                maxY = 1f;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float pivotY = authoring.transform.position.y;
            minY = bounds.min.y - pivotY;
            maxY = bounds.max.y - pivotY;
        }
    }
}

public struct CorpseTag : IComponentData, IEnableableComponent {}
public struct CorpseSpawnData : IComponentData { public float3 surfacePos; public float height; }

public struct CorpseConfig : IComponentData
{
    public float maxTiltAngleX;
    public float maxTiltAngleZ;
    public float boundsMinY;
    public float boundsMaxY;
    public float emergeSpeed;
}

public struct CorpseEmerging : IComponentData, IEnableableComponent {}
public struct CorpseSinking  : IComponentData, IEnableableComponent {}

public struct CorpseSpawRequest : IComponentData
{
    public float3 position;
}


public struct CorpseDespawRequest : IComponentData
{
    public Entity corpseEntity;
}


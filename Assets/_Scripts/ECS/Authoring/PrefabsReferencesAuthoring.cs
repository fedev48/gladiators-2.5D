using Unity.Entities;
using UnityEngine;

public class PrefabsReferencesAuthoring : MonoBehaviour
{
    public GameObject skeletonPrefabGameObject;
    public GameObject bulletPrefabGameObject;
    public GameObject bulletPrefabEnemyGameObject;
    public GameObject corpsPrefabGameObject;

    public class Baker : Baker<PrefabsReferencesAuthoring>
    {
        public override void Bake(PrefabsReferencesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntitiesReferences
            {
                skeletonPrefabEntity = GetEntity(authoring.skeletonPrefabGameObject, TransformUsageFlags.Dynamic),
                bulletPrefabEntity = GetEntity(authoring.bulletPrefabGameObject, TransformUsageFlags.Dynamic),
                bulletPrefabEnemyEntity = GetEntity(authoring.bulletPrefabEnemyGameObject, TransformUsageFlags.Dynamic),
                corpsPrefabEntity = GetEntity(authoring.corpsPrefabGameObject, TransformUsageFlags.Dynamic)
            });
        }
    }
}

public struct EntitiesReferences : IComponentData
{
    public Entity skeletonPrefabEntity;
    public Entity bulletPrefabEntity;
    public Entity bulletPrefabEnemyEntity;
    public Entity corpsPrefabEntity;
}

using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using UnityEngine;

public class GridDataAuthoring : MonoBehaviour
{

    [SerializeField] int width;
    [SerializeField] int height;
    [SerializeField] float cellSize;

    public class Baker : Baker<GridDataAuthoring>
    {
        public override void Bake(GridDataAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new GridConfig
            {
                width = authoring.width,
                height = authoring.height,
                cellSize = authoring.cellSize,
            });

            AddComponent (entity, new IsBlueprintPendingTag());

        }
    }

}

public struct GridConfig : IComponentData
{
    public int width;
    public int height;
    public float cellSize;
  
}

public struct IsBlueprintPendingTag : IComponentData, IEnableableComponent {}

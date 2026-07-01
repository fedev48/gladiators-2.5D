using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GridDebugVisualizer : MonoBehaviour
{
    public enum GridViewMode { Blueprint, FlowField }

    public static GridDebugVisualizer Instance { get; private set; }

    [SerializeField] GameObject cellPrefab;
    [SerializeField] bool debugMode = true;
    [SerializeField] GridViewMode viewMode = GridViewMode.Blueprint;
    [SerializeField] int flowFieldId = 0;

    bool dirty = true;
    GameObject visualRoot;
    int groundMask;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        groundMask = 1 << LayerMask.NameToLayer("Ground");
    }

    void OnValidate() => dirty = true;

    public void MarkDirty() => dirty = true;

    void Update()
    {
        if (!dirty) return;

        ClearVisuals();

        if (!debugMode) { dirty = false; return; }
        if (cellPrefab == null) { dirty = false; Debug.LogWarning("GridDebugVisualizer: falta asignar cellPrefab"); return; }

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        var em = world.EntityManager;

        using var configQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GridConfig>()
            .Build(em);
        if (configQuery.IsEmpty) return;
        GridConfig config = configQuery.GetSingleton<GridConfig>();

        Entity gridEntity = FindGridEntity(em);
        if (gridEntity == Entity.Null) return;

        int targetCellX = -1, targetCellY = -1;
        if (viewMode == GridViewMode.FlowField)
        {
            using var ffQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<FlowFieldMap>()
                .WithAbsent<GridBlueprintTag>()
                .Build(em);
            using var ffEntities = ffQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in ffEntities)
            {
                FlowFieldMap ff = em.GetComponentData<FlowFieldMap>(e);
                if (ff.FlowFieldId == flowFieldId)
                {
                    targetCellX = (int)(ff.Destination.x / config.cellSize) + 1;
                    targetCellY = (int)(ff.Destination.z / config.cellSize) + 1;
                    break;
                }
            }
        }

        var cells = em.GetBuffer<CellComponents>(gridEntity, isReadOnly: true);

        visualRoot = new GameObject("DebugVisuals");
        visualRoot.transform.SetParent(transform);

        for (int i = 0; i < cells.Length; i++)
        {
            CellComponents cellData = cells[i];
            Vector3 center = new Vector3((cellData.x - 1) * config.cellSize, 0, (cellData.y - 1) * config.cellSize);

            GameObject cellObj = Instantiate(cellPrefab, center, Quaternion.identity, visualRoot.transform);
            cellObj.transform.localScale = Vector3.one * config.cellSize;

            bool isWall   = cellData.cost == int.MaxValue;
            bool isTarget = cellData.x == targetCellX && cellData.y == targetCellY;

            SpriteRenderer sr = cellObj.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = isWall ? Color.black : isTarget ? Color.blue : Color.white;
                SnapToGround(cellObj, sr.transform.position, config.cellSize);
            }
            else
                Debug.LogWarning($"GridDebugVisualizer: SpriteRenderer no encontrado en hijo del prefab (celda {i})");
        }

        dirty = false;
    }

    Entity FindGridEntity(EntityManager em)
    {
        if (viewMode == GridViewMode.Blueprint)
        {
            using var q = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<GridBlueprintTag, CellComponents>()
                .Build(em);
            if (q.IsEmpty) return Entity.Null;
            return q.GetSingletonEntity();
        }
        else
        {
            using var q = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<FlowFieldMap, CellComponents>()
                .Build(em);
            if (q.IsEmpty) return Entity.Null;

            using var entities = q.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                if (em.GetComponentData<FlowFieldMap>(e).FlowFieldId == flowFieldId)
                    return e;
            }
            return Entity.Null;
        }
    }

    void SnapToGround(GameObject cellObj, Vector3 spriteWorldPos, float cellSize)
    {
        float half = cellSize * 0.5f;
        Vector3[] corners = {
            new(spriteWorldPos.x - half, spriteWorldPos.y, spriteWorldPos.z - half),
            new(spriteWorldPos.x + half, spriteWorldPos.y, spriteWorldPos.z - half),
            new(spriteWorldPos.x - half, spriteWorldPos.y, spriteWorldPos.z + half),
            new(spriteWorldPos.x + half, spriteWorldPos.y, spriteWorldPos.z + half),
        };

        Vector3[] hits = new Vector3[4];
        for (int c = 0; c < 4; c++)
        {
            Vector3 origin = corners[c] + Vector3.up * 10f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, groundMask)
                && hit.point.y >= 0f && hit.point.y <= 2f)
                hits[c] = new Vector3(corners[c].x, hit.point.y, corners[c].z);
            else
                return;
        }

        Vector3 normal = Vector3.Cross(hits[1] - hits[0], hits[2] - hits[0]).normalized;
        if (Vector3.Dot(normal, Vector3.up) < 0) normal = -normal;

        float avgY = (hits[0].y + hits[1].y + hits[2].y + hits[3].y) * 0.25f;
        cellObj.transform.SetPositionAndRotation(
            new Vector3(cellObj.transform.position.x, avgY, cellObj.transform.position.z),
            Quaternion.FromToRotation(Vector3.up, normal));
    }

    void ClearVisuals()
    {
        if (visualRoot != null) Destroy(visualRoot);
        visualRoot = null;
    }
}

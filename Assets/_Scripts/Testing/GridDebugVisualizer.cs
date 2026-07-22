using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GridDebugVisualizer : MonoBehaviour
{
#if SYSTEM_DEBUG
    public enum GridViewMode { Blueprint, FlowField }

    public static GridDebugVisualizer Instance { get; private set; }

    [SerializeField] GameObject cellPrefab;
    [SerializeField] bool debugMode = true;
    [SerializeField] GridViewMode viewMode = GridViewMode.Blueprint;
    [SerializeField] int flowFieldId = 0;
    [SerializeField] CellDebugView.DisplayMode cellDisplayMode = CellDebugView.DisplayMode.Default;
    [SerializeField] bool drawGridInEditMode = true;
    [SerializeField] float editModeGizmoY = 0.05f;

    bool dirty = true;
    GameObject visualRoot;
    int groundMask;

    Entity trackedFlowField = Entity.Null;
    int trackedDestinationCellIndex = -1;
    float lastRecalcTime = -1f;

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
        CheckFlowFieldChanged();

        if (!dirty) return;

        ClearVisuals();

        if (!debugMode) { dirty = false; return; }
        if (cellPrefab == null) { dirty = false; Debug.LogWarning("GridDebugVisualizer: falta asignar cellPrefab"); return; }

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        var em = world.EntityManager;

        using var configQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<GridConfig>().Build(em);
        if (configQuery.IsEmpty) return;
        GridConfig config = configQuery.GetSingleton<GridConfig>();

        Entity gridEntity = FindGridEntity(em);
        if (gridEntity == Entity.Null) return;

        int targetCellX = -1, targetCellY = -1;
        if (viewMode == GridViewMode.FlowField)
        {
            FlowFieldMap ff = em.GetComponentData<FlowFieldMap>(gridEntity);
            int2 targetCell = GridSystem.IndexToCoords(ff.DestinationCellIndex, config);
            targetCellX = targetCell.x;
            targetCellY = targetCell.y;
        }

        var cells = em.GetBuffer<CellComponents>(gridEntity, isReadOnly: true);

        visualRoot = new GameObject("DebugVisuals");
        visualRoot.transform.SetParent(transform);

        for (int i = 0; i < cells.Length; i++)
        {
            CellComponents cellData = cells[i];
            int2    cell   = GridSystem.IndexToCoords(i, config);
            Vector3 center = new Vector3(cell.x * config.cellSize, 0, cell.y * config.cellSize);

            GameObject cellObj = Instantiate(cellPrefab, center, Quaternion.identity, visualRoot.transform);
            cellObj.transform.localScale = Vector3.one * config.cellSize;

            bool isWall   = cellData.cost == int.MaxValue;
            bool isTarget = i == GridSystem.CoordsToIndex(targetCellX, targetCellY, config);

            var view = cellObj.GetComponent<CellDebugView>();
            if (view != null)
            {
                SnapToGround(cellObj, cellObj.transform.position, config.cellSize);
                Color color = isWall ? Color.black : isTarget ? Color.blue : Color.white;
                view.Show(color, isWall ? CellDebugView.DisplayMode.Default : cellDisplayMode, cellData.movingVector, cellData.bestCost, isTarget);
            }
        }

        dirty = false;
    }

    void CheckFlowFieldChanged()
    {
        if (!debugMode || viewMode != GridViewMode.FlowField) return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        var em = world.EntityManager;

        Entity gridEntity = FindGridEntity(em);
        if (gridEntity == Entity.Null || !em.HasComponent<FlowFieldMap>(gridEntity)) return;

        int destinationCellIndex = em.GetComponentData<FlowFieldMap>(gridEntity).DestinationCellIndex;

        bool entityChanged      = gridEntity != trackedFlowField;
        bool destinationChanged = destinationCellIndex != trackedDestinationCellIndex;
        if (!entityChanged && !destinationChanged) return;

        if (entityChanged)
        {
            Debug.Log($"FlowField {flowFieldId} en pantalla -> celda destino {destinationCellIndex}");
            lastRecalcTime = Time.time;
        }
        else
        {
            Debug.Log($"FlowField {flowFieldId} recalculado -> celda destino {destinationCellIndex} " +
                      $"({Time.time - lastRecalcTime:F2}s desde el recálculo anterior)");
            lastRecalcTime = Time.time;
        }

        trackedFlowField            = gridEntity;
        trackedDestinationCellIndex = destinationCellIndex;
        dirty = true;
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
                .WithAll<FlowFieldPoolSingleton>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(em);
            if (q.IsEmpty) return Entity.Null;

            NativeList<Entity> pool = q.GetSingleton<FlowFieldPoolSingleton>().Pool;
            if (flowFieldId < 0 || flowFieldId >= pool.Length) return Entity.Null;
            return pool[flowFieldId];
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

    void OnDrawGizmos()
    {
        if (Application.isPlaying || !drawGridInEditMode) return;

        var authoring = FindFirstObjectByType<GridDataAuthoring>();
        if (authoring == null) return;

        GridConfig config = authoring.Config;
        if (config.width <= 0 || config.height <= 0 || config.cellSize <= 0f) return;

        int wallMask = LayerMask.GetMask("Walls");
        float half = config.cellSize * 0.5f;
        Vector3 cellFootprint = new Vector3(config.cellSize, 0.01f, config.cellSize);

        for (int y = 0; y < config.height; y++)
        {
            for (int x = 0; x < config.width; x++)
            {
                //same convention as GridSystem.IsOnWall: the cell spans [coord, coord + cellSize), overlap test at its center
                Vector3 corner = new Vector3(x * config.cellSize, editModeGizmoY, y * config.cellSize);
                Vector3 center = corner + new Vector3(half, 0f, half);

                bool isWall = Physics.CheckSphere(center, half, wallMask);

                if (isWall)
                {
                    Gizmos.color = new Color(0f, 0f, 0f, 0.6f);
                    Gizmos.DrawCube(center, cellFootprint);
                }

                Gizmos.color = isWall ? Color.black : new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawWireCube(center, cellFootprint);
            }
        }
    }
#endif
}

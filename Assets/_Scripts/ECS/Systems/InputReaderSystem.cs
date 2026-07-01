using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public partial class InputReaderSystem : SystemBase
{
    private InputSystem_Actions inputSystem;
    private quaternion cameraRot;
    private float3 lastInputDirection = new float3(0f, 0f, 1f);

    protected override void OnCreate()
    {
        inputSystem = new InputSystem_Actions();
        inputSystem.Enable();
    }

    protected override void OnStartRunning()
    {
        cameraRot = quaternion.RotateY(Camera.main.transform.eulerAngles.y * math.TORADIANS);
    }

    protected override void OnUpdate()
    {
        var raw = inputSystem.Player.Move.ReadValue<Vector2>();
        float3 cameraFixedDirection = math.mul(cameraRot, new float3(raw.x, 0, raw.y));

        float3 flatDirection = new float3(cameraFixedDirection.x, 0f, cameraFixedDirection.z);
        if (math.length(flatDirection) > 0.25f) lastInputDirection = math.normalize(flatDirection);

      
        

        foreach (var moveDirection in SystemAPI.Query<RefRW<MoveDirection>>().WithAll<PlayerTag>())
        {
            moveDirection.ValueRW.Value = new float3(cameraFixedDirection.x, 0, cameraFixedDirection.z);
        }

        if (inputSystem.Player.Interact.WasPressedThisFrame())
        {
            float3 randomTarget = new float3(
                UnityEngine.Random.Range(0, 100),
                0f,
                UnityEngine.Random.Range(0, 100));

            Entity testEntity = EntityManager.CreateEntity(
                typeof(Unity.Transforms.LocalTransform),
                typeof(NeedsPathfinding),
                typeof(UsingPathfinding));

            EntityManager.SetComponentData(testEntity, Unity.Transforms.LocalTransform.Identity);
            EntityManager.SetComponentData(testEntity, new NeedsPathfinding { Destination = randomTarget });
            EntityManager.SetComponentEnabled<NeedsPathfinding>(testEntity, true);
            EntityManager.SetComponentEnabled<UsingPathfinding>(testEntity, false);

            if (GridDebugVisualizer.Instance != null) GridDebugVisualizer.Instance.MarkDirty();
            Debug.Log($"[FlowfieldTest] entity {testEntity.Index} -> target {randomTarget}");
            // foreach ((RefRO<SkeletonSpellConfig> spellConfig, Entity entity) in
            //     SystemAPI.Query<RefRO<SkeletonSpellConfig>>().WithEntityAccess())
            // {
            //     EntityManager.SetComponentData(entity, new SkeletonSpawnBurst
            //     {
            //         remaining = spellConfig.ValueRO.spawnCount,
            //         timer     = 0f
            //     });
            //     EntityManager.SetComponentEnabled<SkeletonSpawnBurst>(entity, true);
            // }        
        }

        if (inputSystem.Player.Attack.WasPressedThisFrame())
        {
            foreach ((RefRO<BulletSpellConfig> _, Entity entity) in
                SystemAPI.Query<RefRO<BulletSpellConfig>>().WithEntityAccess())
            {
                EntityManager.SetComponentData(entity, new FireBulletEvent { direction = lastInputDirection });
                EntityManager.SetComponentEnabled<FireBulletEvent>(entity, true);
            }
        }

        

    }

    protected override void OnStopRunning() => inputSystem.Disable();
}
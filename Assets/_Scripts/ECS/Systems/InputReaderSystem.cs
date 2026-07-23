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

      
        

        foreach ((RefRW<DesiredVelocity> desired, RefRO<MoveSpeed> speed) in SystemAPI.Query<RefRW<DesiredVelocity>, RefRO<MoveSpeed>>().WithAll<PlayerTag>())
        {
            desired.ValueRW.Value = new float3(cameraFixedDirection.x, 0, cameraFixedDirection.z) * speed.ValueRO.Value;
        }

        if (inputSystem.Player.Interact.WasPressedThisFrame())
        {
            // float3 randomTarget = new float3(
            //     UnityEngine.Random.Range(0, 100),
            //     0f,
            //     UnityEngine.Random.Range(0, 100));

            // Entity testEntity = EntityManager.CreateEntity(
            //     typeof(Unity.Transforms.LocalTransform),
            //     typeof(NeedsPathfinding),
            //     typeof(UsingPathfinding));

            // EntityManager.SetComponentData(testEntity, Unity.Transforms.LocalTransform.Identity);
            // EntityManager.SetComponentData(testEntity, new NeedsPathfinding { Destination = randomTarget });
            // EntityManager.SetComponentEnabled<NeedsPathfinding>(testEntity, true);
            // EntityManager.SetComponentEnabled<UsingPathfinding>(testEntity, false);

            // if (GridDebugVisualizer.Instance != null) GridDebugVisualizer.Instance.MarkDirty();
            // Debug.Log($"[FlowfieldTest] entity {testEntity.Index} -> target {randomTarget}");
            foreach ((RefRO<SkeletonSpellConfig> spellConfig, RefRO<VisualEntity> visual, Entity entity) in
                SystemAPI.Query<RefRO<SkeletonSpellConfig>, RefRO<VisualEntity>>().WithEntityAccess())
            {
                EntityManager.SetComponentData(entity, new SummonSkeletonEvent { count = spellConfig.ValueRO.spawnCount });
                EntityManager.SetComponentEnabled<SummonSkeletonEvent>(entity, true);

                Entity visualEntity = visual.ValueRO.Value;
                AnimationDirection facing = EntityManager.GetComponentData<AnimRequest>(visualEntity).direction;
                EntityManager.SetComponentData(visualEntity, new IsOneShot { animation = Animation.Cast, animationDirection = facing });
                EntityManager.SetComponentEnabled<IsOneShot>(visualEntity, true);

                float castDuration = GetClipDuration(visualEntity, Animation.Cast, facing);
                float remainingTime = EntityManager.GetComponentData<MovementBlocked>(entity).remainingTime;
                EntityManager.SetComponentData(entity, new MovementBlocked { remainingTime = math.max(remainingTime, castDuration) });
                EntityManager.SetComponentEnabled<MovementBlocked>(entity, true);
            }
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

    float GetClipDuration(Entity visualEntity, Animation animation, AnimationDirection direction)
    {
        DynamicBuffer<AnimationClipData> clips = EntityManager.GetBuffer<AnimationClipData>(visualEntity);

        foreach (AnimationClipData clip in clips)
        {
            if (clip.role != animation || clip.direction != direction) continue;

            AnimationClipData resolved = clip.overrideTo >= 0 ? clips[clip.overrideTo] : clip;
            return resolved.frameCount / resolved.fps;
        }

        return 0f;
    }

    protected override void OnStopRunning() => inputSystem.Disable();
}
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct MovementAnimRequestSystem : ISystem
{
    ComponentLookup<AnimRequest>      _requestLookup;
    ComponentLookup<CameraFacingData> _cameraLookup;

    public void OnCreate(ref SystemState state)
    {
        _requestLookup = state.GetComponentLookup<AnimRequest>(isReadOnly: false);
        _cameraLookup  = state.GetComponentLookup<CameraFacingData>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _requestLookup.Update(ref state);
        _cameraLookup.Update(ref state);

        foreach ((RefRO<MoveDirection> moveDirection, RefRO<VisualEntity> visual) in
            SystemAPI.Query<RefRO<MoveDirection>, RefRO<VisualEntity>>()
                .WithAll<UnitMovementAnimTag>())
        {
            Entity visualEntity = visual.ValueRO.Value;
            if (!_requestLookup.HasComponent(visualEntity)) continue;

            RefRW<AnimRequest> request = _requestLookup.GetRefRW(visualEntity);

            float3 worldDir = moveDirection.ValueRO.Value;
            if (math.lengthsq(worldDir) <= 0.01f)
            {

                request.ValueRW.role = Animation.Idle;
                continue;
            }

            quaternion invRotation = _cameraLookup.HasComponent(visualEntity)
                ? _cameraLookup[visualEntity].invRotation
                : quaternion.identity;
            float3 screenDir = math.mul(invRotation, worldDir);

            request.ValueRW.role      = Animation.Walk;
            request.ValueRW.direction = FacingDirection(screenDir);
        }
    }

    static AnimationDirection FacingDirection(float3 direction)
    {
        float absX = math.abs(direction.x);
        float absZ = math.abs(direction.z);
        if (absZ >= absX) return direction.z >= 0f ? AnimationDirection.Back      : AnimationDirection.Front;
        else              return direction.x >= 0f ? AnimationDirection.SideRight : AnimationDirection.SideLeft;
    }
}

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

        foreach ((RefRO<DesiredVelocity> desiredVelocity, RefRO<VisualEntity> visual) in
            SystemAPI.Query<RefRO<DesiredVelocity>, RefRO<VisualEntity>>()
                .WithAll<UnitMovementAnimTag>())
        {
            Entity visualEntity = visual.ValueRO.value;
            if (!_requestLookup.HasComponent(visualEntity)) continue;

            RefRW<AnimRequest> request = _requestLookup.GetRefRW(visualEntity);

            float3 worldDir = desiredVelocity.ValueRO.value;
            if (math.lengthsq(worldDir) <= 0.01f)
            {

                request.ValueRW.role = Animation.Idle;
                continue;
            }

            quaternion invRotation    = quaternion.identity;
            bool       fourDirections = true;

            if (_cameraLookup.HasComponent(visualEntity))
            {
                CameraFacingData facingData = _cameraLookup[visualEntity];
                invRotation    = facingData.invRotation;
                fourDirections = facingData.fourDirections;
            }

            float3 screenDir = math.mul(invRotation, worldDir);

            request.ValueRW.role      = Animation.Walk;
            request.ValueRW.direction = AnimationActions.FacingDirection(screenDir, fourDirections);
        }
    }
}

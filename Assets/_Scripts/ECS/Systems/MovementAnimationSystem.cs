using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
// [BurstCompile]
public partial struct MovementAnimationSystem : ISystem
{
    ComponentLookup<SpriteAnimationState> _animLookup;
    ComponentLookup<OneShotAnimTag>       _oneShotLookup;
    ComponentLookup<CameraFacingData>     _cameraLookup;

    public void OnCreate(ref SystemState state)
    {
        _animLookup    = state.GetComponentLookup<SpriteAnimationState>(isReadOnly: false);
        _oneShotLookup = state.GetComponentLookup<OneShotAnimTag>(isReadOnly: true);
        _cameraLookup  = state.GetComponentLookup<CameraFacingData>(isReadOnly: true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _animLookup.Update(ref state);
        _oneShotLookup.Update(ref state);
        _cameraLookup.Update(ref state);

        foreach (var (moveDir, visual, entity) in
            SystemAPI.Query<RefRO<MoveDirection>, RefRO<VisualEntity>>()
                .WithAll<UnitMovementAnimTag>().WithEntityAccess())
        {
            Entity visualEntity = visual.ValueRO.Value;

            if (_oneShotLookup.IsComponentEnabled(visualEntity)) continue;
            if (!_animLookup.HasComponent(visualEntity))         continue;

            RefRW<SpriteAnimationState> animState = _animLookup.GetRefRW(visualEntity);

            float3 worldDir = moveDir.ValueRO.Value;
            bool   moving   = math.lengthsq(worldDir) > 0.01f;

            // Convert world-space direction to screen-space using baked inverse camera rotation
            quaternion invRot = _cameraLookup.HasComponent(visualEntity)
                ? _cameraLookup[visualEntity].invRotation
                : quaternion.identity;
            float3 screenDir = math.mul(invRot, worldDir);

            int facing = moving
                ? FacingIndex(screenDir)
                : math.max(0, animState.ValueRO.prevAnimation) % 4;

            animState.ValueRW.currentAnimation = moving ? 4 + facing : facing;
        }
    }

    static int FacingIndex(float3 dir)
    {
        float ax = math.abs(dir.x);
        float az = math.abs(dir.z);
        if (az >= ax) return dir.z >= 0f ? (int)MoveDir4.North : (int)MoveDir4.South;
        else          return dir.x >= 0f ? (int)MoveDir4.East  : (int)MoveDir4.West;
    }
}

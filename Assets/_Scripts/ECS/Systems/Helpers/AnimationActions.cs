using Unity.Entities;
using Unity.Mathematics;

public static class AnimationActions
{
    //takes a direction already rotated into screen space
    public static AnimationDirection FacingDirection(float3 direction, bool fourDirections)
    {
        if (!fourDirections) return direction.x >= 0f ? AnimationDirection.SideRight : AnimationDirection.SideLeft;

        float absX = math.abs(direction.x);
        float absZ = math.abs(direction.z);
        if (absZ >= absX) return direction.z >= 0f ? AnimationDirection.Back      : AnimationDirection.Front;
        else              return direction.x >= 0f ? AnimationDirection.SideRight : AnimationDirection.SideLeft;
    }

    //rotates a world space direction into screen space using the visual's camera data
    public static AnimationDirection ResolveDirection(
        Entity visualEntity,
        float3 worldDirection,
        ref ComponentLookup<CameraFacingData> cameraLookup)
    {
        quaternion invRotation    = quaternion.identity;
        bool       fourDirections = true;

        if (cameraLookup.HasComponent(visualEntity))
        {
            CameraFacingData facingData = cameraLookup[visualEntity];
            invRotation    = facingData.invRotation;
            fourDirections = facingData.fourDirections;
        }

        return FacingDirection(math.mul(invRotation, worldDirection), fourDirections);
    }

    //resolved clip is returned so the caller can derive its duration and hit frame
    public static bool TryPlayOneShot(
        Entity visualEntity,
        Animation role,
        AnimationDirection direction,
        ref BufferLookup<AnimationClipData> clipsLookup,
        ref ComponentLookup<IsOneShot> oneShotLookup,
        out AnimationClipData clip)
    {
        clip = default;

        if (!clipsLookup.HasBuffer(visualEntity) || !oneShotLookup.HasComponent(visualEntity)) return false;
        if (!TryGetClip(role, direction, clipsLookup[visualEntity], out clip)) return false;
        if (clip.fps <= 0f) return false;

        oneShotLookup[visualEntity] = new IsOneShot { animation = role, animationDirection = direction };
        oneShotLookup.SetComponentEnabled(visualEntity, true);

        return true;
    }

    public static bool TryGetClip(Animation animation, AnimationDirection direction, DynamicBuffer<AnimationClipData> clips, out AnimationClipData resolved)
    {
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].role != animation || clips[i].direction != direction) continue;

            int resolvedIndex = i;
            for (int guard = 0; guard < clips.Length && clips[resolvedIndex].overrideTo >= 0; guard++)
                resolvedIndex = clips[resolvedIndex].overrideTo;

            resolved = clips[resolvedIndex];
            return resolved.frameCount > 0;
        }

        resolved = default;
        return false;
    }
}

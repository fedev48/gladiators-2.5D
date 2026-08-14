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

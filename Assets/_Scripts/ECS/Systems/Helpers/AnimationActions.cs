using Unity.Entities;

public static class AnimationActions
{
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

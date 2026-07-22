using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct SpriteAnimationSystem : ISystem
{
    [BurstCompile]
    public readonly void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<SpriteAnimationState> stateRef,
                  RefRO<AnimRequest> request,
                  RefRO<IsOneShot> oneShot,
                  EnabledRefRW<IsOneShot> oneShotEnabled,
                  DynamicBuffer<AnimationClipData> clips,
                  DynamicBuffer<SpriteFrameElement> frames,
                  RefRW<SpriteUVRect> uvRef) in
            SystemAPI.Query<RefRW<SpriteAnimationState>,
                            RefRO<AnimRequest>,
                            RefRO<IsOneShot>,
                            EnabledRefRW<IsOneShot>,
                            DynamicBuffer<AnimationClipData>,
                            DynamicBuffer<SpriteFrameElement>,
                            RefRW<SpriteUVRect>>()
                .WithPresent<IsOneShot>())
        {
            ref SpriteAnimationState animState = ref stateRef.ValueRW;

            bool playingOneShot = oneShotEnabled.ValueRO;
            Animation          targetRole = playingOneShot ? oneShot.ValueRO.animation          : request.ValueRO.role;
            AnimationDirection targetDir  = playingOneShot ? oneShot.ValueRO.animationDirection : request.ValueRO.direction;

            if (targetRole != animState.currentAnimation)
            {
                animState.currentAnimation   = targetRole;
                animState.animationDirection = targetDir;
                animState.currentFrame = 0;
                animState.elapsed      = 0f;
            }
            else if (targetDir != animState.animationDirection)
            {
                animState.animationDirection = targetDir;
            }

            int clipIndex = ResolveClip(clips, animState.currentAnimation, animState.animationDirection);
            for (int guard = 0; guard < clips.Length && clips[clipIndex].overrideTo >= 0; guard++)
                clipIndex = clips[clipIndex].overrideTo;

            AnimationClipData clip = clips[clipIndex];
            if (clip.frameCount == 0) continue;

            if (animState.currentFrame >= clip.frameCount) animState.currentFrame = clip.frameCount - 1;

            animState.elapsed += deltaTime;
            if (animState.elapsed >= 1f / clip.fps)
            {
                animState.elapsed -= 1f / clip.fps;

                int nextFrame = animState.currentFrame + 1;
                if (nextFrame >= clip.frameCount)
                {
                    if (playingOneShot)
                    {
                        //  one shot ends, envent should trigger
                        oneShotEnabled.ValueRW = false;
                    }
                    else
                    {
                        animState.currentFrame = 0;
                    }
                }
                else
                {
                    animState.currentFrame = nextFrame;
                }
            }

            uvRef.ValueRW.value = frames[clip.startIndex + animState.currentFrame].uv;
        }
    }

    static int ResolveClip(in DynamicBuffer<AnimationClipData> clips, Animation role, AnimationDirection direction)
    {
        int roleFallback = -1;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].role != role) continue;
            if (clips[i].direction == direction) return i;
            if (roleFallback < 0) roleFallback = i;
        }
        return roleFallback >= 0 ? roleFallback : 0;
    }
}

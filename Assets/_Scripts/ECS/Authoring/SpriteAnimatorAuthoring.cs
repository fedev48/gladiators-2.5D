using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class SpriteAnimatorAuthoring : MonoBehaviour
{
    public List<SpriteAnimationClip> animations;
    public Animation initialAnimation = 0;
    public Animation currentAnimation = 0;
    public Vector2 flipPivotOffset  = Vector2.zero;
    public float   cameraYAngle     = -135f;
    public bool               debugOverride    = false;
    public Animation          debugAnimation   = Animation.Idle;
    public AnimationDirection debugDirection   = AnimationDirection.Front;

    public class Baker : Baker<SpriteAnimatorAuthoring>
    {
        public override void Bake(SpriteAnimatorAuthoring authoring)
        {
            if (authoring.animations == null || authoring.animations.Count == 0) return;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpriteAnimationState
            {
                currentAnimation = authoring.initialAnimation,
                currentFrame     = 0,
                elapsed          = 0f,
            });

            AddComponent(entity, new AnimRequest
            {
                role      = Animation.Idle,
                direction = AnimationDirection.Front,
            });



            var clipBuffer  = AddBuffer<AnimationClipData>(entity);
            var frameBuffer = AddBuffer<SpriteFrameElement>(entity);

            int frameOffset = 0;
            foreach (var clip in authoring.animations)
            {
                if (clip.isOverride && clip.flip)
                {
                    
                    var target     = authoring.animations[clip.overrideTo];
                    int frameCount = target.frames?.Count ?? 0;
                    clipBuffer.Add(new AnimationClipData
                    {
                        role       = clip.animation,
                        direction  = clip.animationDirection,
                        startIndex = frameOffset,
                        frameCount = frameCount,
                        fps        = Mathf.Max(target.fps, 0.01f),
                        hitFrame   = target.hitFrame,
                        overrideTo = -1
                    });
                    if (target.frames != null)
                    {
                        float2 po = (float2)authoring.flipPivotOffset;
                        foreach (var sprite in target.frames)
                        {
                            float4 uv = SpriteToUV(sprite);
                            frameBuffer.Add(new SpriteFrameElement { uv = new float4(uv.x + uv.z + po.x, uv.y + po.y, -uv.z, uv.w) });
                        }
                    }
                    frameOffset += frameCount;
                }
                else
                {
                    clipBuffer.Add(new AnimationClipData
                    {
                        role       = clip.animation,
                        direction  = clip.animationDirection,
                        startIndex = frameOffset,
                        frameCount = clip.isOverride ? 0 : (clip.frames?.Count ?? 0),
                        fps        = Mathf.Max(clip.fps, 0.01f),
                        hitFrame   = clip.hitFrame,
                        overrideTo = clip.isOverride ? clip.overrideTo : -1
                    });
                    if (!clip.isOverride && clip.frames != null)
                    {
                        foreach (var sprite in clip.frames)
                            frameBuffer.Add(new SpriteFrameElement { uv = SpriteToUV(sprite) });
                    }
                    frameOffset += clip.isOverride ? 0 : (clip.frames?.Count ?? 0);
                }
            }

            AddComponent(entity, new SpriteUVRect { value = frameBuffer.Length > 0 ? frameBuffer[0].uv : default });
            AddComponent(entity, new SpriteMaskColor { value = new float4(0f, 0f, 0f, 1f) });
            AddComponent(entity, new DamageAnimation());
            SetComponentEnabled<DamageAnimation>(entity, false);
            AddComponent(entity, new IsOneShot
            {
                animation = Animation.None,
                animationDirection = 0
            });
            SetComponentEnabled<IsOneShot>(entity, false);
            AddComponent(entity, new CameraFacingData { invRotation = math.inverse(quaternion.RotateY(math.radians(authoring.cameraYAngle))) });

#if SYSTEM_DEBUG
            AddComponent(entity, new DebugAnimationOverride
            {
                animation = authoring.debugAnimation,
                direction = authoring.debugDirection
            });
            SetComponentEnabled<DebugAnimationOverride>(entity, authoring.debugOverride);
#endif
        }

        static float4 SpriteToUV(Sprite sprite)
        {
            Rect    r  = sprite.rect;
            Vector2 ts = sprite.texture.texelSize;
            return new float4(r.xMin * ts.x, r.yMin * ts.y, r.width * ts.x, r.height * ts.y);
        }
    }
}
#region Authoring
[Serializable]
public class SpriteAnimationClip
{
    public Animation animation = Animation.None;
    public AnimationDirection animationDirection = AnimationDirection.Front;
    public List<Sprite> frames;
    public float        fps        = 8f;
    public int          hitFrame   = -1;
    public bool         isOverride = false;
    public int          overrideTo = 0;
    public bool         flip       = false;
}

public enum Animation
{
    None,
    Idle,
    Walk,
    Attack,
    Cast,
    Emerge
}
public enum AnimationDirection : byte
{
    Front,
    SideRight,
    SideLeft,
    Back
}
#endregion

#region State

public struct AnimRequest : IComponentData
{
    public Animation          role;
    public AnimationDirection direction;
}

public struct SpriteAnimationState : IComponentData
{
    public Animation          currentAnimation;    
    public AnimationDirection animationDirection;
    public int                currentFrame;
    public float              elapsed;
}

public struct IsOneShot : IComponentData, IEnableableComponent
{
    public Animation          animation;
    public AnimationDirection animationDirection;
}

#if SYSTEM_DEBUG
public struct DebugAnimationOverride : IComponentData, IEnableableComponent
{
    public Animation          animation;
    public AnimationDirection direction;
}
#endif
#endregion

#region Runtime

[InternalBufferCapacity(4)]
public struct AnimationClipData : IBufferElementData
{
    public Animation          role;
    public AnimationDirection direction;
    public int   startIndex;
    public int   frameCount;
    public float fps;
    public int   hitFrame;
    public int   overrideTo;
}

[InternalBufferCapacity(16)]
public struct SpriteFrameElement : IBufferElementData
{
    public float4 uv;
}

[MaterialProperty("_SpriteUV")]
public struct SpriteUVRect : IComponentData
{
    public float4 value;
}

[MaterialProperty("_MaskColor")]
public struct SpriteMaskColor : IComponentData
{
    public float4 value;
}

public struct CameraFacingData : IComponentData
{
    public quaternion invRotation;
}
#endregion
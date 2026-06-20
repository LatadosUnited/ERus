using ERus.Engine.Assets;
using System.Numerics;
using System;

namespace ERus.Engine.ECS;

public class AnimatorSystem : BaseSystem
{
    private Core.Engine _engine;

    public AnimatorSystem(Registry registry, Core.Engine engine) : base(registry)
    {
        _engine = engine;
    }

    public override void Update(double deltaTime)
    {
        foreach (var entity in Registry.View<AnimatorComponent, MeshComponent>())
        {
            ref var animator = ref Registry.GetComponent<AnimatorComponent>(entity);
            ref var meshComponent = ref Registry.GetComponent<MeshComponent>(entity);

            if (!animator.IsPlaying || string.IsNullOrEmpty(animator.CurrentAnimationName)) continue;

            string? path = _engine.AssetDatabase.GetPathByGuid(meshComponent.AssetGuid);
            if (string.IsNullOrEmpty(path)) continue;

            var model = AssetManager.Get().LoadModel(path);
            if (model == null || !model.Animations.ContainsKey(animator.CurrentAnimationName)) continue;

            var animation = model.Animations[animator.CurrentAnimationName];

            animator.CurrentTime += (float)deltaTime * animation.TicksPerSecond * animator.PlaybackSpeed;
            animator.CurrentTime = animator.CurrentTime % animation.Duration;

            CalculateBoneTransform(model.RootNode, Matrix4x4.Identity, animation, animator);
        }
    }

    private void CalculateBoneTransform(NodeHierarchy node, Matrix4x4 parentTransform, AnimationData animation, AnimatorComponent animator)
    {
        string nodeName = node.Name;
        Matrix4x4 nodeTransform = node.Transformation;

        if (animation.Channels.TryGetValue(nodeName, out var channel))
        {
            var pos = InterpolatePosition(animator.CurrentTime, channel);
            var rot = InterpolateRotation(animator.CurrentTime, channel);
            var scale = InterpolateScaling(animator.CurrentTime, channel);

            var translationMatrix = Matrix4x4.CreateTranslation(pos);
            var rotationMatrix = Matrix4x4.CreateFromQuaternion(rot);
            var scaleMatrix = Matrix4x4.CreateScale(scale);

            nodeTransform = scaleMatrix * rotationMatrix * translationMatrix;
        }

        Matrix4x4 globalTransformation = nodeTransform * parentTransform;

        if (animation.BoneInfoMap.TryGetValue(nodeName, out var boneInfo))
        {
            int index = boneInfo.Id;
            var offset = boneInfo.Offset;
            animator.FinalBoneMatrices[index] = offset * globalTransformation;
        }

        foreach (var child in node.Children)
        {
            CalculateBoneTransform(child, globalTransformation, animation, animator);
        }
    }

    private Vector3 InterpolatePosition(float animationTime, BoneAnimationChannel channel)
    {
        if (channel.Positions.Count == 0) return Vector3.Zero;
        if (channel.Positions.Count == 1) return channel.Positions[0].Position;

        int p0Index = channel.GetPositionIndex(animationTime);
        int p1Index = p0Index + 1;
        float scaleFactor = GetScaleFactor(channel.Positions[p0Index].TimeStamp, channel.Positions[p1Index].TimeStamp, animationTime);
        return Vector3.Lerp(channel.Positions[p0Index].Position, channel.Positions[p1Index].Position, scaleFactor);
    }

    private Quaternion InterpolateRotation(float animationTime, BoneAnimationChannel channel)
    {
        if (channel.Rotations.Count == 0) return Quaternion.Identity;
        if (channel.Rotations.Count == 1) return channel.Rotations[0].Orientation;

        int p0Index = channel.GetRotationIndex(animationTime);
        int p1Index = p0Index + 1;
        float scaleFactor = GetScaleFactor(channel.Rotations[p0Index].TimeStamp, channel.Rotations[p1Index].TimeStamp, animationTime);
        return Quaternion.Slerp(channel.Rotations[p0Index].Orientation, channel.Rotations[p1Index].Orientation, scaleFactor);
    }

    private Vector3 InterpolateScaling(float animationTime, BoneAnimationChannel channel)
    {
        if (channel.Scales.Count == 0) return Vector3.One;
        if (channel.Scales.Count == 1) return channel.Scales[0].Scale;

        int p0Index = channel.GetScaleIndex(animationTime);
        int p1Index = p0Index + 1;
        float scaleFactor = GetScaleFactor(channel.Scales[p0Index].TimeStamp, channel.Scales[p1Index].TimeStamp, animationTime);
        return Vector3.Lerp(channel.Scales[p0Index].Scale, channel.Scales[p1Index].Scale, scaleFactor);
    }

    private float GetScaleFactor(float lastTimeStamp, float nextTimeStamp, float animationTime)
    {
        float framesDiff = nextTimeStamp - lastTimeStamp;
        if (framesDiff <= 0.0f) return 0.0f;
        float midWayLength = animationTime - lastTimeStamp;
        return midWayLength / framesDiff;
    }
}

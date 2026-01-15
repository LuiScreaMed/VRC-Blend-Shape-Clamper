using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    public class ProcessAnimationPass : Pass<ProcessAnimationPass>
    {
        public override string DisplayName => "Process Animation";
        public override string QualifiedName => "LuiStudio.BlendSHapeClamper.Passes.ProcessAnimation";

        protected override void Execute(BuildContext context)
        {
            BlendShapeClamperContext clamperContext = context.Extension<BlendShapeClamperContext>();
            AnimatorServicesContext servicesContext = context.Extension<AnimatorServicesContext>();
            VirtualControllerContext controllerContext = servicesContext.ControllerContext;
            if (!controllerContext.Controllers.TryGetValue(AnimLayerType.FX, out VirtualAnimatorController fx))
            {
                return;
            }

            List<VirtualClip> clips = fx.AllReachableNodes().OfType<VirtualClip>().ToList();

            foreach (VirtualClip clip in clips)
            {
                List<EditorCurveBinding> bindings = clip.GetFloatCurveBindings().ToList();
                foreach (EditorCurveBinding binding in bindings)
                {
                    if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                    if (!binding.propertyName.StartsWith("blendShape.")) continue;
                    if (!clamperContext.ClampedBlendShapes.ContainsKey(binding.path)) continue;

                    string blendShapeName = binding.propertyName.Replace("blendShape.", "");

                    Dictionary<string, (int, float)> shapes = clamperContext.ClampedBlendShapes[binding.path];
                    if (!shapes.ContainsKey(blendShapeName)) continue;

                    float clampedTo = shapes[blendShapeName].Item2;
                    AnimationCurve curve = clip.GetFloatCurve(binding);
                    AnimationCurve newCurve = new();
                    Keyframe[] keyframes = curve.keys;
                    List<Keyframe> newKeyframes = new();
                    foreach (Keyframe keyframe in keyframes)
                    {
                        float value = keyframe.value;
                        value = value < clampedTo ? value / clampedTo * 100 : 100;
                        Keyframe newKeyframe = new()
                        {
                            inTangent = keyframe.inTangent,
                            inWeight = keyframe.inWeight,
                            outTangent = keyframe.outTangent,
                            outWeight = keyframe.outWeight,
                            time = keyframe.time,
                            value = value,
                            weightedMode = keyframe.weightedMode,
                        };
                        newKeyframes.Add(newKeyframe);
                    }
                    newCurve.keys = newKeyframes.ToArray();
                    for (int i = 0; i < keyframes.Length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(newCurve, i, AnimationUtility.GetKeyLeftTangentMode(curve, i));
                    }
                    clip.SetFloatCurve(binding, newCurve);
                }
            }
        }
    }
}
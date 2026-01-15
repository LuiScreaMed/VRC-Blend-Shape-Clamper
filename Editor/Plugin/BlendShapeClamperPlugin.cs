using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEngine;

[assembly: ExportsPlugin(typeof(LuiStudio.Utilities.BlendShapeClamper.Editor.BlendShapeClamperPlugin))]
namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    internal class BlendShapeClamperContext : IExtensionContext
    {
        internal Dictionary<string, Dictionary<string, (int, float)>> ClampedBlendShapes;
        private bool _activated = false;

        public void OnActivate(BuildContext context)
        {
            if (_activated) return;
            _activated = true;
            ClampedBlendShapes = new();
        }

        public void OnDeactivate(BuildContext context)
        {
            ClampedBlendShapes = null;
            _activated = false;
        }
    }

    public class BlendShapeClamperPlugin : Plugin<BlendShapeClamperPlugin>
    {
        public override string QualifiedName => "LuiStudio.BlendShapeClamper";
        public override string DisplayName => "Blend Shape Clamper";
        public override Color? ThemeColor => new Color(0xff / 255f, 0x80 / 255f, 0x0 / 255f, 1);

        protected override void Configure()
        {
            InPhase(
                BuildPhase.Optimizing
            ).WithRequiredExtensions(
                new[]
                {
                    typeof(AnimatorServicesContext),
                    typeof(BlendShapeClamperContext)
                },
                (_seq) =>
                {
                    // The values of clamped blend shapes will be overwritten by the following rules:
                    // 1) When the original value is equal with or larger than the clamped value,
                    //    overwrite it to 100.
                    //    e.g. Original value: 80; Clamped to: 70; Result: 100
                    // 2) When the original value is smaller than the clamped value,
                    //    overwrite it to (original value / clamped value * 100)
                    //    e.g. Original value: 50; Clamped to: 70; Result: 50 / 70 * 100 ≈ 71.4286

                    // Clamp the blend shapes in skinned mesh renderer and overwrite values
                    _seq.Run(ClampBlendShapePass.Instance);
                    // Overwrite keyframe values in animations
                    _seq.Run(ProcessAnimationPass.Instance);
                    _seq.Run(
                        "Purge Blend Shape Clamper Components",
                        (ctx) =>
                        {
                            foreach (BlendShapeClamper component in ctx.AvatarRootTransform.GetComponentsInChildren<BlendShapeClamper>(true))
                            {
                                Object.DestroyImmediate(component);
                            }
                        }
                    );
                }
            );
        }
    }
}
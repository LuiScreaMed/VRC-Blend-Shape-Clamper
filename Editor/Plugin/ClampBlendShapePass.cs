using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.util;
using UnityEngine;


namespace LuiStudio.Utilities.BlendShapeClamper.Editor
{
    internal class ClampBlendShapePass : Pass<ClampBlendShapePass>
    {
        public override string DisplayName => "Clamp Blend Shape";
        public override string QualifiedName => "LuiStudio.BlendSHapeClamper.Passes.ClampBlendShape";

        protected override void Execute(BuildContext context)
        {
            BlendShapeClamperContext clamperContext = context.Extension<BlendShapeClamperContext>();

            BlendShapeClamper[] clampers = context.AvatarRootObject.GetComponentsInChildren<BlendShapeClamper>();

            foreach (BlendShapeClamper clamper in clampers)
            {
                ClampBlendShapes(context, clamperContext, clamper);
            }
        }

        /// <summary>
        /// Clamp blendshapes of the skinned mesh renderer in the same gameobject with the clamper component.
        /// </summary>
        /// <param name="clamper"></param>
        private void ClampBlendShapes(BuildContext context, BlendShapeClamperContext clamperContext, BlendShapeClamper clamper)
        {
            if (clamper.ClampItems.Count == 0) return;

            SkinnedMeshRenderer renderer = clamper.GetComponent<SkinnedMeshRenderer>();
            if (renderer.sharedMesh == null) return;

            Mesh mesh = Object.Instantiate(renderer.sharedMesh);
            renderer.sharedMesh = mesh;

            List<BlendShapesData> newBlendShapes = new();

            Dictionary<string, (int, float)> clampedShapes = new();

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                int frameCount = mesh.GetBlendShapeFrameCount(i);

                List<float> weights = new();
                List<Vector3[]> deltaVertices = new();
                List<Vector3[]> deltaNormals = new();
                List<Vector3[]> deltaTangents = new();

                for (int f = 0; f < frameCount; f++)
                {
                    Vector3[] frameDeltaVertices = new Vector3[mesh.vertexCount];
                    Vector3[] frameDeltaNormals = new Vector3[mesh.vertexCount];
                    Vector3[] frameDeltaTangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(
                        i,
                        f,
                        frameDeltaVertices,
                        frameDeltaNormals,
                        frameDeltaTangents
                    );
                    float weight = mesh.GetBlendShapeFrameWeight(i, f);
                    int itemIndex = clamper.ClampItems.FindIndex(
                        (item) => item.BlendShape == name
                    );

                    if (itemIndex != -1)
                    {
                        BlendShapeClampItem item = clamper.ClampItems[itemIndex];
                        float scale = item.ClampTo * .01f;
                        for (int k = 0; k < frameDeltaVertices.Length; k++)
                        {
                            frameDeltaVertices[k] *= scale;
                            frameDeltaNormals[k] *= scale;
                            frameDeltaTangents[k] *= scale;
                        }
                        clampedShapes.Add(name, (i, item.ClampTo));
                    }

                    weights.Add(weight);
                    deltaVertices.Add(frameDeltaVertices);
                    deltaNormals.Add(frameDeltaNormals);
                    deltaTangents.Add(frameDeltaTangents);
                }

                newBlendShapes.Add(
                    new(
                        name,
                        weights,
                        deltaVertices,
                        deltaNormals,
                        deltaTangents
                    )
                );
            }

            if (clampedShapes.Count > 0)
            {
                clamperContext.ClampedBlendShapes.Add(
                    MiscHelpers.AvatarRootPath(clamper.gameObject),
                    clampedShapes
                );
            }

            foreach (KeyValuePair<string, (int, float)> clampedShape in clampedShapes)
            {
                int index = clampedShape.Value.Item1;
                float clampedValue = clampedShape.Value.Item2;
                float weight = renderer.GetBlendShapeWeight(index);
                weight = weight < clampedValue ? weight / clampedValue * 100 : 100;
                renderer.SetBlendShapeWeight(index, weight);
            }

            mesh.ClearBlendShapes();

            foreach (BlendShapesData data in newBlendShapes)
            {
                for (int f = 0; f < data.weights.Count; f++)
                {
                    mesh.AddBlendShapeFrame(
                        data.name,
                        data.weights[f],
                        data.deltaVertices[f],
                        data.deltaNormals[f],
                        data.deltaTangents[f]
                    );
                }
            }

            context.AssetSaver.SaveAsset(mesh);
        }

        private struct BlendShapesData
        {
            public string name;
            public List<float> weights;
            public List<Vector3[]> deltaVertices;
            public List<Vector3[]> deltaNormals;
            public List<Vector3[]> deltaTangents;

            public BlendShapesData(
                string name,
                List<float> weights,
                List<Vector3[]> deltaVertices,
                List<Vector3[]> deltaNormals,
                List<Vector3[]> deltaTangents
            )
            {
                this.name = name;
                this.weights = weights;
                this.deltaVertices = deltaVertices;
                this.deltaNormals = deltaNormals;
                this.deltaTangents = deltaTangents;
            }
        }
    }
}

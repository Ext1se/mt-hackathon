using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CC
{
    public class BlendshapeManager : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private List<string> blendshapeNames = new();
        [SerializeField, HideInInspector]
        private List<int> blendshapeIndices = new();

        private Dictionary<string, int> nameToIndex;
        private Dictionary<string, int> NameToIndex => nameToIndex ??= blendshapeNames.Zip(blendshapeIndices, (key, value) => new { key, value }).ToDictionary(x => x.key, x => x.value);

        private SkinnedMeshRenderer mesh;
        private SkinnedMeshRenderer Mesh => mesh = mesh != null ? mesh : GetComponent<SkinnedMeshRenderer>();

        public void parseBlendshapes()
        {
            blendshapeNames.Clear();
            blendshapeIndices.Clear();
            nameToIndex = null;

            if (Mesh.sharedMesh != null)
            {
                for (int i = 0; i < Mesh.sharedMesh.blendShapeCount; i++)
                {
                    string[] split = Mesh.sharedMesh.GetBlendShapeName(i).Split(".");
                    blendshapeNames.Add(split[^1]);
                    blendshapeIndices.Add(i);
                }
            }
        }

        public void setBlendshape(string name, float value)
        {
            if (NameToIndex.ContainsKey(name)) Mesh.SetBlendShapeWeight(NameToIndex[name], value * 100);
        }
    }
}
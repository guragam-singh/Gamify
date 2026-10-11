using UnityEngine;

namespace Gamify.Racing
{
    // Shared, batched meshes: no per-prop colliders, AI, or gameplay callbacks.
    public class RaceEnvironment : MonoBehaviour
    {
        public Material grass, foliage, trunk, white, red, blue, yellow;
        public Material foliageAccent;
        public Mesh cubeMesh, sphereMesh, cylinderMesh;
        public Transform treePrefab;
        public float builtLength;
        private readonly System.Collections.Generic.List<Mesh> _meshes = new System.Collections.Generic.List<Mesh>();

        public void RefreshLength(float endZ)
        {
            if (Mathf.Approximately(endZ, builtLength) && transform.childCount > 0) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
            { var child = transform.GetChild(i); child.gameObject.SetActive(false); if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject); }
            foreach (var mesh in _meshes)
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            _meshes.Clear();
            builtLength = endZ;
            var green = new System.Collections.Generic.List<CombineInstance>();
            var accent = new System.Collections.Generic.List<CombineInstance>();
            var meadow = new System.Collections.Generic.List<CombineInstance>();
            var shrubs = new System.Collections.Generic.List<CombineInstance>();
            var wood = new System.Collections.Generic.List<CombineInstance>();
            var whites = new System.Collections.Generic.List<CombineInstance>();
            var reds = new System.Collections.Generic.List<CombineInstance>();
            var blues = new System.Collections.Generic.List<CombineInstance>();
            var yellows = new System.Collections.Generic.List<CombineInstance>();
            Add(meadow, cubeMesh, new Vector3(0,-0.45f,(endZ-30f)*0.5f), new Vector3(240f,0.5f,endZ+130f));
            for (float z = -20f; z < endZ; z += 12f)
            {
                // Preserve player lane centres, with dedicated bot lanes beyond ±7m.
                foreach (float x in new[] {-9.5f,-6.5f,-3f,0f,3f,6.5f,9.5f})
                    Add(whites,cubeMesh,new Vector3(x,0.035f,z),new Vector3(0.12f,0.04f,4.8f));
            }
            for (float z = -25f; z < endZ; z += 24f)
                foreach (float side in new[] {-1f,1f})
                {
                    float x = side * (23f + Mathf.Sin(z * 0.12f) * 2f);
                    // Reuse the authored tree prefab meshes/materials, batched.
                    if (treePrefab != null)
                        foreach (var filter in treePrefab.GetComponentsInChildren<MeshFilter>())
                        {
                            var list = filter.name.Contains("Trunk") ? wood :
                                (foliageAccent != null && Mathf.Sin(z * 0.37f + side) > 0f ? accent : green);
                            float variation = 0.85f + 0.25f * (0.5f + 0.5f * Mathf.Sin(z * 0.73f + side));
                            Vector3 size = new Vector3(variation, variation + 0.08f * Mathf.Sin(z), variation);
                            list.Add(new CombineInstance { mesh=filter.sharedMesh, transform=Matrix4x4.TRS(new Vector3(x,0,z),Quaternion.Euler(0,z*7f,0),size) * filter.transform.localToWorldMatrix });
                        }
                    Add(green,sphereMesh,new Vector3(side*46f,-0.2f,z+8f),new Vector3(30f,12f,25f));
                    Add(shrubs,sphereMesh,new Vector3(side*21f,0.45f,z+10f),new Vector3(2.8f,1.5f,2.2f));
                    Add(shrubs,sphereMesh,new Vector3(side*22.5f,0.35f,z+11f),new Vector3(1.8f,1.1f,1.6f));
                    Add(wood,cylinderMesh,new Vector3(side*19f,2f,z+6f),new Vector3(0.12f,2f,0.12f));
                    Add((Mathf.RoundToInt(z/24f)%2==0)?blues:yellows,cubeMesh,new Vector3(side*19.6f,3.3f,z+6f),new Vector3(1.4f,0.8f,0.07f));
                }
            for (float z=-25f;z<endZ;z+=6f)
                foreach(float side in new[]{-1f,1f})
                {
                    Add((Mathf.RoundToInt((z+25f)/6f)%2==0)?whites:reds,cubeMesh,new Vector3(side*16.5f,0.12f,z),new Vector3(0.8f,0.22f,6f));
                    Add(blues,cubeMesh,new Vector3(side*17.4f,0.55f,z),new Vector3(0.3f,0.55f,6f));
                }
            foreach(float lineZ in new[]{0f,endZ-35f})
                for(int x=-13;x<14;x++) for(int row=0;row<2;row++)
                    Add(((x+row)&1)==0?whites:blues,cubeMesh,new Vector3(x,0.04f,lineZ+row),new Vector3(1f,0.04f,1f));
            Batch("MeadowAndTrees",green,foliage);
            if (accent.Count > 0) Batch("TreeColourVariation",accent,foliageAccent);
            Batch("Meadow",meadow,grass);
            Batch("Bushes",shrubs,foliage);
            Batch("TreeTrunksAndFlagPoles",wood,trunk);
            Batch("RoadMarkings",whites,white);
            Batch("CoralCurbs",reds,red);
            Batch("BlueRailsAndFlags",blues,blue);
            Batch("SunshineFlags",yellows,yellow);
        }

        private static void Add(System.Collections.Generic.List<CombineInstance> list, Mesh mesh, Vector3 position, Vector3 scale)
        {
            list.Add(new CombineInstance { mesh=mesh, transform=Matrix4x4.TRS(position,Quaternion.identity,scale) });
        }

        private void Batch(string label, System.Collections.Generic.List<CombineInstance> instances, Material material)
        {
            // Keep each batch within 16-bit vertex indexing; avoids huge per-prop draw counts.
            int start=0;
            while(start<instances.Count)
            {
                int count=0,vertices=0;
                while(start+count<instances.Count && vertices+instances[start+count].mesh.vertexCount<60000)
                { vertices+=instances[start+count].mesh.vertexCount; count++; }
                var mesh=new Mesh(); mesh.name=label;
                mesh.CombineMeshes(instances.GetRange(start,count).ToArray(),true,true);
                _meshes.Add(mesh);
                var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial=material;
                start+=count;
            }
        }
    }
}

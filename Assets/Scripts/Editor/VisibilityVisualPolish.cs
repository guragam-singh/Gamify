using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamify.Racing.Editor
{
    public static class VisibilityVisualPolish
    {
        public static string Apply()
        {
            if (Application.isPlaying) return "Stop Play Mode first";
            const string folder = "Assets/RacingArt";
            // Authored lightweight brick atlas. Cube front UV maps one tile over
            // a 2.45x3m panel; rectangular atlas makes near-square brick proportions.
            string texturePath = folder + "/StylisedBrick.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(256, 320, TextureFormat.RGBA32, true);
                texture.name = "StylisedBrick"; texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                for (int y = 0; y < 320; y++) for (int x = 0; x < 256; x++)
                {
                    int row = y / 32;
                    int brickX = (x + (row % 2) * 32) % 64;
                    bool mortar = y % 32 < 3 || brickX < 3;
                    float variation = 0.91f + 0.08f * Mathf.Sin(row * 12.4f + (x + (row % 2) * 32) / 64 * 7.3f);
                    Color color = mortar ? new Color(0.72f,0.76f,0.78f) : new Color(0.93f,0.85f,0.71f) * variation;
                    if (!mortar && y % 32 == 3) color *= 1.05f;
                    texture.SetPixel(x,y,color);
                }
                texture.Apply(true,false); AssetDatabase.CreateAsset(texture,texturePath);
            }
            string matPath = folder + "/StylisedBrickWall.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap",texture); mat.SetColor("_BaseColor",Color.white);
                mat.SetFloat("_Smoothness",0.15f); AssetDatabase.CreateAsset(mat,matPath);
            }
            var gm = Object.FindAnyObjectByType<GameManager>();
            int walls = 0;
            foreach (var cp in gm.checkpoints)
                foreach (var door in cp.doors) { door.wallRenderer.sharedMaterial = mat; walls++; }
            // Keep all wall highlight colours, meshes, transforms and colliders.
            var env = gm.raceDirector.raceEnvironment;
            env.builtLength = 0;
            env.RefreshLength(gm.checkpoints[gm.checkpoints.Count - 1].CheckpointZ + 45f);
            int index = 0;
            foreach (var filter in env.GetComponentsInChildren<MeshFilter>())
            {
                string path = folder + "/LivingEnvironment_" + index++ + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) AssetDatabase.CreateAsset(filter.sharedMesh,path);
                else { EditorUtility.CopySerialized(filter.sharedMesh,saved); filter.sharedMesh=saved; }
            }
            EditorUtility.SetDirty(env); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            return walls + " existing answer wall surfaces textured; varied trees, bushes and separate meadow material saved.";
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamify.Racing.Editor
{
    // Targeted in-place upgrade, deliberately does not call SceneBuilder.
    public static class ArcadeTrackUpgrade
    {
        private const string Folder = "Assets/RacingArt";
        private static Material Mat(string name, Color color)
        {
            string path=Folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",0.25f); mat.enableInstancing=true;
            EditorUtility.SetDirty(mat); return mat;
        }
        private static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        public static string Apply()
        {
            if(Application.isPlaying) return "Stop Play Mode first.";
            var gm=Object.FindAnyObjectByType<GameManager>();var race=gm.raceDirector;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","RacingArt");
            var asphalt=Mat("Asphalt",new Color(0.12f,0.19f,0.26f));
            var green=Mat("Meadow",new Color(0.27f,0.65f,0.20f));
            var leaves=Mat("LeafGreen",new Color(0.23f,0.58f,0.16f));
            var wood=Mat("WarmWood",new Color(0.48f,0.26f,0.12f));
            var white=Mat("Ivory",new Color(0.96f,0.98f,1f));
            var coral=Mat("Coral",new Color(1f,0.20f,0.16f));
            var blue=Mat("SkyBlue",new Color(0.06f,0.40f,0.92f));
            var gold=Mat("Sunshine",new Color(1f,0.73f,0.03f));
            var orange=Mat("Tangerine",new Color(1f,0.38f,0.04f));
            var rubber=Mat("Rubber",new Color(0.035f,0.055f,0.075f));
            var glass=Mat("GlassBlue",new Color(0.40f,0.78f,0.94f));
            race.checkpointDistance=275f; gm.checkpointSpacing=275f;
            race.botLaneXPositions=new[]{-11f,-8f,8f,11f}; race.obstacleMaterial=orange;
            float end=45f+(gm.questionManager.Questions.Count>0?gm.questionManager.Questions.Count-1:7)*275f+45f;
            var road=race.track.Find("RoadSurface");road.localScale=new Vector3(28f,0.2f,end+30f);road.position=new Vector3(0,-0.1f,(end-30f)*0.5f);road.GetComponent<Renderer>().sharedMaterial=asphalt;
            foreach(string name in new[]{"LeftShoulder","RightShoulder"})
            {
                var t=race.track.Find(name);t.position=new Vector3(name.StartsWith("Left")?-14.5f:14.5f,0.1f,(end-30f)*0.5f);
                t.localScale=new Vector3(1f,0.2f,end+30f);t.GetComponent<Renderer>().sharedMaterial=coral;
            }
            foreach(var t in race.track.GetComponentsInChildren<Transform>())
                if(t.name=="Stripe"||t.name.StartsWith("Pillar"))t.gameObject.SetActive(false);

            // Keep all four functional gates and lane centres exactly as authored.
            foreach(var cp in gm.checkpoints)
            {
                cp.transform.position=new Vector3(0,0,45f+cp.checkpointIndex*275f);
                cp.transform.Find("ArchBeam").GetComponent<Renderer>().sharedMaterial=blue;
                foreach(var d in cp.doors)
                {
                    d.frameDefaultColor=new Color(0.05f,0.36f,0.80f);d.frameHighlightColor=new Color(1f,0.83f,0.16f);
                    d.wallDefaultColor=new Color(0.94f,0.98f,1f);d.wallHighlightColor=new Color(1f,0.90f,0.48f);
                    d.textDefaultColor=new Color(0.025f,0.07f,0.14f);d.textHighlightColor=new Color(0.025f,0.07f,0.14f);
                    d.wallRenderer.sharedMaterial=white;
                    foreach(var r in d.transform.Find("Frame").GetComponentsInChildren<Renderer>())r.sharedMaterial=blue;
                    d.optionText.color=d.textDefaultColor;d.badgeText.color=d.textDefaultColor;
                    d.indicatorArrow.GetComponent<Renderer>().sharedMaterial=gold;
                    EditorUtility.SetDirty(d);
                }
                // Non-colliding arcade header above existing beam, no text/UI occlusion.
                if(cp.transform.Find("ArcadeHeader")==null)
                {
                    var header=new GameObject("ArcadeHeader").transform;header.SetParent(cp.transform,false);
                    Shape("GoldRoof",PrimitiveType.Cube,header,new Vector3(0,4.8f,0),new Vector3(13.4f,0.35f,1f),gold);
                    foreach(float x in new[]{-6.3f,6.3f})Shape("RoundCap",PrimitiveType.Sphere,header,new Vector3(x,4.8f,0),Vector3.one*0.65f,coral);
                }
            }
            Material[] carColors={coral,blue,gold,orange};
            PaintCar(gm.carController.carModel,blue,glass,rubber,white);
            for(int i=0;i<race.bots.Length;i++)
            {
                PaintCar(race.bots[i].model,carColors[i],glass,rubber,white);
                var p=race.bots[i].transform.position;p.x=race.botLaneXPositions[i];race.bots[i].transform.position=p;
            }
            // Reusable tree prefab, then combine its mesh instances at runtime/editor.
            string prefabPath=Folder+"/ToyTree.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)==null)
            {
                var tree=new GameObject("ToyTree");
                Shape("Trunk",PrimitiveType.Cylinder,tree.transform,new Vector3(0,1.25f,0),new Vector3(0.45f,1.25f,0.45f),wood);
                Shape("Crown",PrimitiveType.Sphere,tree.transform,new Vector3(0,3.6f,0),new Vector3(3f,3.8f,3f),leaves);
                PrefabUtility.SaveAsPrefabAsset(tree,prefabPath);Object.DestroyImmediate(tree);
            }
            var env=race.raceEnvironment;
            if(env==null){var root=new GameObject("ArcadeEnvironment");env=root.AddComponent<RaceEnvironment>();race.raceEnvironment=env;}
            env.grass=green;env.foliage=leaves;env.trunk=wood;env.white=white;env.red=coral;env.blue=blue;env.yellow=gold;
            env.treePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).transform;
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);env.cubeMesh=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            primitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);env.sphereMesh=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            primitive=GameObject.CreatePrimitive(PrimitiveType.Cylinder);env.cylinderMesh=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            env.builtLength=0;env.RefreshLength(end);
            // Save generated batched meshes as assets so the editor scene persists.
            int meshIndex=0;
            foreach(var filter in env.GetComponentsInChildren<MeshFilter>())
            {
                string path=Folder+"/EnvironmentBatch_"+(meshIndex++)+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(filter.sharedMesh,path);
                else { EditorUtility.CopySerialized(filter.sharedMesh,existing);filter.sharedMesh=existing; }
            }
            var camera=gm.carController.followCamera;camera.clearFlags=CameraClearFlags.Skybox;
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCADE - FREE Racing Car/Skybox/Day/Day Skybox.mat");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.62f,0.80f,1f);RenderSettings.ambientEquatorColor=new Color(0.50f,0.62f,0.69f);RenderSettings.ambientGroundColor=new Color(0.24f,0.34f,0.20f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(0.55f,0.81f,0.95f);RenderSettings.fogStartDistance=180;RenderSettings.fogEndDistance=500;
            var light=GameObject.Find("Directional Light").GetComponent<Light>();light.color=new Color(1f,0.94f,0.83f);light.intensity=1.15f;light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(45f,-25f,0f);RenderSettings.sun=light;
            var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if(data!=null){data.renderShadows=true;data.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;}
            EditorUtility.SetDirty(race);EditorUtility.SetDirty(gm);EditorUtility.SetDirty(env);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
            return "28m road, 275m question spacing, colourful arcade materials, existing day skybox, reusable tree and batched environment saved.";
        }
        private static void PaintCar(Transform model,Material body,Material glass,Material wheel,Material light)
        {
            foreach(var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterial=r.name.Contains("Wheel")?wheel:r.name.Contains("Cockpit")?glass:r.name.Contains("Headlight")?light:body;
        }
    }
}

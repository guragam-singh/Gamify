using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Gamify.Racing.Editor
{
    public static class MasterRaceUpgrade
    {
        private const string Folder = "Assets/RacingArt";
        private static Material Converted(Material original)
        {
            string path = Folder + "/Integrated_" + original.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", original.mainTexture);
                mat.SetColor("_BaseColor", original.color);
                mat.SetFloat("_Smoothness", 0.3f); mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void ConvertRenderers(GameObject go)
        {
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = Converted(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        private static void Vehicle(Transform original, string prefabPath, out Transform model, out Transform[] wheels)
        {
            Transform parent = original.parent;
            original.gameObject.SetActive(false); // Reversible, retains old model and references in hierarchy.
            var root = new GameObject("ArcadeCarVisual").transform;
            root.SetParent(parent, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(root, false); go.transform.localScale = Vector3.one * 0.72f;
            ConvertRenderers(go);
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("Wheel")) list.Add(t);
            model = root; wheels = list.ToArray();
        }

        private static TextMeshProUGUI Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));go.transform.SetParent(parent,false);
            var text=go.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;
            var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            return text;
        }

        private static void StatsRect(TextMeshProUGUI text, Vector2 position, Vector2 size)
        {
            var rect=text.rectTransform;rect.SetParent(text.GetComponentInParent<GameUI>().hudRoot.transform,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            text.fontSize=28;text.color=Color.white;text.alignment=TextAlignmentOptions.Left;
        }

        public static string Apply()
        {
            if(Application.isPlaying)return "Stop Play Mode first";
            var gm=Object.FindAnyObjectByType<GameManager>();var race=gm.raceDirector;var ui=gm.gameUI;
            race.checkpointDistance=650f;gm.checkpointSpacing=650f;
            float end=45f+7*650f+45f;
            foreach(var cp in gm.checkpoints){var p=cp.transform.position;p.z=45f+cp.checkpointIndex*650f;cp.transform.position=p;}
            foreach(string surface in new[]{"RoadSurface","LeftShoulder","RightShoulder"})
            {
                var t=race.track.Find(surface);var p=t.position;var scale=t.localScale;
                p.z=(end-30f)*0.5f;scale.z=end+30f;
                if(surface=="RoadSurface")scale.x=32f;
                else p.x=surface.StartsWith("Left")?-16.5f:16.5f;
                t.position=p;t.localScale=scale;
            }
            // Compatible visual-only car variants: keep the root controller and bounds.
            string carFolder="Assets/ARCADE - FREE Racing Car/Prefabs (Meshes Only)/";
            Transform model;Transform[] wheels;
            if(gm.carController.carModel.name!="ArcadeCarVisual")
            {
                Vehicle(gm.carController.carModel,carFolder+"Free Racing Car Blue Variant.prefab",out model,out wheels);
                gm.carController.carModel=model;gm.carController.wheels=wheels;
            }
            string[] variants={"Red","Purple","Gray","Blue"};
            for(int i=0;i<race.bots.Length;i++)
                if(race.bots[i].model.name!="ArcadeCarVisual")
                {Vehicle(race.bots[i].model,carFolder+"Free Racing Car "+variants[i]+" Variant.prefab",out model,out wheels);race.bots[i].model=model;race.bots[i].wheels=wheels;}
            var scenery=GameObject.Find("SelectedAssetScenery");
            if(scenery==null)
            {
                scenery=new GameObject("SelectedAssetScenery");
                string bed="Assets/BEDRILL/Track_Environment_Free/Prefabs/";
                // Few meaningful props, placed beyond driving/camera/answer corridors.
                for(int i=0;i<16;i++)
                {
                    string name=i%4==0?"Tribune_free_obs":i%4==1?"Flag_free_obs":i%4==2?"Sign_free_obs":"Tire_free_obs";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(bed+name+".prefab");
                    var prop=(GameObject)PrefabUtility.InstantiatePrefab(prefab);prop.transform.SetParent(scenery.transform);
                    prop.transform.position=new Vector3(i%2==0?-27f:27f,0f,70f+i*260f);
                    prop.transform.rotation=Quaternion.Euler(0,i%2==0?90f:-90f,0);
                    foreach(var c in prop.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                    ConvertRenderers(prop);
                }
            }
            race.raceEnvironment.builtLength=0;race.raceEnvironment.RefreshLength(end);
            int index=0;
            foreach(var filter in race.raceEnvironment.GetComponentsInChildren<MeshFilter>())
            {
                string path=Folder+"/MasterEnvironment_"+(index++)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null)AssetDatabase.CreateAsset(filter.sharedMesh,path);
                else {EditorUtility.CopySerialized(filter.sharedMesh,saved);filter.sharedMesh=saved;}
            }
            // Existing UI, same question/answer controls; fix the overflowing StatsBar
            // anchors that placed lives/timer outside the canvas on the baseline.
            StatsRect(ui.livesText,new Vector2(30,-125),new Vector2(270,46));
            StatsRect(ui.scoreText,new Vector2(30,-175),new Vector2(270,46));
            StatsRect(ui.timerText,new Vector2(1550,-125),new Vector2(340,46));
            ui.timerText.alignment=TextAlignmentOptions.Right;
            ui.questionBodyText.fontSize=32;ui.questionBodyText.color=Color.white;
            ui.questionBodyText.GetComponentInParent<Image>().color=new Color(0.035f,0.10f,0.20f,0.94f);
            ui.explanationBodyText.fontSize=30;ui.explanationTitleText.color=new Color(1f,0.75f,0.24f);
            ui.resultsTitleText.color=new Color(0.23f,0.95f,0.72f);
            if(ui.speedText==null)
            {
                ui.speedText=Label(ui.hudRoot.transform,"SpeedText",new Vector2(30,-230),new Vector2(280,70),48,ui.scoreText.font);
                ui.raceStatusText=Label(ui.hudRoot.transform,"RaceStatus",new Vector2(470,-265),new Vector2(980,44),24,ui.scoreText.font);
                ui.raceStatusText.alignment=TextAlignmentOptions.Center;
                var bg=new GameObject("CourseProgress",typeof(RectTransform),typeof(Image));bg.transform.SetParent(ui.hudRoot.transform,false);
                var rect=bg.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0.5f,0);rect.pivot=new Vector2(0.5f,0);rect.anchoredPosition=new Vector2(0,125);rect.sizeDelta=new Vector2(1100,12);
                bg.GetComponent<Image>().color=new Color(0.03f,0.09f,0.16f,0.85f);bg.GetComponent<Image>().raycastTarget=false;
                var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(bg.transform,false);
                var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.sizeDelta=Vector2.zero;
                var image=fill.GetComponent<Image>();var texture=Texture2D.whiteTexture;
                string spritePath=Folder+"/ProgressSprite.asset";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if(sprite==null){sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(0.5f,0.5f));AssetDatabase.CreateAsset(sprite,spritePath);}
                image.sprite=sprite;image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;
                image.color=new Color(0.15f,0.85f,0.80f);image.raycastTarget=false;ui.courseProgressFill=image;
            }
            EditorUtility.SetDirty(gm);EditorUtility.SetDirty(race);EditorUtility.SetDirty(ui);EditorUtility.SetDirty(gm.carController);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
            return "32m road, 650m spacing, selected visual car variants and BEDRILL scenery, readable stats/speed/progress UI saved.";
        }
    }
}

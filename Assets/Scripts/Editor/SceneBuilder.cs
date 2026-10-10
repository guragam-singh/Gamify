using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Gamify.Racing.Editor
{
    public static class SceneBuilder
    {
        private static Shader GetUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit") 
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default");
        }

        private static Material CreateGrayscaleMat(string name, Color color)
        {
            Material mat = new Material(GetUnlitShader());
            mat.name = name;
            mat.color = color;
            return mat;
        }

        private static void ApplyGrayscaleMatToChildren(GameObject go, Material mat)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
            }
        }

        [MenuItem("Tools/Gamify/Build Racer Scene")]
        public static void BuildScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Undo.RegisterFullObjectHierarchyUndo(activeScene.GetRootGameObjects().Length > 0 ? activeScene.GetRootGameObjects()[0] : null, "Build Racer Scene");

            // Setup Camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.orthographic = false;
            cam.fieldOfView = 56f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.06f);
            cam.transform.position = new Vector3(-1.5f, 3.4f, -7.2f);
            cam.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

            // Setup Directional Light
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light dirLight = null;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional) { dirLight = l; break; }
            }
            if (dirLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.color = Color.white;
            dirLight.intensity = 1.15f;
            dirLight.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            // Materials
            Material roadMat = CreateGrayscaleMat("RoadMat", new Color(0.12f, 0.12f, 0.12f));
            Material shoulderMat = CreateGrayscaleMat("ShoulderMat", new Color(0.04f, 0.04f, 0.04f));
            Material stripeMat = CreateGrayscaleMat("StripeMat", new Color(0.92f, 0.92f, 0.92f));
            Material headlightMat = CreateGrayscaleMat("HeadlightMat", Color.white);
            Material frameMat = CreateGrayscaleMat("FrameMat", new Color(0.18f, 0.18f, 0.18f));
            Material wallMat = CreateGrayscaleMat("WallMat", new Color(0.48f, 0.48f, 0.48f));

            // Load Grayscale Environment Material
            Material envGrayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BEDRILL/Track_Environment_Free/Materials/Environment_Free_Grayscale.mat");
            if (envGrayMat == null)
            {
                envGrayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BEDRILL/Track_Environment_Free/Materials/Environment_Free_Material.mat");
            }

            // Prefabs from Asset Packs
            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - FREE Racing Car/Prefabs (With Colliders)/Free Racing Car Gray Variant.prefab");
            GameObject blueCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - FREE Racing Car/Prefabs (With Colliders)/Free Racing Car Blue Variant.prefab");
            GameObject redCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - FREE Racing Car/Prefabs (With Colliders)/Free Racing Car Red Variant.prefab");
            GameObject purpleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ARCADE - FREE Racing Car/Prefabs (With Colliders)/Free Racing Car Purple Variant.prefab");

            GameObject archPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Arch_for_banner_free_obs.prefab");
            GameObject finishArchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Arch_banner_finish_free_obs.prefab");
            GameObject barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Barrier_free_obs.prefab");
            GameObject polePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Pole_light_free_obs.prefab");
            GameObject tribunePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Tribune_free_obs.prefab");
            GameObject trafficLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Traffic_light_free_obs.prefab");
            GameObject tirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Tire_free_obs.prefab");
            GameObject conePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BEDRILL/Track_Environment_Free/Prefabs/Cone_free_obs.prefab");

            // Clean old objects
            string[] cleanupNames = { "Track", "Car", "Checkpoints", "GameManager", "Canvas", "EventSystem", "Obstacles", "BotCars" };
            foreach (var n in cleanupNames)
            {
                var found = GameObject.Find(n);
                if (found != null) Object.DestroyImmediate(found);
            }

            // 1. Build Track & Scenery (Extended to 760 units for 120 spacing)
            GameObject trackRoot = new GameObject("Track");

            // Asphalt Road (Width: 13, Length: 760, centered at Z = 355)
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "RoadSurface";
            road.transform.SetParent(trackRoot.transform);
            road.transform.position = new Vector3(0f, -0.1f, 355f);
            road.transform.localScale = new Vector3(13f, 0.2f, 760f);
            road.GetComponent<MeshRenderer>().material = roadMat;

            // Shoulders
            GameObject leftShoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftShoulder.name = "LeftShoulder";
            leftShoulder.transform.SetParent(trackRoot.transform);
            leftShoulder.transform.position = new Vector3(-7f, 0.12f, 355f);
            leftShoulder.transform.localScale = new Vector3(1.2f, 0.35f, 760f);
            leftShoulder.GetComponent<MeshRenderer>().material = shoulderMat;

            GameObject rightShoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightShoulder.name = "RightShoulder";
            rightShoulder.transform.SetParent(trackRoot.transform);
            rightShoulder.transform.position = new Vector3(7f, 0.12f, 355f);
            rightShoulder.transform.localScale = new Vector3(1.2f, 0.35f, 760f);
            rightShoulder.GetComponent<MeshRenderer>().material = shoulderMat;

            // Lane Divider Dashes
            float[] dividerX = new float[] { -3f, 0f, 3f };
            for (float z = -10f; z < 720f; z += 12f)
            {
                foreach (float x in dividerX)
                {
                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stripe.name = "Stripe";
                    stripe.transform.SetParent(trackRoot.transform);
                    stripe.transform.position = new Vector3(x, 0.02f, z);
                    stripe.transform.localScale = new Vector3(0.2f, 0.05f, 5.5f);
                    stripe.GetComponent<MeshRenderer>().material = stripeMat;
                    Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
            }

            // Roadside Barriers
            if (barrierPrefab != null)
            {
                for (float z = -15f; z < 725f; z += 3.2f)
                {
                    // Left Barrier
                    GameObject bL = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab);
                    bL.transform.SetParent(trackRoot.transform);
                    bL.transform.position = new Vector3(-7.2f, 0.1f, z);
                    bL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    bL.transform.localScale = new Vector3(1f, 0.85f, 1f);
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(bL, envGrayMat);

                    // Right Barrier
                    GameObject bR = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab);
                    bR.transform.SetParent(trackRoot.transform);
                    bR.transform.position = new Vector3(7.2f, 0.1f, z);
                    bR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    bR.transform.localScale = new Vector3(1f, 0.85f, 1f);
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(bR, envGrayMat);
                }
            }

            // Streetlight Poles
            if (polePrefab != null)
            {
                for (float z = 8f; z < 710f; z += 24f)
                {
                    // Left pole
                    GameObject pL = (GameObject)PrefabUtility.InstantiatePrefab(polePrefab);
                    pL.transform.SetParent(trackRoot.transform);
                    pL.transform.position = new Vector3(-8.2f, 0f, z);
                    pL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    pL.transform.localScale = Vector3.one * 0.8f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(pL, envGrayMat);

                    // Right pole
                    GameObject pR = (GameObject)PrefabUtility.InstantiatePrefab(polePrefab);
                    pR.transform.SetParent(trackRoot.transform);
                    pR.transform.position = new Vector3(8.2f, 0f, z + 12f);
                    pR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    pR.transform.localScale = Vector3.one * 0.8f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(pR, envGrayMat);
                }
            }

            // Spectator Tribunes (distributed along the extended track)
            if (tribunePrefab != null)
            {
                float[] tribuneZs = new float[] { 140f, 260f, 380f, 500f, 620f };
                foreach (float tz in tribuneZs)
                {
                    GameObject tL = (GameObject)PrefabUtility.InstantiatePrefab(tribunePrefab);
                    tL.transform.SetParent(trackRoot.transform);
                    tL.transform.position = new Vector3(-13.5f, 0f, tz);
                    tL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    tL.transform.localScale = Vector3.one * 0.9f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(tL, envGrayMat);

                    GameObject tR = (GameObject)PrefabUtility.InstantiatePrefab(tribunePrefab);
                    tR.transform.SetParent(trackRoot.transform);
                    tR.transform.position = new Vector3(13.5f, 0f, tz);
                    tR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    tR.transform.localScale = Vector3.one * 0.9f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(tR, envGrayMat);
                }
            }

            // Finish Line Arch (at Z = 670)
            if (finishArchPrefab != null)
            {
                GameObject finishArch = (GameObject)PrefabUtility.InstantiatePrefab(finishArchPrefab);
                finishArch.name = "FinishLineArch";
                finishArch.transform.SetParent(trackRoot.transform);
                finishArch.transform.position = new Vector3(0f, 4.5f, 670f);
                finishArch.transform.localScale = new Vector3(1.85f, 1.85f, 1.85f);
                if (envGrayMat != null) ApplyGrayscaleMatToChildren(finishArch, envGrayMat);
            }

            // 2. Build Player Car
            GameObject carGo = new GameObject("Car");
            carGo.transform.position = new Vector3(-1.5f, 0.25f, 0f);

            // Rigidbody and trigger collider for responsive physics detection
            var carRb = carGo.AddComponent<Rigidbody>();
            carRb.isKinematic = true;
            var carCol = carGo.AddComponent<BoxCollider>();
            carCol.isTrigger = true;
            carCol.size = new Vector3(2.2f, 1.4f, 4.4f);
            carCol.center = new Vector3(0f, 0.7f, 0.5f);

            var carController = carGo.AddComponent<CarController>();

            GameObject carModel = new GameObject("Model");
            carModel.transform.SetParent(carGo.transform);
            carModel.transform.localPosition = Vector3.zero;

            Transform frontLeft = null;
            Transform frontRight = null;
            List<Transform> allWheels = new List<Transform>();

            if (carPrefab != null)
            {
                GameObject carInstance = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                carInstance.name = "AFRC_CarModel";
                carInstance.transform.SetParent(carModel.transform);
                carInstance.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                carInstance.transform.localRotation = Quaternion.identity;
                carInstance.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

                frontLeft = carInstance.transform.Find("Wheels/Meshes/FrontLeftWheel");
                frontRight = carInstance.transform.Find("Wheels/Meshes/FrontRightWheel");
                Transform rearLeft = carInstance.transform.Find("Wheels/Meshes/RearLeftWheel");
                Transform rearRight = carInstance.transform.Find("Wheels/Meshes/RearRightWheel");

                if (frontLeft != null) allWheels.Add(frontLeft);
                if (frontRight != null) allWheels.Add(frontRight);
                if (rearLeft != null) allWheels.Add(rearLeft);
                if (rearRight != null) allWheels.Add(rearRight);

                // Add Headlights
                GameObject hl1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hl1.name = "Headlight_L";
                hl1.transform.SetParent(carInstance.transform);
                hl1.transform.localPosition = new Vector3(-0.62f, 0.38f, 2.38f);
                hl1.transform.localScale = new Vector3(0.28f, 0.18f, 0.1f);
                hl1.GetComponent<MeshRenderer>().material = headlightMat;
                Object.DestroyImmediate(hl1.GetComponent<Collider>());

                GameObject hl2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hl2.name = "Headlight_R";
                hl2.transform.SetParent(carInstance.transform);
                hl2.transform.localPosition = new Vector3(0.62f, 0.38f, 2.38f);
                hl2.transform.localScale = new Vector3(0.28f, 0.18f, 0.1f);
                hl2.GetComponent<MeshRenderer>().material = headlightMat;
                Object.DestroyImmediate(hl2.GetComponent<Collider>());
            }

            carController.carModel = carModel.transform;
            carController.wheels = allWheels.ToArray();
            carController.frontLeftWheel = frontLeft;
            carController.frontRightWheel = frontRight;
            carController.followCamera = cam;

            // 3. Build Checkpoints (5 checkpoints at 90, 210, 330, 450, 570 — 120 units spacing)
            GameObject cpRoot = new GameObject("Checkpoints");
            List<Checkpoint> checkpointList = new List<Checkpoint>();

            float[] cpDistances = new float[] { 90f, 210f, 330f, 450f, 570f };
            float[] laneXs = new float[] { -4.5f, -1.5f, 1.5f, 4.5f };

            for (int c = 0; c < cpDistances.Length; c++)
            {
                float cpZ = cpDistances[c];
                GameObject cpGo = new GameObject($"Checkpoint_{c + 1}");
                cpGo.transform.SetParent(cpRoot.transform);
                cpGo.transform.position = new Vector3(0f, 0f, cpZ);

                var checkpointComp = cpGo.AddComponent<Checkpoint>();
                checkpointComp.checkpointIndex = c;
                checkpointComp.doors = new DoorController[4];

                // Overhead Archway
                if (archPrefab != null)
                {
                    GameObject arch = (GameObject)PrefabUtility.InstantiatePrefab(archPrefab);
                    arch.name = "OverheadArch";
                    arch.transform.SetParent(cpGo.transform);
                    arch.transform.localPosition = new Vector3(0f, 0f, 0f);
                    arch.transform.localRotation = Quaternion.identity;
                    arch.transform.localScale = new Vector3(0.92f, 1f, 1f);
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(arch, envGrayMat);
                }
                else
                {
                    GameObject archBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    archBeam.name = "ArchBeam";
                    archBeam.transform.SetParent(cpGo.transform);
                    archBeam.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                    archBeam.transform.localScale = new Vector3(13.5f, 0.6f, 0.8f);
                    archBeam.GetComponent<MeshRenderer>().material = frameMat;
                }

                // Traffic Light on Arch
                if (trafficLightPrefab != null)
                {
                    GameObject tlight = (GameObject)PrefabUtility.InstantiatePrefab(trafficLightPrefab);
                    tlight.name = "TrafficLight";
                    tlight.transform.SetParent(cpGo.transform);
                    tlight.transform.localPosition = new Vector3(0f, 5.2f, -0.4f);
                    tlight.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    tlight.transform.localScale = Vector3.one * 0.9f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(tlight, envGrayMat);
                }

                // Roadside Tire Stack & Cones near checkpoint
                if (tirePrefab != null)
                {
                    GameObject tireL = (GameObject)PrefabUtility.InstantiatePrefab(tirePrefab);
                    tireL.transform.SetParent(cpGo.transform);
                    tireL.transform.localPosition = new Vector3(-6.2f, 0f, -8f);
                    tireL.transform.localScale = Vector3.one * 0.85f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(tireL, envGrayMat);

                    GameObject tireR = (GameObject)PrefabUtility.InstantiatePrefab(tirePrefab);
                    tireR.transform.SetParent(cpGo.transform);
                    tireR.transform.localPosition = new Vector3(6.2f, 0f, -8f);
                    tireR.transform.localScale = Vector3.one * 0.85f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(tireR, envGrayMat);
                }

                if (conePrefab != null)
                {
                    GameObject coneL = (GameObject)PrefabUtility.InstantiatePrefab(conePrefab);
                    coneL.transform.SetParent(cpGo.transform);
                    coneL.transform.localPosition = new Vector3(-6.0f, 0f, -6f);
                    coneL.transform.localScale = Vector3.one * 0.9f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(coneL, envGrayMat);

                    GameObject coneR = (GameObject)PrefabUtility.InstantiatePrefab(conePrefab);
                    coneR.transform.SetParent(cpGo.transform);
                    coneR.transform.localPosition = new Vector3(6.0f, 0f, -6f);
                    coneR.transform.localScale = Vector3.one * 0.9f;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(coneR, envGrayMat);
                }

                // 4 Answer Doors
                for (int d = 0; d < 4; d++)
                {
                    float doorX = laneXs[d];
                    GameObject doorGo = new GameObject($"Door_{d}");
                    doorGo.transform.SetParent(cpGo.transform);
                    doorGo.transform.localPosition = new Vector3(doorX, 0f, 0f);

                    var doorCtrl = doorGo.AddComponent<DoorController>();

                    // Door Archway Frame
                    GameObject frameRoot = new GameObject("Frame");
                    frameRoot.transform.SetParent(doorGo.transform);
                    frameRoot.transform.localPosition = Vector3.zero;

                    GameObject fLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fLeft.transform.SetParent(frameRoot.transform);
                    fLeft.transform.localPosition = new Vector3(-1.35f, 1.7f, 0f);
                    fLeft.transform.localScale = new Vector3(0.22f, 3.4f, 0.6f);
                    fLeft.GetComponent<MeshRenderer>().material = frameMat;

                    GameObject fRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fRight.transform.SetParent(frameRoot.transform);
                    fRight.transform.localPosition = new Vector3(1.35f, 1.7f, 0f);
                    fRight.transform.localScale = new Vector3(0.22f, 3.4f, 0.6f);
                    fRight.GetComponent<MeshRenderer>().material = frameMat;

                    GameObject fTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fTop.transform.SetParent(frameRoot.transform);
                    fTop.transform.localPosition = new Vector3(0f, 3.4f, 0f);
                    fTop.transform.localScale = new Vector3(2.9f, 0.28f, 0.6f);
                    fTop.GetComponent<MeshRenderer>().material = frameMat;

                    // Solid Breakable Wall Slab
                    GameObject wallSolid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSolid.name = "WallSolid";
                    wallSolid.transform.SetParent(doorGo.transform);
                    wallSolid.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                    wallSolid.transform.localScale = new Vector3(2.45f, 3.0f, 0.35f);
                    wallSolid.GetComponent<MeshRenderer>().material = wallMat;

                    // Indicator Arrow
                    GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arrow.name = "IndicatorArrow";
                    arrow.transform.SetParent(doorGo.transform);
                    arrow.transform.localPosition = new Vector3(0f, 4.0f, 0f);
                    arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    arrow.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    arrow.GetComponent<MeshRenderer>().material = headlightMat;
                    arrow.SetActive(false);

                    // Badge Text 3D [A]
                    GameObject badgeGo = new GameObject("BadgeText");
                    badgeGo.transform.SetParent(doorGo.transform);
                    badgeGo.transform.localPosition = new Vector3(0f, 2.7f, -0.22f);
                    var badgeTmp = badgeGo.AddComponent<TextMeshPro>();
                    badgeTmp.text = $"[{(char)('A' + d)}]";
                    badgeTmp.fontSize = 6.5f;
                    badgeTmp.alignment = TextAlignmentOptions.Center;
                    badgeTmp.color = Color.white;
                    badgeTmp.rectTransform.sizeDelta = new Vector2(2.4f, 1.0f);

                    // Option Text 3D
                    GameObject optGo = new GameObject("OptionText");
                    optGo.transform.SetParent(doorGo.transform);
                    optGo.transform.localPosition = new Vector3(0f, 1.4f, -0.22f);
                    var optTmp = optGo.AddComponent<TextMeshPro>();
                    optTmp.text = $"Option {d + 1}";
                    optTmp.fontSize = 3.6f;
                    optTmp.enableWordWrapping = true;
                    optTmp.alignment = TextAlignmentOptions.Center;
                    optTmp.color = new Color(0.7f, 0.7f, 0.7f);
                    optTmp.rectTransform.sizeDelta = new Vector2(2.3f, 2.0f);

                    // Connect door controller
                    doorCtrl.doorIndex = d;
                    doorCtrl.frameRenderer = fTop.GetComponent<MeshRenderer>();
                    doorCtrl.wallRenderer = wallSolid.GetComponent<MeshRenderer>();
                    doorCtrl.wallSolidObject = wallSolid;
                    doorCtrl.indicatorArrow = arrow;
                    doorCtrl.badgeText = badgeTmp;
                    doorCtrl.optionText = optTmp;

                    checkpointComp.doors[d] = doorCtrl;
                }

                checkpointList.Add(checkpointComp);
            }

            // 4. Build Obstacles (placed between checkpoints with always clear lanes)
            GameObject obsRoot = new GameObject("Obstacles");
            List<Obstacle> obstacleList = new List<Obstacle>();

            // Helper struct for obstacle definition
            var obstaclePlacements = new (float x, float z, ObstacleType type)[]
            {
                // Segment 1: Start (0) to CP1 (90)
                (-1.5f, 32f, ObstacleType.Cone),
                (1.5f, 52f, ObstacleType.TireStack),
                (4.5f, 52f, ObstacleType.TireStack),
                (-4.5f, 68f, ObstacleType.Cone),

                // Segment 2: CP1 (90) to CP2 (210)
                (4.5f, 125f, ObstacleType.Cone),
                (-1.5f, 150f, ObstacleType.TireStack),
                (1.5f, 150f, ObstacleType.TireStack),
                (-4.5f, 175f, ObstacleType.Barrier),
                (1.5f, 190f, ObstacleType.Cone),

                // Segment 3: CP2 (210) to CP3 (330)
                (-4.5f, 240f, ObstacleType.TireStack),
                (-1.5f, 240f, ObstacleType.TireStack),
                (1.5f, 265f, ObstacleType.Cone),
                (4.5f, 285f, ObstacleType.Barrier),
                (-1.5f, 305f, ObstacleType.TireStack),

                // Segment 4: CP3 (330) to CP4 (450)
                (-1.5f, 360f, ObstacleType.Cone),
                (1.5f, 385f, ObstacleType.TireStack),
                (4.5f, 385f, ObstacleType.TireStack),
                (-4.5f, 408f, ObstacleType.Barrier),
                (-1.5f, 425f, ObstacleType.Cone),
                (1.5f, 425f, ObstacleType.Cone),

                // Segment 5: CP4 (450) to CP5 (570)
                (-4.5f, 485f, ObstacleType.TireStack),
                (1.5f, 508f, ObstacleType.Cone),
                (4.5f, 508f, ObstacleType.Cone),
                (-1.5f, 528f, ObstacleType.Barrier),
                (-4.5f, 548f, ObstacleType.Cone)
            };

            foreach (var op in obstaclePlacements)
            {
                GameObject prefabToUse = conePrefab;
                Vector3 scale = Vector3.one;

                if (op.type == ObstacleType.Cone)
                {
                    prefabToUse = conePrefab;
                    scale = Vector3.one * 0.95f;
                }
                else if (op.type == ObstacleType.TireStack)
                {
                    prefabToUse = tirePrefab;
                    scale = Vector3.one * 0.9f;
                }
                else if (op.type == ObstacleType.Barrier)
                {
                    prefabToUse = barrierPrefab;
                    scale = new Vector3(0.9f, 0.85f, 0.9f);
                }

                if (prefabToUse != null)
                {
                    GameObject obsInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabToUse);
                    obsInstance.name = $"Obstacle_{op.type}_{op.z:F0}_{op.x:F1}";
                    obsInstance.transform.SetParent(obsRoot.transform);
                    obsInstance.transform.position = new Vector3(op.x, 0f, op.z);
                    obsInstance.transform.localScale = scale;
                    if (envGrayMat != null) ApplyGrayscaleMatToChildren(obsInstance, envGrayMat);

                    // Setup trigger collider
                    var col = obsInstance.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = obsInstance.AddComponent<BoxCollider>();
                        box.size = op.type == ObstacleType.Cone ? new Vector3(1f, 1.2f, 1f) :
                                   op.type == ObstacleType.TireStack ? new Vector3(1.5f, 1.2f, 1.5f) :
                                   new Vector3(2.4f, 1f, 0.9f);
                        box.center = new Vector3(0f, 0.5f, 0f);
                        box.isTrigger = true;
                    }
                    else
                    {
                        col.isTrigger = true;
                    }

                    // Rigidbody for hit response
                    var rb = obsInstance.GetComponent<Rigidbody>();
                    if (rb == null) rb = obsInstance.AddComponent<Rigidbody>();
                    rb.isKinematic = true;

                    var obsComp = obsInstance.AddComponent<Obstacle>();
                    obsComp.obstacleType = op.type;
                    obsComp.speedMultiplierOnHit = op.type == ObstacleType.Cone ? 0.65f : 0.45f;
                    obsComp.cameraShakeIntensity = op.type == ObstacleType.Cone ? 0.25f : 0.38f;
                    obsComp.cameraShakeDuration = 0.3f;

                    obstacleList.Add(obsComp);
                }
            }

            // 5. Build AI Bot Carts (3 Bots on track)
            GameObject botsRoot = new GameObject("BotCars");
            List<BotCarController> botList = new List<BotCarController>();

            var botConfigs = new (GameObject prefab, float x, float z, int lane, float speed, string name)[]
            {
                (blueCarPrefab, -4.5f, 28f, 0, 15f, "BotCar_Blue"),
                (redCarPrefab, 1.5f, 55f, 2, 14f, "BotCar_Red"),
                (purpleCarPrefab != null ? purpleCarPrefab : blueCarPrefab, 4.5f, 82f, 3, 16f, "BotCar_Purple")
            };

            foreach (var bc in botConfigs)
            {
                if (bc.prefab == null) continue;

                GameObject botGo = new GameObject(bc.name);
                botGo.transform.SetParent(botsRoot.transform);
                botGo.transform.position = new Vector3(bc.x, 0.25f, bc.z);

                var botCtrl = botGo.AddComponent<BotCarController>();
                botCtrl.baseSpeed = bc.speed;
                botCtrl.currentLaneIndex = bc.lane;
                botCtrl.loopStartZ = 10f;
                botCtrl.loopEndZ = 650f;

                var bCol = botGo.AddComponent<BoxCollider>();
                bCol.isTrigger = true;
                bCol.size = new Vector3(2.2f, 1.4f, 4.4f);
                bCol.center = new Vector3(0f, 0.7f, 0.5f);

                var bRb = botGo.AddComponent<Rigidbody>();
                bRb.isKinematic = true;

                GameObject bModel = new GameObject("Model");
                bModel.transform.SetParent(botGo.transform);
                bModel.transform.localPosition = Vector3.zero;

                GameObject bInst = (GameObject)PrefabUtility.InstantiatePrefab(bc.prefab);
                bInst.name = "MeshModel";
                bInst.transform.SetParent(bModel.transform);
                bInst.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                bInst.transform.localRotation = Quaternion.identity;
                bInst.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                if (envGrayMat != null) ApplyGrayscaleMatToChildren(bInst, envGrayMat);

                // Disable internal colliders to avoid collision anomalies
                foreach (var c in bInst.GetComponentsInChildren<Collider>()) c.enabled = false;

                Transform bFl = bInst.transform.Find("Wheels/Meshes/FrontLeftWheel");
                Transform bFr = bInst.transform.Find("Wheels/Meshes/FrontRightWheel");
                Transform bRl = bInst.transform.Find("Wheels/Meshes/RearLeftWheel");
                Transform bRr = bInst.transform.Find("Wheels/Meshes/RearRightWheel");

                List<Transform> bWheels = new List<Transform>();
                if (bFl != null) bWheels.Add(bFl);
                if (bFr != null) bWheels.Add(bFr);
                if (bRl != null) bWheels.Add(bRl);
                if (bRr != null) bWheels.Add(bRr);

                botCtrl.carModel = bModel.transform;
                botCtrl.wheels = bWheels.ToArray();
                botCtrl.frontLeftWheel = bFl;
                botCtrl.frontRightWheel = bFr;

                botList.Add(botCtrl);
            }

            // 6. Build UI Canvas
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var gameUI = canvasGo.AddComponent<GameUI>();

            // Screen Flash Image
            GameObject flashGo = new GameObject("ScreenFlash");
            flashGo.transform.SetParent(canvasGo.transform, false);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;
            var flashRect = flashGo.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            flashGo.SetActive(false);
            gameUI.screenFlashImage = flashImg;

            // HUD Root
            GameObject hudRoot = new GameObject("HUD");
            hudRoot.transform.SetParent(canvasGo.transform, false);
            var hudRect = hudRoot.AddComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.sizeDelta = Vector2.zero;
            gameUI.hudRoot = hudRoot;

            // Top Question Card
            GameObject qCard = new GameObject("QuestionCard");
            qCard.transform.SetParent(hudRoot.transform, false);
            var qCardImg = qCard.AddComponent<Image>();
            qCardImg.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            var qCardRect = qCard.GetComponent<RectTransform>();
            qCardRect.anchorMin = new Vector2(0.5f, 1f);
            qCardRect.anchorMax = new Vector2(0.5f, 1f);
            qCardRect.pivot = new Vector2(0.5f, 1f);
            qCardRect.anchoredPosition = new Vector2(0f, -20f);
            qCardRect.sizeDelta = new Vector2(1200f, 170f);

            // Topic Text
            GameObject topicGo = new GameObject("TopicText");
            topicGo.transform.SetParent(qCard.transform, false);
            var topicTmp = topicGo.AddComponent<TextMeshProUGUI>();
            topicTmp.text = "[ TOPIC: COMPUTER SCIENCE ]";
            topicTmp.fontSize = 20f;
            topicTmp.alignment = TextAlignmentOptions.Left;
            topicTmp.color = new Color(0.75f, 0.75f, 0.75f);
            var topicRect = topicGo.GetComponent<RectTransform>();
            topicRect.anchorMin = new Vector2(0f, 1f);
            topicRect.anchorMax = new Vector2(0.5f, 1f);
            topicRect.pivot = new Vector2(0f, 1f);
            topicRect.anchoredPosition = new Vector2(25f, -14f);
            topicRect.sizeDelta = new Vector2(500f, 30f);
            gameUI.topicText = topicTmp;

            // Question Progress
            GameObject progGo = new GameObject("ProgressText");
            progGo.transform.SetParent(qCard.transform, false);
            var progTmp = progGo.AddComponent<TextMeshProUGUI>();
            progTmp.text = "QUESTION 1 / 5";
            progTmp.fontSize = 20f;
            progTmp.alignment = TextAlignmentOptions.Right;
            progTmp.color = Color.white;
            var progRect = progGo.GetComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0.5f, 1f);
            progRect.anchorMax = new Vector2(1f, 1f);
            progRect.pivot = new Vector2(1f, 1f);
            progRect.anchoredPosition = new Vector2(-25f, -14f);
            progRect.sizeDelta = new Vector2(500f, 30f);
            gameUI.questionProgressText = progTmp;

            // Question Body Text
            GameObject qBodyGo = new GameObject("QuestionBody");
            qBodyGo.transform.SetParent(qCard.transform, false);
            var qBodyTmp = qBodyGo.AddComponent<TextMeshProUGUI>();
            qBodyTmp.text = "Question text loading...";
            qBodyTmp.fontSize = 28f;
            qBodyTmp.enableWordWrapping = true;
            qBodyTmp.alignment = TextAlignmentOptions.Center;
            qBodyTmp.color = Color.white;
            var qBodyRect = qBodyGo.GetComponent<RectTransform>();
            qBodyRect.anchorMin = new Vector2(0f, 0f);
            qBodyRect.anchorMax = new Vector2(1f, 1f);
            qBodyRect.offsetMin = new Vector2(30f, 15f);
            qBodyRect.offsetMax = new Vector2(-30f, -45f);
            gameUI.questionBodyText = qBodyTmp;

            // Stats Bar (Lives, Score, Timer)
            GameObject statsGo = new GameObject("StatsBar");
            statsGo.transform.SetParent(hudRoot.transform, false);
            var statsRect = statsGo.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0f, 1f);
            statsRect.anchorMax = new Vector2(1f, 1f);
            statsRect.pivot = new Vector2(0.5f, 1f);
            statsRect.anchoredPosition = new Vector2(0f, -200f);
            statsRect.sizeDelta = new Vector2(1200f, 45f);

            // Lives
            GameObject livesGo = new GameObject("LivesText");
            livesGo.transform.SetParent(statsGo.transform, false);
            var livesTmp = livesGo.AddComponent<TextMeshProUGUI>();
            livesTmp.text = "LIVES: █ █ █";
            livesTmp.fontSize = 24f;
            livesTmp.alignment = TextAlignmentOptions.Left;
            livesTmp.color = Color.white;
            var livesRect = livesGo.GetComponent<RectTransform>();
            livesRect.anchorMin = new Vector2(0f, 0.5f);
            livesRect.anchorMax = new Vector2(0f, 0.5f);
            livesRect.pivot = new Vector2(0f, 0.5f);
            livesRect.anchoredPosition = new Vector2(10f, 0f);
            livesRect.sizeDelta = new Vector2(300f, 40f);
            gameUI.livesText = livesTmp;

            // Score
            GameObject scoreGo = new GameObject("ScoreText");
            scoreGo.transform.SetParent(statsGo.transform, false);
            var scoreTmp = scoreGo.AddComponent<TextMeshProUGUI>();
            scoreTmp.text = "SCORE: 0000";
            scoreTmp.fontSize = 24f;
            scoreTmp.alignment = TextAlignmentOptions.Center;
            scoreTmp.color = Color.white;
            var scoreRect = scoreGo.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = Vector2.zero;
            scoreRect.sizeDelta = new Vector2(300f, 40f);
            gameUI.scoreText = scoreTmp;

            // Timer
            GameObject timerGo = new GameObject("TimerText");
            timerGo.transform.SetParent(statsGo.transform, false);
            var timerTmp = timerGo.AddComponent<TextMeshProUGUI>();
            timerTmp.text = "TIME: 20s";
            timerTmp.fontSize = 24f;
            timerTmp.alignment = TextAlignmentOptions.Right;
            timerTmp.color = Color.white;
            var timerRect = timerGo.GetComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(1f, 0.5f);
            timerRect.anchorMax = new Vector2(1f, 0.5f);
            timerRect.pivot = new Vector2(1f, 0.5f);
            timerRect.anchoredPosition = new Vector2(-10f, 0f);
            timerRect.sizeDelta = new Vector2(300f, 40f);
            gameUI.timerText = timerTmp;

            // Bottom Controls Bar
            GameObject bottomBar = new GameObject("BottomBar");
            bottomBar.transform.SetParent(hudRoot.transform, false);
            var bImg = bottomBar.AddComponent<Image>();
            bImg.color = new Color(0.06f, 0.06f, 0.06f, 0.90f);
            var bRect = bottomBar.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 0f);
            bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.anchoredPosition = new Vector2(0f, 25f);
            bRect.sizeDelta = new Vector2(1200f, 85f);

            // Preview Aim
            GameObject aimGo = new GameObject("AimText");
            aimGo.transform.SetParent(bottomBar.transform, false);
            var aimTmp = aimGo.AddComponent<TextMeshProUGUI>();
            aimTmp.text = "AIMING AT DOOR [B]: Stack";
            aimTmp.fontSize = 22f;
            aimTmp.alignment = TextAlignmentOptions.Center;
            aimTmp.color = Color.white;
            var aimRect = aimGo.GetComponent<RectTransform>();
            aimRect.anchorMin = new Vector2(0f, 0.5f);
            aimRect.anchorMax = new Vector2(1f, 1f);
            aimRect.offsetMin = new Vector2(10f, 0f);
            aimRect.offsetMax = new Vector2(-10f, -5f);
            gameUI.selectedDoorPreviewText = aimTmp;

            // Hint Text
            GameObject hintGo = new GameObject("HintText");
            hintGo.transform.SetParent(bottomBar.transform, false);
            var hintTmp = hintGo.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "[ W / S ] Gas / Brake   [ A / D ] Steer   |   [ Q / E ] Pick Door   [ SPACE ] Ram Wall";
            hintTmp.fontSize = 18f;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.color = new Color(0.85f, 0.85f, 0.85f);
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.offsetMin = new Vector2(10f, 5f);
            hintRect.offsetMax = new Vector2(-10f, 0f);

            // Modal: Explanation (When wrong answer)
            GameObject explModal = new GameObject("ExplanationModal");
            explModal.transform.SetParent(canvasGo.transform, false);
            var emImg = explModal.AddComponent<Image>();
            emImg.color = new Color(0f, 0f, 0f, 0.85f);
            var emRect = explModal.GetComponent<RectTransform>();
            emRect.anchorMin = Vector2.zero;
            emRect.anchorMax = Vector2.one;
            emRect.sizeDelta = Vector2.zero;

            GameObject emBox = new GameObject("Box");
            emBox.transform.SetParent(explModal.transform, false);
            var emBoxImg = emBox.AddComponent<Image>();
            emBoxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            var emBoxRect = emBox.GetComponent<RectTransform>();
            emBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            emBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            emBoxRect.anchoredPosition = Vector2.zero;
            emBoxRect.sizeDelta = new Vector2(850f, 320f);

            GameObject emTitle = new GameObject("Title");
            emTitle.transform.SetParent(emBox.transform, false);
            var emTitleTmp = emTitle.AddComponent<TextMeshProUGUI>();
            emTitleTmp.text = "✖ INCORRECT - TRY AGAIN";
            emTitleTmp.fontSize = 32f;
            emTitleTmp.alignment = TextAlignmentOptions.Center;
            emTitleTmp.color = Color.white;
            var emTitleRect = emTitle.GetComponent<RectTransform>();
            emTitleRect.anchorMin = new Vector2(0f, 1f);
            emTitleRect.anchorMax = new Vector2(1f, 1f);
            emTitleRect.pivot = new Vector2(0.5f, 1f);
            emTitleRect.anchoredPosition = new Vector2(0f, -25f);
            emTitleRect.sizeDelta = new Vector2(800f, 45f);

            GameObject emText = new GameObject("Text");
            emText.transform.SetParent(emBox.transform, false);
            var emTextTmp = emText.AddComponent<TextMeshProUGUI>();
            emTextTmp.text = "Explanation text goes here...";
            emTextTmp.fontSize = 24f;
            emTextTmp.alignment = TextAlignmentOptions.Center;
            emTextTmp.enableWordWrapping = true;
            emTextTmp.color = new Color(0.85f, 0.85f, 0.85f);
            var emTextRect = emText.GetComponent<RectTransform>();
            emTextRect.anchorMin = new Vector2(0f, 0f);
            emTextRect.anchorMax = new Vector2(1f, 1f);
            emTextRect.offsetMin = new Vector2(40f, 70f);
            emTextRect.offsetMax = new Vector2(-40f, -80f);
            gameUI.explanationBodyText = emTextTmp;

            GameObject emPrompt = new GameObject("Prompt");
            emPrompt.transform.SetParent(emBox.transform, false);
            var emPromptTmp = emPrompt.AddComponent<TextMeshProUGUI>();
            emPromptTmp.text = "PRESS [ SPACE ] TO RETRY";
            emPromptTmp.fontSize = 20f;
            emPromptTmp.alignment = TextAlignmentOptions.Center;
            emPromptTmp.color = Color.white;
            var emPromptRect = emPrompt.GetComponent<RectTransform>();
            emPromptRect.anchorMin = new Vector2(0f, 0f);
            emPromptRect.anchorMax = new Vector2(1f, 0f);
            emPromptRect.pivot = new Vector2(0.5f, 0f);
            emPromptRect.anchoredPosition = new Vector2(0f, 25f);
            emPromptRect.sizeDelta = new Vector2(800f, 35f);

            explModal.SetActive(false);
            gameUI.explanationPanel = explModal;

            // Modal: Results / Game Over / Victory
            GameObject resModal = new GameObject("ResultsModal");
            resModal.transform.SetParent(canvasGo.transform, false);
            var resImg = resModal.AddComponent<Image>();
            resImg.color = new Color(0f, 0f, 0f, 0.90f);
            var resRect = resModal.GetComponent<RectTransform>();
            resRect.anchorMin = Vector2.zero;
            resRect.anchorMax = Vector2.one;
            resRect.sizeDelta = Vector2.zero;

            GameObject resBox = new GameObject("Box");
            resBox.transform.SetParent(resModal.transform, false);
            var resBoxImg = resBox.AddComponent<Image>();
            resBoxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
            var resBoxRect = resBox.GetComponent<RectTransform>();
            resBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            resBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            resBoxRect.anchoredPosition = Vector2.zero;
            resBoxRect.sizeDelta = new Vector2(850f, 380f);

            GameObject resTitle = new GameObject("Title");
            resTitle.transform.SetParent(resBox.transform, false);
            var resTitleTmp = resTitle.AddComponent<TextMeshProUGUI>();
            resTitleTmp.text = "★ RACE COMPLETED ★";
            resTitleTmp.fontSize = 42f;
            resTitleTmp.alignment = TextAlignmentOptions.Center;
            resTitleTmp.color = Color.white;
            var resTitleRect = resTitle.GetComponent<RectTransform>();
            resTitleRect.anchorMin = new Vector2(0f, 1f);
            resTitleRect.anchorMax = new Vector2(1f, 1f);
            resTitleRect.pivot = new Vector2(0.5f, 1f);
            resTitleRect.anchoredPosition = new Vector2(0f, -40f);
            resTitleRect.sizeDelta = new Vector2(800f, 60f);
            gameUI.resultsTitleText = resTitleTmp;

            GameObject resScore = new GameObject("Score");
            resScore.transform.SetParent(resBox.transform, false);
            var resScoreTmp = resScore.AddComponent<TextMeshProUGUI>();
            resScoreTmp.text = "FINAL SCORE: 0000";
            resScoreTmp.fontSize = 32f;
            resScoreTmp.alignment = TextAlignmentOptions.Center;
            resScoreTmp.color = Color.white;
            var resScoreRect = resScore.GetComponent<RectTransform>();
            resScoreRect.anchorMin = new Vector2(0f, 0.5f);
            resScoreRect.anchorMax = new Vector2(1f, 0.5f);
            resScoreRect.anchoredPosition = new Vector2(0f, 40f);
            resScoreRect.sizeDelta = new Vector2(800f, 50f);
            gameUI.resultsScoreText = resScoreTmp;

            GameObject resCorrect = new GameObject("Correct");
            resCorrect.transform.SetParent(resBox.transform, false);
            var resCorrectTmp = resCorrect.AddComponent<TextMeshProUGUI>();
            resCorrectTmp.text = "CORRECT ANSWERS: 5 / 5";
            resCorrectTmp.fontSize = 26f;
            resCorrectTmp.alignment = TextAlignmentOptions.Center;
            resCorrectTmp.color = new Color(0.85f, 0.85f, 0.85f);
            var resCorrectRect = resCorrect.GetComponent<RectTransform>();
            resCorrectRect.anchorMin = new Vector2(0f, 0.5f);
            resCorrectRect.anchorMax = new Vector2(1f, 0.5f);
            resCorrectRect.anchoredPosition = new Vector2(0f, -15f);
            resCorrectRect.sizeDelta = new Vector2(800f, 40f);
            gameUI.resultsCorrectText = resCorrectTmp;

            GameObject resAcc = new GameObject("Accuracy");
            resAcc.transform.SetParent(resBox.transform, false);
            var resAccTmp = resAcc.AddComponent<TextMeshProUGUI>();
            resAccTmp.text = "ACCURACY: 100.0%";
            resAccTmp.fontSize = 26f;
            resAccTmp.alignment = TextAlignmentOptions.Center;
            resAccTmp.color = new Color(0.85f, 0.85f, 0.85f);
            var resAccRect = resAcc.GetComponent<RectTransform>();
            resAccRect.anchorMin = new Vector2(0f, 0.5f);
            resAccRect.anchorMax = new Vector2(1f, 0.5f);
            resAccRect.anchoredPosition = new Vector2(0f, -65f);
            resAccRect.sizeDelta = new Vector2(800f, 40f);
            gameUI.resultsAccuracyText = resAccTmp;

            GameObject resPrompt = new GameObject("Prompt");
            resPrompt.transform.SetParent(resBox.transform, false);
            var resPromptTmp = resPrompt.AddComponent<TextMeshProUGUI>();
            resPromptTmp.text = "PRESS [ SPACE ] TO RESTART RACE";
            resPromptTmp.fontSize = 22f;
            resPromptTmp.alignment = TextAlignmentOptions.Center;
            resPromptTmp.color = Color.white;
            var resPromptRect = resPrompt.GetComponent<RectTransform>();
            resPromptRect.anchorMin = new Vector2(0f, 0f);
            resPromptRect.anchorMax = new Vector2(1f, 0f);
            resPromptRect.pivot = new Vector2(0.5f, 0f);
            resPromptRect.anchoredPosition = new Vector2(0f, 35f);
            resPromptRect.sizeDelta = new Vector2(800f, 40f);
            gameUI.resultsPromptText = resPromptTmp;

            resModal.SetActive(false);
            gameUI.resultsPanel = resModal;

            // 7. GameManager Root
            GameObject gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var qm = gmGo.AddComponent<QuestionManager>();
            var audio = gmGo.AddComponent<GameAudio>();

            gm.questionManager = qm;
            gm.carController = carController;
            gm.gameUI = gameUI;
            gm.gameAudio = audio;
            gm.checkpoints = checkpointList;
            gm.checkpointsContainer = cpRoot.transform;
            gm.obstacles = obstacleList;
            gm.botCars = botList;
            gm.checkpointSpacing = 120f;

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[Gamify] Grayscale 2.5D Racer Scene successfully generated with WASD Controls, 120 Spacing, Obstacles, and AI Bot Carts!");
        }
    }
}

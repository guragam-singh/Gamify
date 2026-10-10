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

        [MenuItem("Tools/Gamify/Build Racer Scene")]
        public static void BuildScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Undo.RegisterFullObjectHierarchyUndo(activeScene.GetRootGameObjects().Length > 0 ? activeScene.GetRootGameObjects()[0] : null, "Build Racer Scene");

            // Clean existing game objects except Main Camera and Light if already configured
            var roots = activeScene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go.name == "Global Light 2D")
                {
                    Object.DestroyImmediate(go);
                }
            }

            // Setup Camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.orthographic = false;
            cam.fieldOfView = 58f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            cam.transform.position = new Vector3(-1.5f, 3.5f, -6.8f);
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
            dirLight.intensity = 1.1f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Materials
            Material roadMat = CreateGrayscaleMat("RoadMat", new Color(0.12f, 0.12f, 0.12f));
            Material shoulderMat = CreateGrayscaleMat("ShoulderMat", new Color(0.05f, 0.05f, 0.05f));
            Material stripeMat = CreateGrayscaleMat("StripeMat", new Color(0.9f, 0.9f, 0.9f));
            Material carBodyMat = CreateGrayscaleMat("CarBodyMat", new Color(0.25f, 0.25f, 0.25f));
            Material carCockpitMat = CreateGrayscaleMat("CarCockpitMat", new Color(0.85f, 0.85f, 0.85f));
            Material carWheelMat = CreateGrayscaleMat("CarWheelMat", new Color(0.04f, 0.04f, 0.04f));
            Material headlightMat = CreateGrayscaleMat("HeadlightMat", Color.white);
            Material frameMat = CreateGrayscaleMat("FrameMat", new Color(0.2f, 0.2f, 0.2f));
            Material wallMat = CreateGrayscaleMat("WallMat", new Color(0.5f, 0.5f, 0.5f));
            Material pillarMat = CreateGrayscaleMat("PillarMat", new Color(0.35f, 0.35f, 0.35f));

            // Clean old track / game objects if they exist
            string[] cleanupNames = { "Track", "Car", "Checkpoints", "GameManager", "Canvas", "EventSystem" };
            foreach (var n in cleanupNames)
            {
                var found = GameObject.Find(n);
                if (found != null) Object.DestroyImmediate(found);
            }

            // Build Track
            GameObject trackRoot = new GameObject("Track");

            // Main Road Surface (Length: 380, Width: 13)
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "RoadSurface";
            road.transform.SetParent(trackRoot.transform);
            road.transform.position = new Vector3(0f, -0.1f, 170f);
            road.transform.localScale = new Vector3(13f, 0.2f, 400f);
            road.GetComponent<MeshRenderer>().material = roadMat;

            // Left & Right Shoulders
            GameObject leftShoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftShoulder.name = "LeftShoulder";
            leftShoulder.transform.SetParent(trackRoot.transform);
            leftShoulder.transform.position = new Vector3(-7f, 0.15f, 170f);
            leftShoulder.transform.localScale = new Vector3(1f, 0.4f, 400f);
            leftShoulder.GetComponent<MeshRenderer>().material = shoulderMat;

            GameObject rightShoulder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightShoulder.name = "RightShoulder";
            rightShoulder.transform.SetParent(trackRoot.transform);
            rightShoulder.transform.position = new Vector3(7f, 0.15f, 170f);
            rightShoulder.transform.localScale = new Vector3(1f, 0.4f, 400f);
            rightShoulder.GetComponent<MeshRenderer>().material = shoulderMat;

            // Lane Divider Dashes (-3, 0, 3)
            float[] dividerX = new float[] { -3f, 0f, 3f };
            for (float z = -10f; z < 360f; z += 12f)
            {
                foreach (float x in dividerX)
                {
                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stripe.name = "Stripe";
                    stripe.transform.SetParent(trackRoot.transform);
                    stripe.transform.position = new Vector3(x, 0.02f, z);
                    stripe.transform.localScale = new Vector3(0.2f, 0.05f, 5f);
                    stripe.GetComponent<MeshRenderer>().material = stripeMat;
                    Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
            }

            // Roadside Pillars for speed sense
            for (float z = 0f; z < 360f; z += 18f)
            {
                // Left & Right pillars
                GameObject pL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pL.name = "Pillar_L";
                pL.transform.SetParent(trackRoot.transform);
                pL.transform.position = new Vector3(-7.8f, 1.2f, z);
                pL.transform.localScale = new Vector3(0.4f, 2.4f, 0.4f);
                pL.GetComponent<MeshRenderer>().material = pillarMat;

                GameObject pR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pR.name = "Pillar_R";
                pR.transform.SetParent(trackRoot.transform);
                pR.transform.position = new Vector3(7.8f, 1.2f, z);
                pR.transform.localScale = new Vector3(0.4f, 2.4f, 0.4f);
                pR.GetComponent<MeshRenderer>().material = pillarMat;
            }

            // Build Car
            GameObject carGo = new GameObject("Car");
            carGo.transform.position = new Vector3(-1.5f, 0.3f, 0f);
            var carController = carGo.AddComponent<CarController>();

            GameObject carModel = new GameObject("Model");
            carModel.transform.SetParent(carGo.transform);
            carModel.transform.localPosition = Vector3.zero;

            // Chassis
            GameObject chassis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chassis.name = "Chassis";
            chassis.transform.SetParent(carModel.transform);
            chassis.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            chassis.transform.localScale = new Vector3(1.7f, 0.45f, 3.2f);
            chassis.GetComponent<MeshRenderer>().material = carBodyMat;
            Object.DestroyImmediate(chassis.GetComponent<Collider>());

            // Cockpit
            GameObject cockpit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cockpit.name = "Cockpit";
            cockpit.transform.SetParent(carModel.transform);
            cockpit.transform.localPosition = new Vector3(0f, 0.5f, -0.2f);
            cockpit.transform.localScale = new Vector3(1.3f, 0.4f, 1.6f);
            cockpit.GetComponent<MeshRenderer>().material = carCockpitMat;
            Object.DestroyImmediate(cockpit.GetComponent<Collider>());

            // Headlights
            GameObject hlL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hlL.name = "Headlight_L";
            hlL.transform.SetParent(carModel.transform);
            hlL.transform.localPosition = new Vector3(-0.6f, 0.18f, 1.62f);
            hlL.transform.localScale = new Vector3(0.3f, 0.2f, 0.1f);
            hlL.GetComponent<MeshRenderer>().material = headlightMat;
            Object.DestroyImmediate(hlL.GetComponent<Collider>());

            GameObject hlR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hlR.name = "Headlight_R";
            hlR.transform.SetParent(carModel.transform);
            hlR.transform.localPosition = new Vector3(0.6f, 0.18f, 1.62f);
            hlR.transform.localScale = new Vector3(0.3f, 0.2f, 0.1f);
            hlR.GetComponent<MeshRenderer>().material = headlightMat;
            Object.DestroyImmediate(hlR.GetComponent<Collider>());

            // Wheels
            Transform[] wheels = new Transform[4];
            Vector3[] wheelPositions = new Vector3[]
            {
                new Vector3(-0.95f, 0f, 1.0f),
                new Vector3(0.95f, 0f, 1.0f),
                new Vector3(-0.95f, 0f, -1.0f),
                new Vector3(0.95f, 0f, -1.0f)
            };
            for (int w = 0; w < 4; w++)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{w}";
                wheel.transform.SetParent(carModel.transform);
                wheel.transform.localPosition = wheelPositions[w];
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.65f, 0.2f, 0.65f);
                wheel.GetComponent<MeshRenderer>().material = carWheelMat;
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheels[w] = wheel.transform;
            }

            carController.carModel = carModel.transform;
            carController.wheels = wheels;
            carController.followCamera = cam;

            // Build Checkpoints (5 checkpoints)
            GameObject cpRoot = new GameObject("Checkpoints");
            List<Checkpoint> checkpointList = new List<Checkpoint>();

            float[] cpDistances = new float[] { 45f, 110f, 175f, 240f, 305f };
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

                // Overhead archway beam
                GameObject archBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                archBeam.name = "ArchBeam";
                archBeam.transform.SetParent(cpGo.transform);
                archBeam.transform.localPosition = new Vector3(0f, 4.3f, 0f);
                archBeam.transform.localScale = new Vector3(13f, 0.5f, 0.8f);
                archBeam.GetComponent<MeshRenderer>().material = frameMat;

                // 4 Doors
                for (int d = 0; d < 4; d++)
                {
                    float doorX = laneXs[d];
                    GameObject doorGo = new GameObject($"Door_{d}");
                    doorGo.transform.SetParent(cpGo.transform);
                    doorGo.transform.localPosition = new Vector3(doorX, 0f, 0f);

                    var doorCtrl = doorGo.AddComponent<DoorController>();

                    // Door Archway Frame (Left pillar, Right pillar, Top header)
                    GameObject frameRoot = new GameObject("Frame");
                    frameRoot.transform.SetParent(doorGo.transform);
                    frameRoot.transform.localPosition = Vector3.zero;

                    GameObject fLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fLeft.transform.SetParent(frameRoot.transform);
                    fLeft.transform.localPosition = new Vector3(-1.35f, 1.7f, 0f);
                    fLeft.transform.localScale = new Vector3(0.25f, 3.4f, 0.6f);
                    fLeft.GetComponent<MeshRenderer>().material = frameMat;

                    GameObject fRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fRight.transform.SetParent(frameRoot.transform);
                    fRight.transform.localPosition = new Vector3(1.35f, 1.7f, 0f);
                    fRight.transform.localScale = new Vector3(0.25f, 3.4f, 0.6f);
                    fRight.GetComponent<MeshRenderer>().material = frameMat;

                    GameObject fTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fTop.transform.SetParent(frameRoot.transform);
                    fTop.transform.localPosition = new Vector3(0f, 3.4f, 0f);
                    fTop.transform.localScale = new Vector3(2.9f, 0.3f, 0.6f);
                    fTop.GetComponent<MeshRenderer>().material = frameMat;

                    // Solid Breakable Wall
                    GameObject wallSolid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallSolid.name = "WallSolid";
                    wallSolid.transform.SetParent(doorGo.transform);
                    wallSolid.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                    wallSolid.transform.localScale = new Vector3(2.45f, 3.0f, 0.35f);
                    wallSolid.GetComponent<MeshRenderer>().material = wallMat;

                    // Indicator Arrow (above door)
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

            // Build UI Canvas
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
            bRect.sizeDelta = new Vector2(1100f, 85f);

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
            hintTmp.text = "[ Q / E ] Cycle Doors    |    [ SPACE ] Confirm & Ram Door";
            hintTmp.fontSize = 18f;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.color = new Color(0.7f, 0.7f, 0.7f);
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.offsetMin = new Vector2(10f, 5f);
            hintRect.offsetMax = new Vector2(-10f, 0f);
            gameUI.controlsHintText = hintTmp;

            // Explanation Modal (Wrong Answer)
            GameObject expModal = new GameObject("ExplanationModal");
            expModal.transform.SetParent(canvasGo.transform, false);
            var expBg = expModal.AddComponent<Image>();
            expBg.color = new Color(0f, 0f, 0f, 0.88f);
            var expRect = expModal.GetComponent<RectTransform>();
            expRect.anchorMin = Vector2.zero;
            expRect.anchorMax = Vector2.one;
            expRect.sizeDelta = Vector2.zero;

            GameObject expBox = new GameObject("Box");
            expBox.transform.SetParent(expModal.transform, false);
            var expBoxImg = expBox.AddComponent<Image>();
            expBoxImg.color = new Color(0.14f, 0.14f, 0.14f, 1f);
            var expBoxRect = expBox.GetComponent<RectTransform>();
            expBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            expBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            expBoxRect.sizeDelta = new Vector2(900f, 400f);

            GameObject expTitle = new GameObject("Title");
            expTitle.transform.SetParent(expBox.transform, false);
            var expTitleTmp = expTitle.AddComponent<TextMeshProUGUI>();
            expTitleTmp.text = "CRASH! WRONG DOOR";
            expTitleTmp.fontSize = 36f;
            expTitleTmp.alignment = TextAlignmentOptions.Center;
            expTitleTmp.color = Color.white;
            var expTitleRect = expTitle.GetComponent<RectTransform>();
            expTitleRect.anchorMin = new Vector2(0f, 1f);
            expTitleRect.anchorMax = new Vector2(1f, 1f);
            expTitleRect.pivot = new Vector2(0.5f, 1f);
            expTitleRect.anchoredPosition = new Vector2(0f, -30f);
            expTitleRect.sizeDelta = new Vector2(800f, 50f);
            gameUI.explanationTitleText = expTitleTmp;

            GameObject expBody = new GameObject("Body");
            expBody.transform.SetParent(expBox.transform, false);
            var expBodyTmp = expBody.AddComponent<TextMeshProUGUI>();
            expBodyTmp.text = "Explanation of why this answer is wrong...";
            expBodyTmp.fontSize = 24f;
            expBodyTmp.enableWordWrapping = true;
            expBodyTmp.alignment = TextAlignmentOptions.Center;
            expBodyTmp.color = new Color(0.85f, 0.85f, 0.85f);
            var expBodyRect = expBody.GetComponent<RectTransform>();
            expBodyRect.anchorMin = new Vector2(0f, 0f);
            expBodyRect.anchorMax = new Vector2(1f, 1f);
            expBodyRect.offsetMin = new Vector2(40f, 100f);
            expBodyRect.offsetMax = new Vector2(-40f, -90f);
            gameUI.explanationBodyText = expBodyTmp;

            GameObject expPrompt = new GameObject("Prompt");
            expPrompt.transform.SetParent(expBox.transform, false);
            var expPromptTmp = expPrompt.AddComponent<TextMeshProUGUI>();
            expPromptTmp.text = "PRESS [ SPACE ] TO RETRY QUESTION";
            expPromptTmp.fontSize = 22f;
            expPromptTmp.alignment = TextAlignmentOptions.Center;
            expPromptTmp.color = Color.white;
            var expPromptRect = expPrompt.GetComponent<RectTransform>();
            expPromptRect.anchorMin = new Vector2(0f, 0f);
            expPromptRect.anchorMax = new Vector2(1f, 0f);
            expPromptRect.pivot = new Vector2(0.5f, 0f);
            expPromptRect.anchoredPosition = new Vector2(0f, 30f);
            expPromptRect.sizeDelta = new Vector2(800f, 40f);
            gameUI.explanationPromptText = expPromptTmp;

            expModal.SetActive(false);
            gameUI.explanationPanel = expModal;

            // Results Modal (Victory / Game Over)
            GameObject resModal = new GameObject("ResultsModal");
            resModal.transform.SetParent(canvasGo.transform, false);
            var resBg = resModal.AddComponent<Image>();
            resBg.color = new Color(0f, 0f, 0f, 0.92f);
            var resRect = resModal.GetComponent<RectTransform>();
            resRect.anchorMin = Vector2.zero;
            resRect.anchorMax = Vector2.one;
            resRect.sizeDelta = Vector2.zero;

            GameObject resBox = new GameObject("Box");
            resBox.transform.SetParent(resModal.transform, false);
            var resBoxImg = resBox.AddComponent<Image>();
            resBoxImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            var resBoxRect = resBox.GetComponent<RectTransform>();
            resBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            resBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            resBoxRect.sizeDelta = new Vector2(920f, 520f);

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

            // GameManager Root
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

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[Gamify] Grayscale 2.5D Racer Scene successfully generated and saved!");
        }
    }
}

using System.Collections.Generic;
using SamsamIdleOn.Core;
using SamsamIdleOn.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SamsamIdleOn.Editor
{
    public static class HomeScreenSceneBuilder
    {
        private const string HomeScenePath = "Assets/_Game/Scenes/Home.unity";
        private const string MainScenePath = "Assets/_Game/Scenes/Main.unity";
        private const string MainSceneName = "Main";
        private const string HomeRootName = "HomeScreenRoot";

        [MenuItem("Samsam IdleOn/Build Home Screen")]
        public static void BuildHomeScreen()
        {
            Scene scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
            EnsureCamera();
            EnsureEventSystem();
            GameManager gameManager = EnsureGameManager();
            BuildCanvas(gameManager);
            EnsureHomeSceneFirstInBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureCamera()
        {
            Camera camera = Object.FindAnyObjectByType<Camera>();

            if (camera == null)
            {
                GameObject cameraObject = new("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.tag = "MainCamera";
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.14f, 0.18f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static GameManager EnsureGameManager()
        {
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();

            if (gameManager != null)
            {
                return gameManager;
            }

            GameObject gameManagerObject = new("GameManager");
            return gameManagerObject.AddComponent<GameManager>();
        }

        private static void BuildCanvas(GameManager gameManager)
        {
            GameObject existingRoot = GameObject.Find(HomeRootName);

            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }

            GameObject root = new(HomeRootName);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image background = CreateImage(rootRect, "Background", new Color(0.08f, 0.14f, 0.18f, 1f));
            Stretch(background.rectTransform);

            Image panel = CreateImage(rootRect, "MenuPanel", new Color(0.06f, 0.08f, 0.09f, 0.92f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(620f, 560f);

            TMP_Text title = CreateText(panelRect, "TitleLabel", "Samsam IdleOn", 58f, FontStyles.Bold, new Color(0.98f, 0.94f, 0.72f, 1f));
            SetRect(title.rectTransform, new Vector2(0f, 166f), new Vector2(540f, 92f));

            TMP_Text name = CreateText(panelRect, "NameLabel", "Sameer", 30f, FontStyles.Normal, new Color(0.72f, 0.9f, 0.95f, 1f));
            SetRect(name.rectTransform, new Vector2(0f, 104f), new Vector2(540f, 48f));

            TMP_Text subtitle = CreateText(panelRect, "SubtitleLabel", "Early-game idle adventure demo", 24f, FontStyles.Normal, new Color(0.78f, 0.82f, 0.8f, 1f));
            SetRect(subtitle.rectTransform, new Vector2(0f, 55f), new Vector2(540f, 42f));

            Button playButton = CreateButton(panelRect, "PlayButton", "Play", new Vector2(0f, -45f), new Vector2(360f, 76f), new Color(0.32f, 0.72f, 0.52f, 1f));
            Button resetButton = CreateButton(panelRect, "ResetSaveButton", "Reset Save", new Vector2(0f, -137f), new Vector2(360f, 58f), new Color(0.45f, 0.48f, 0.52f, 1f));

            TMP_Text status = CreateText(panelRect, "StatusLabel", string.Empty, 18f, FontStyles.Normal, new Color(0.82f, 0.86f, 0.84f, 1f));
            SetRect(status.rectTransform, new Vector2(0f, -214f), new Vector2(480f, 36f));

            TMP_Text devNote = CreateText(panelRect, "DevNoteLabel", "Reset Save is for development testing.", 16f, FontStyles.Normal, new Color(0.6f, 0.65f, 0.64f, 1f));
            SetRect(devNote.rectTransform, new Vector2(0f, -250f), new Vector2(480f, 30f));

            HomeScreenController controller = root.AddComponent<HomeScreenController>();
            SerializedObject serializedController = new(controller);
            serializedController.FindProperty("mainSceneName").stringValue = MainSceneName;
            serializedController.FindProperty("gameManager").objectReferenceValue = gameManager;
            serializedController.FindProperty("playButton").objectReferenceValue = playButton;
            serializedController.FindProperty("resetSaveButton").objectReferenceValue = resetButton;
            serializedController.FindProperty("statusLabel").objectReferenceValue = status;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateImage(RectTransform parent, string name, Color color)
        {
            GameObject imageObject = new(name);
            imageObject.layer = 5;
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, FontStyles style, Color color)
        {
            GameObject textObject = new(name);
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Color color)
        {
            Image image = CreateImage(parent, name, color);
            SetRect(image.rectTransform, position, size);

            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            button.colors = colors;

            TMP_Text text = CreateText(image.rectTransform, "Label", label, 28f, FontStyles.Bold, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRect(RectTransform rectTransform, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }

        private static void EnsureHomeSceneFirstInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new();
            scenes.Add(new EditorBuildSettingsScene(HomeScenePath, true));

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == HomeScenePath)
                {
                    continue;
                }

                scenes.Add(scene);
            }

            bool hasMainScene = false;

            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == MainScenePath)
                {
                    hasMainScene = true;
                    break;
                }
            }

            if (!hasMainScene)
            {
                scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

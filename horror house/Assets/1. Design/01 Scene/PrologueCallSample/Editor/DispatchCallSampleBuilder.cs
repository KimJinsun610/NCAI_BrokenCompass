using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace NightDuty.PrologueSample.Editor
{
    public static class DispatchCallSampleBuilder
    {
        public const string Folder = "Assets/1. Design/01 Scene/PrologueCallSample";
        public const string ScenePath = Folder + "/PrologueCallSample_hyun.unity";
        private static TMP_FontAsset font;
        private static readonly Color Ink = new Color(.84f, .87f, .84f);
        private static readonly Color Muted = new Color(.48f, .57f, .54f);

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Sample already exists; refusing to overwrite.");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                font = CreateFont();
                var camera = new GameObject("SampleCamera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = new Color(.018f, .025f, .026f);
                new GameObject("SampleEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var root = new GameObject("DispatchCallSample", typeof(DispatchCallSample));
                var controller = root.GetComponent<DispatchCallSample>();
                var canvasObject = new GameObject("Canvas_Prologue", typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var background = Panel("Background", canvas.transform, new Color(.025f, .036f, .039f));
                for (int i = 0; i < 18; i++)
                    Box("ShadowBand" + i, background.transform, new Vector2(0, 540 - i * 60), new Vector2(1920, 60), new Color(0, 0, 0, .025f + i * .009f));
                Box("LeftRule", background.transform, new Vector2(-780, 0), new Vector2(1, 800), new Color(.22f, .3f, .28f, .5f));
                Text("Brand", background.transform, "야간근무", new Vector2(-652, 448), new Vector2(220, 50), 24, Ink, TextAlignmentOptions.Left);
                Text("Footer", background.transform, "NIGHT DUTY   /   PROLOGUE", new Vector2(-550, -450), new Vector2(420, 40), 16, Muted, TextAlignmentOptions.Left);

                var title = Panel("Title", canvas.transform, Color.clear);
                Text("Eyebrow", title.transform, "철거까지 남은 시간, 닷새.", new Vector2(0, 125), new Vector2(1000, 60), 26, Muted);
                var heading = Text("Heading", title.transform, "야간근무", new Vector2(0, 20), new Vector2(1100, 140), 92, Ink);
                heading.characterSpacing = 18;
                Text("Subtitle", title.transform, "한 통의 전화가 걸려 왔다.", new Vector2(0, -95), new Vector2(1000, 60), 27, Muted);
                var play = Button("Play", title.transform, "게임 플레이", new Vector2(0, -240), new Vector2(310, 68));

                var call = Panel("Call", canvas.transform, Color.clear);
                var group = call.AddComponent<CanvasGroup>();
                var phone = Box("PhoneSilhouette", call.transform, new Vector2(-515, 25), new Vector2(272, 480), new Color(.065f, .084f, .084f));
                Box("PhoneScreen", phone.transform, Vector2.zero, new Vector2(248, 446), new Color(.028f, .044f, .044f));
                Box("SpeakerSlot", phone.transform, new Vector2(0, 194), new Vector2(62, 4), Muted);
                Text("PhoneCaller", phone.transform, "시설관리팀", new Vector2(0, 85), new Vector2(230, 52), 26, Ink);
                Text("PhoneLabel", phone.transform, "파견 담당자", new Vector2(0, 37), new Vector2(230, 45), 18, Muted);
                Box("PhoneHome", phone.transform, new Vector2(0, -194), new Vector2(64, 3), Muted);
                var bars = new RectTransform[27];
                for (int i = 0; i < bars.Length; i++)
                    bars[i] = Box("VoiceBar" + i, phone.transform, new Vector2((i - 13) * 6, -45), new Vector2(3, 7), new Color(.47f, .66f, .59f)).GetComponent<RectTransform>();
                var state = Text("CallState", call.transform, "수신 전화", new Vector2(170, 255), new Vector2(900, 54), 23, new Color(.52f, .71f, .64f), TextAlignmentOptions.Left);
                var caller = Text("Caller", call.transform, "시설관리팀 · 파견 담당자", new Vector2(170, 190), new Vector2(900, 56), 27, Ink, TextAlignmentOptions.Left);
                Box("Divider", call.transform, new Vector2(170, 133), new Vector2(900, 1), new Color(.22f, .3f, .28f));
                var sub = Text("Dialogue", call.transform, "......", new Vector2(170, -12), new Vector2(900, 230), 39, Ink, TextAlignmentOptions.MidlineLeft);
                sub.lineSpacing = 16;
                var action = Button("NextOrContract", call.transform, "다음 문장", new Vector2(420, -225), new Vector2(400, 66));
                var skip = Button("SkipCall", call.transform, "통화 건너뛰기", new Vector2(600, -450), new Vector2(260, 48));
                skip.GetComponent<UnityEngine.UI.Image>().color = new Color(.05f, .07f, .07f);
                var black = Panel("TransitionFade", canvas.transform, Color.black);
                black.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
                var fade = black.AddComponent<CanvasGroup>();
                fade.alpha = 0; fade.blocksRaycasts = false;

                AudioSource VoiceSource(string name, float volume)
                {
                    var obj = new GameObject(name, typeof(AudioSource));
                    obj.transform.SetParent(root.transform);
                    var source = obj.GetComponent<AudioSource>();
                    source.playOnAwake = false; source.volume = volume; source.spatialBlend = 0;
                    return source;
                }
                var voice = VoiceSource("CallerVoice", .85f);
                voice.gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 260;
                voice.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400;
                var effects = VoiceSource("TelephoneEffects", .6f);
                var ambience = VoiceSource("QuietOfficeBed", .6f);
                var serialized = new SerializedObject(controller);
                void Set(string key, UnityEngine.Object value) { serialized.FindProperty(key).objectReferenceValue = value; }
                Set("titlePanel", title); Set("callPanel", call); Set("stateText", state); Set("subtitle", sub);
                Set("counter", caller); Set("actionLabel", action.GetComponentInChildren<TMP_Text>());
                Set("playButton", play); Set("actionButton", action); Set("skipButton", skip);
                Set("callGroup", group); Set("fade", fade); Set("voice", voice); Set("effects", effects); Set("ambience", ambience);
                var barProperty = serialized.FindProperty("bars"); barProperty.arraySize = bars.Length;
                for (int i = 0; i < bars.Length; i++) barProperty.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
                var clips = serialized.FindProperty("voiceClips"); clips.arraySize = DispatchCallSample.Lines.Length;
                for (int i = 0; i < clips.arraySize; i++)
                {
                    var path = Folder + "/Voice/Call_" + (i + 1).ToString("D2") + ".wav";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) throw new InvalidOperationException("Missing TTS: " + path);
                    clips.GetArrayElementAtIndex(i).objectReferenceValue = clip;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ApplyMinimalLayout(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
                return ScenePath;
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Modify only this sample's layout, preserving its existing serialized audio references.
        public static string ApplyMinimalLayout(Scene scene)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (scene.path != ScenePath && scene.path != "") throw new InvalidOperationException("Not the sample scene.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Find(string name) => all.First(t => t.name == name);
            var background = Find("Background");
            background.GetComponent<UnityEngine.UI.Image>().color = Color.black;
            foreach (Transform child in background) child.gameObject.SetActive(false);
            Find("SampleCamera").GetComponent<Camera>().backgroundColor = Color.black;
            Find("Title").gameObject.SetActive(false);
            Find("Play").gameObject.SetActive(false);
            var call = Find("Call");
            call.gameObject.SetActive(true);
            foreach (string name in new[] { "CallState", "Caller", "Divider", "NextOrContract", "SkipCall" })
                Find(name).gameObject.SetActive(false);
            // Keep only the original waveform; the phone frame and its labels are not displayed.
            var phone = Find("PhoneSilhouette");
            foreach (var bar in all.Where(t => t.name.StartsWith("VoiceBar")))
            {
                bar.SetParent(call, false);
                var rect = (RectTransform)bar;
                int index = int.Parse(bar.name.Substring("VoiceBar".Length));
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2((index - 13) * 8, 0);
                rect.sizeDelta = new Vector2(3, 5);
                bar.GetComponent<UnityEngine.UI.Image>().color = new Color(.10f, .10f, .10f, 1);
            }
            phone.gameObject.SetActive(false);
            var text = Find("Dialogue").GetComponent<TextMeshProUGUI>();
            text.text = "......";
            text.color = new Color(.84f, .84f, .84f);
            text.fontSize = 34;
            text.alignment = TextAlignmentOptions.Center;
            text.lineSpacing = 10;
            var subtitleRect = text.rectTransform;
            subtitleRect.anchorMin = new Vector2(.12f, 0);
            subtitleRect.anchorMax = new Vector2(.88f, 0);
            subtitleRect.pivot = new Vector2(.5f, 0);
            subtitleRect.sizeDelta = new Vector2(0, 150);
            subtitleRect.anchoredPosition = new Vector2(0, 75);
            EditorSceneManager.MarkSceneDirty(scene);
            return "Black background, lower subtitles, dark monochrome waveform, no visible buttons";
        }

        private static TMP_FontAsset CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/0. Main/99 Resources/FONT/Freesentation-4Regular.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            asset.name = "DispatchCall_Korean";
            string characters = string.Concat(DispatchCallSample.Lines) + "야간근무철거까지남은시간닷새한통의전화가걸려왔다게임플레이수신전화시설관리팀파견담당자통화연결됨다음문장통화건너뛰기통화종료문서가도착했습니다발신파견근무계약서근무조건을확인한뒤결정하십시오계약서확인계약서씬이빌드목록에없습니다이샘플은에디터에서먼저확인해주세요...... ·";
            characters += new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            if (!asset.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("Font missing characters: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, Folder + "/DispatchCall_Korean.asset");
            foreach (var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static GameObject Panel(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return obj;
        }

        private static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var obj = Panel(name, parent, color);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = pos;
            return obj;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 pos, Vector2 size, float pointSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = pos;
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = pointSize; text.enableAutoSizing = false; text.color = color;
            text.alignment = alignment; text.text = value; text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var obj = Box(name, parent, pos, size, new Color(.12f, .20f, .18f));
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = obj.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.72f, .90f, .82f); colors.pressedColor = new Color(.45f, .62f, .54f); button.colors = colors;
            Text("Label", obj.transform, label, Vector2.zero, size - new Vector2(24, 8), 23, Ink);
            return button;
        }
    }
}

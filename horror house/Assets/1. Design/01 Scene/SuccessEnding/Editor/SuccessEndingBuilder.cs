using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDuty.SuccessEnding.Editor
{
    // 성공 엔딩 씬 생성 도구. Assets/1. Design/01 Scene/SuccessEnding 안에만 씁니다.
    public static class SuccessEndingBuilder
    {
        public const string Folder = "Assets/1. Design/01 Scene/SuccessEnding";
        public const string ScenePath = Folder + "/SuccessEnding_hyun.unity";
        public const string FontPath = Folder + "/SuccessEnding_Korean.asset";
        public const string PrologueAudio = "Assets/1. Design/01 Scene/PrologueCallSample/Resources/PhoneAudio";
        public const string SourceFont = "Assets/0. Main/99 Resources/FONT/Freesentation-4Regular.ttf";
        // 자막(대사)은 프롤로그와 같은 Noto Serif KR 자막 폰트. 글자 추가: Fonts/NotoSerifKR/SubtitleCharacters.txt → Rebuild 메뉴.
        public const string SubtitleFontPath = "Assets/1. Design/Fonts/NotoSerifKR/NotoSerifKR-Medium SDF Subtitle.asset";
        public const string TabletCursorText = "인수인계";
        public const string TabletText = "인수인계가 진행됩니다.";

        public static readonly string[] RuleTexts = {
            "뒤를 돌아보지 마십시오.", "부르는 소리에 대답하지 마십시오.", "손전등을 끄십시오.", "계속 비추십시오.",
            "멈추십시오.", "대답하지 마십시오.", "돌아보지 마십시오.", "경비실을 나가지 마십시오."
        };

        private static AudioClip Clip(string relative)
        {
            var path = relative.StartsWith("Assets/") ? relative : Folder + "/" + relative;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing audio: " + path);
            return clip;
        }

        private static EndingLine Line(string id, string text, float duration, params string[] voices)
        {
            return new EndingLine
            {
                id = id, text = text, duration = duration,
                voices = voices.Select(v => Clip("Voice/" + v)).ToArray(),
            };
        }

        private static EndingLine Line(string id, string text, float duration, EndingVoiceRoute route, float volume, params string[] voices)
        {
            var l = Line(id, text, duration, voices);
            l.voiceRoute = route; l.voiceVolume = volume;
            return l;
        }

        private static EndingLine Burst(string id, string text, float duration, int copies, float spread, bool edges,
            float noise, float stat, float hb, float hbVol, float tension, float stagger, float volume, params string[] voices)
        {
            var l = Line(id, text, duration, voices);
            l.mode = EndingLineMode.RandomStay; l.extendToVoice = false; l.voiceRoute = EndingVoiceRoute.Crowd;
            l.copies = copies; l.spawnSpread = spread; l.preferEdges = edges;
            l.voiceStagger = stagger; l.voiceVolume = volume;
            l.driveBuildUpAudio = true; l.lineNoise = noise; l.staticNoise = stat;
            l.heartbeatInterval = hb; l.heartbeatVolume = hbVol; l.tensionVolume = tension;
            return l;
        }

        public static string AllCharacters()
        {
            // 수칙지 폰트(SuccessEnding_Korean)에 필요한 글자: 몰아치기 수칙 + 23~25번 태블릿 문장
            var lines = new List<string>(RuleTexts) {
                TabletCursorText + "_", TabletText,
                "「」『』·…—–~!?.,:;'\"()[]0123456789"
            };
            string ascii = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
            return new string((string.Concat(lines) + ascii).Distinct().ToArray());
        }

        public static TMP_FontAsset GetOrCreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                // 동적 폰트: 플레이 중에 글자가 추가되지 않도록 필요한 글자를 미리 넣어 둔다
                string need = new string(AllCharacters().Where(c => !char.IsWhiteSpace(c) && !existing.HasCharacter(c)).ToArray());
                if (need.Length > 0)
                {
                    if (!existing.TryAddCharacters(need, out string miss) && !string.IsNullOrEmpty(miss.Trim()))
                        Debug.LogWarning("[SuccessEndingBuilder] Font missing characters: " + miss);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (source == null) throw new InvalidOperationException("Source font not found: " + SourceFont);
            var asset = TMP_FontAsset.CreateFontAsset(source, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "SuccessEnding_Korean";
            if (!asset.TryAddCharacters(AllCharacters(), out string missing) && !string.IsNullOrEmpty(missing.Trim()))
                Debug.LogWarning("[SuccessEndingBuilder] Font missing characters: " + missing);
            AssetDatabase.CreateAsset(asset, FontPath);
            foreach (var texture in asset.atlasTextures) { texture.name = asset.name + " Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        // overwrite=true 는 이 도구가 만든 SuccessEnding_hyun.unity 만 다시 만듭니다.
        public static string Build(bool overwrite)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null && !overwrite)
                throw new InvalidOperationException("Scene already exists; refusing to overwrite.");
            GetOrCreateFont();
            var original = SceneManager.GetActiveScene();
            // 지금 열린 씬이 이 엔딩 씬이거나 저장 안 된 빈 씬이면 그 자리에서 새로 만들고(저장 후 그대로 열어 둠),
            // 다른 씬이 열려 있으면 그 씬은 건드리지 않고 추가로 만들었다가 닫는다.
            bool single = !original.IsValid() || string.IsNullOrEmpty(original.path) || original.path == ScenePath;
            if (single && original.IsValid() && original.isDirty && SceneManager.sceneCount == 1)
                throw new InvalidOperationException("Active scene has unsaved changes: " + original.path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, single ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // NewScene(Single)은 안 쓰는 에셋을 내리므로 폰트는 새 씬을 만든 뒤에 다시 불러온다
            var font = GetOrCreateFont();
            var subtitleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SubtitleFontPath);
            if (subtitleFont == null) throw new InvalidOperationException("Subtitle font not found: " + SubtitleFontPath);
            try
            {
                var camGo = new GameObject("EndingCamera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                var cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;

                var canvasGo = new GameObject("Canvas_SuccessEnding", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;

                var bg = Stretch(new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image)), canvas.transform);
                var img = bg.GetComponent<UnityEngine.UI.Image>(); img.color = Color.black; img.raycastTarget = false;
                var layer = Stretch(new GameObject("RandomTextLayer", typeof(RectTransform)), canvas.transform);
                var flashGo = Stretch(new GameObject("FlashOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image)), canvas.transform);
                var flash = flashGo.GetComponent<UnityEngine.UI.Image>(); flash.color = new Color(1f, 1f, 1f, 0f); flash.raycastTarget = false;
                var center = Text("CenterLine", canvas.transform, subtitleFont, 34, new Vector2(1600, 200));
                var final = Text("FinalLine", canvas.transform, font, 72, new Vector2(1760, 300));

                var root = new GameObject("SuccessEndingSequence");
                var seq = root.AddComponent<SuccessEndingSequence>();
                AudioSource Src(string name, float volume, bool loop = false, Transform parent = null)
                {
                    var go = new GameObject(name, typeof(AudioSource));
                    go.transform.SetParent(parent != null ? parent : root.transform, false);
                    var s = go.GetComponent<AudioSource>();
                    s.playOnAwake = false; s.volume = volume; s.spatialBlend = 0; s.loop = loop;
                    return s;
                }
                seq.effects = Src("TelephoneEffects", .6f);
                seq.callLine = Src("CallLineAmbience", .35f, true);
                seq.lineStatic = Src("LineStaticNoise", 0f, true);
                seq.heartbeat = Src("Heartbeat", .8f);
                seq.tension = Src("TensionRiser_CAP-COM-03", 0f);
                seq.riser = Src("ClimaxRiser_CLX-02", .85f);
                var riser14 = Src("CutRiser_CLX-14_1", 0f);
                var riser13 = Src("CutRiser_CLX-13_2", 0f);
                seq.hit = Src("ClimaxHit_v3", 1f);
                // 통화 음성: 프롤로그 CallerVoice와 같은 체인/설정
                // AudioSource(pitch .95) → UncannyVoiceFX → Distortion .38 → Chorus → HighPass 300 → LowPass 3400
                seq.dispatcherVoice = Src("DispatcherVoice_Telephone", .9f);
                var dgo = seq.dispatcherVoice.gameObject;
                var dfx = dgo.AddComponent<NightDuty.PrologueSample.UncannyVoiceFX>();
                var dist = dgo.AddComponent<AudioDistortionFilter>(); dist.distortionLevel = dfx.baseDistortion; dfx.distortion = dist;
                var chorus = dgo.AddComponent<AudioChorusFilter>();
                chorus.dryMix = .8f; chorus.wetMix1 = .25f; chorus.wetMix2 = .2f; chorus.wetMix3 = 0f; chorus.delay = 24f; chorus.rate = .6f; chorus.depth = .08f;
                dgo.AddComponent<AudioHighPassFilter>().cutoffFrequency = 300;
                dgo.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400;
                seq.dispatcherVoice.pitch = dfx.basePitch;
                // 귓가 음성(7~9): 2D, 전화 필터 없음, 리버브 없음. 아주 약한 비인간 레이어 + 저역↓ 고역↑ 컴프레서
                seq.closeVoice = Src("DispatcherVoice_CloseToEar", .9f);
                var cgo = seq.closeVoice.gameObject;
                var cfx = cgo.AddComponent<NightDuty.PrologueSample.UncannyVoiceFX>();
                cfx.basePitch = .97f; cfx.baseDistortion = 0f; cfx.doublingEnabled = true; cfx.doublingMix = .12f; cfx.ringModMix = .04f; cfx.glitchesEnabled = false;
                cgo.AddComponent<CloseVoiceFX>();
                seq.closeVoice.pitch = cfx.basePitch;
                // 19번: 필터·이펙트 전혀 없는 깨끗한 음성
                seq.cleanVoice = Src("DispatcherVoice_Clean", .9f);
                seq.tabletGlitch = Src("TabletGlitch", .8f);
                seq.tinnitus = Src("Tail_Tinnitus", .6f);
                seq.blackout = Src("Tail_Blackout", .35f);
                var pool = new GameObject("CrowdVoicePool").transform; pool.SetParent(root.transform, false);
                seq.crowdVoices = Enumerable.Range(0, 32).Select(i => Src("CrowdVoice_" + i.ToString("00"), .8f, false, pool)).ToArray();

                seq.randomTextLayer = layer.GetComponent<RectTransform>();
                seq.centerText = center; seq.finalText = final; seq.font = font; seq.subtitleFont = subtitleFont; seq.flashOverlay = flash;

                seq.phoneRing = Clip(PrologueAudio + "/Phone_ring_3.wav");
                seq.callConnect = Clip(PrologueAudio + "/Call_connect_1.wav");
                seq.hangUp = null; // 별도 끊김 소리가 없어 Call_connect_1 재사용
                seq.callLineLoop = Clip("Sound/AMB_PROLOGUE_CallLine_40s.wav");
                seq.staticLoop = Clip("Sound/SFX_CCTV_static.wav");
                seq.heartbeatOne = Clip("Sound/SFX_BODY_HeartBeat_One.wav");
                seq.tensionClip = Clip("Sound/CAP-COM-03_1.wav");
                seq.riserClip = Clip("Sound/CLX-02_3.wav");
                seq.riserPeakTime = 3.30f;        // 3.40초부터 소리가 꺾이므로 그 직전(가장 큰 구간)에서 끊는다
                seq.riserVolume = .85f; seq.riserVolumeAtCut = 1f;
                seq.tensionPitch = new Vector2(1f, 1.3f);
                seq.extraCutRisers = new List<CutRiser> {
                    // 쇠 긁는 상승음: 5.0~5.9초가 가장 큼 → 5.8초를 끊김 프레임에
                    new CutRiser { name = "CLX-14_1 쇠 긁힘 상승", source = riser14, clip = Clip("Sound/CLX-14_1.wav"), peakTime = 5.8f, volume = new Vector2(.45f, .95f) },
                    // 아이들 합창 → 비명 상승: 1.8~2.4초가 가장 큼 → 2.3초를 끊김 프레임에
                    new CutRiser { name = "CLX-13_2 합창 비명 상승", source = riser13, clip = Clip("Sound/CLX-13_2.wav"), peakTime = 2.3f, volume = new Vector2(.5f, .85f) },
                };
                seq.notification = Clip("Sound/SFX_TABLET_Notification_Vibrate.wav");
                seq.climaxHit = Clip("Sound/ClimaxHit_v3_교실소년_잡힘_사운드입힘.wav");
                seq.tabletGlitchClip = Clip("Sound/SFX_TABLET_Glitch.wav");
                seq.youDiedClip = Clip("Sound/SFX_RESULT_YouDied.wav");
                seq.tinnitusClip = Clip("Sound/CAP_COM_11_TinnitusTail.wav");
                seq.blackoutClip = Clip("Sound/CAP_COM_16_BlackoutTail.wav");
                // 22~27 사망 암시 사운드 (런타임 소스로 재생, 비워도 Resources/SuccessEndingDeath에서 자동 로드)
                seq.useDeathSoundDesign = true;
                seq.deathNotificationClip = Clip("Resources/SuccessEndingDeath/SFX_DEATH_TabletVibrate_Audible.wav");
                seq.deathHeartbeatClip = Clip("Resources/SuccessEndingDeath/SFX_DEATH_HeartbeatAccel.wav");
                seq.deathBreathClip = Clip("Resources/SuccessEndingDeath/SFX_BODY_RoughBreath_02.wav");
                seq.deathBodyFallClip = Clip("Resources/SuccessEndingDeath/CAP-COM-14_3.wav");
                seq.deathFlashlightClip = Clip("Resources/SuccessEndingDeath/SFX_DEATH_FlashlightDropRoll.wav");

                // 새 담당자 TTS (김현웅이/엔딩, 음성 인식으로 대사 매칭) = Voice/EndV2_xx_Dispatcher.wav
                var tel = EndingVoiceRoute.Telephone;
                seq.callLines = new List<EndingLine> {
                    Line("4", "현장관리팀 파견 담당자입니다.", 3f, tel, .9f, "EndV2_04_Dispatcher.wav"),
                    Line("5", "닷새 동안 단순 점검 업무였지만, 고생 많으셨습니다.", 4f, tel, .9f, "EndV2_05_Dispatcher.wav"),
                    Line("6", "감사합니다.", 2f, tel, .9f, "EndV2_06_Dispatcher.wav"),
                };
                var ear = EndingVoiceRoute.CloseToEar;
                var l7 = Line("7", "뒤를 돌아보지 마십시오.", 2.0f, ear, .8f, "EndV2_07_Dispatcher.wav");
                var l8 = Line("8", "부르는 소리에 대답하지 마십시오.", 1.8f, ear, .8f, "EndV2_08_Dispatcher.wav");
                var l9 = Line("9", "손전등을 끄십시오.", 1.5f, ear, .8f, "EndV2_09_Dispatcher.wav");
                foreach (var l in new[] { l7, l8, l9 }) { l.driveBuildUpAudio = true; l.hideBeforeEnd = .15f; }
                l7.lineNoise = .45f; l7.staticNoise = .06f;
                l8.lineNoise = .5f; l8.staticNoise = .09f; l8.heartbeatInterval = .85f; l8.heartbeatVolume = .6f;
                l9.lineNoise = .55f; l9.staticNoise = .12f; l9.heartbeatInterval = .75f; l9.heartbeatVolume = .7f; l9.tensionVolume = .3f;
                seq.buildUpLines = new List<EndingLine> {
                    l7, l8, l9,
                    //       id    text                 dur  copies spread edges noise static hb   hbVol tension stagger vol  voices
                    Burst("10", "계속 비추십시오.", 1.0f, 1, 0f, false, .58f, .16f, .55f, .8f, .38f, 0f, .9f, "End_10_KeepShining.wav"),
                    Burst("11", "멈추십시오.", 0.9f, 2, .35f, false, .64f, .22f, .48f, .85f, .45f, 0f, .9f, "End_11_Stop.wav"),
                    Burst("12", "대답하지 마십시오.", 0.8f, 3, .3f, false, .7f, .3f, .42f, .9f, .52f, .06f, .75f,
                        "End_12_DontAnswer_A.wav", "End_12_DontAnswer_B.wav", "End_12_DontAnswer_C.wav"),
                    Burst("13", "돌아보지 마십시오.", 0.7f, 6, .5f, true, .76f, .38f, .36f, .95f, .6f, .12f, .75f,
                        "End_13_DontLookBack_A.wav", "End_13_DontLookBack_B.wav", "End_13_DontLookBack_C.wav"),
                    Burst("14", "경비실을 나가지 마십시오.", 0.6f, 6, .4f, true, .82f, .46f, .3f, 1f, .68f, .04f, .8f,
                        "End_14_DontLeaveGuardRoom_A.wav", "End_14_DontLeaveGuardRoom_B.wav", "End_14_DontLeaveGuardRoom_C.wav"),
                };
                // 14번은 '모든 목소리 겹침' 시작, 이어서 지속 플러드(SustainFlood)가 끊김 직전까지 몰아친다
                seq.buildUpLines[7].flood = false;
                seq.buildUpLines[7].alsoPlayEarlierCrowdVoices = true;
                seq.floodDuration = 4.8f;
                seq.floodSpawnRate = new Vector2(10f, 220f);
                seq.floodSpawnCurve = 2f;
                seq.floodMaxTexts = 900;
                seq.floodFontSize = new Vector2(26f, 60f);
                seq.floodFontSizeEnd = new Vector2(20f, 120f);
                seq.floodIncludeBuildUpTexts = true;
                seq.floodVoiceRate = new Vector2(8f, 22f);
                seq.floodMinActiveVoices = 12;
                seq.floodMinActiveVoicesEnd = 20;
                seq.floodVoicePitch = new Vector2(.9f, 1.1f);
                seq.floodVoiceVolume = new Vector2(.5f, .8f);
                seq.floodVoiceMaxStartOffset = .35f;
                seq.floodVoiceIncludeDispatcher = true;
                seq.floodLineNoise = 1f; seq.floodStaticNoise = .85f;
                seq.floodHeartbeatInterval = .14f; seq.floodHeartbeatVolume = 1f; seq.floodTensionVolume = 1f;
                seq.floodTexts = RuleTexts.ToArray();
                var l17 = Line("17", "저희는 규칙을… 보낸 적이 없…", 3.5f, tel, .9f, "EndV2_17_Dispatcher.wav");
                // 깨끗하게 녹음된 17을 엔진에서 깨뜨린다: '보낸' 시작 2.04초(음성 인식+파형), '없' 2.96~3.10초 → 3.06초에 전부 끊김
                l17.breakAtVoiceTime = 2.04f; l17.breakCutAtVoiceTime = 3.06f; l17.breakTextIndex = l17.text.IndexOf("보낸", StringComparison.Ordinal);
                l17.extendToVoice = false;
                seq.afterLines = new List<EndingLine> {
                    Line("16", "……규칙 말씀이십니까?", 2.5f, tel, .9f, "EndV2_16_Dispatcher.wav"),
                    l17,
                };
                seq.cleanLines = new List<EndingLine> {
                    Line("19", "고생하셨습니다.", 2.0f, EndingVoiceRoute.Clean, .85f, "EndV2_19_Dispatcher.wav"),
                };
                seq.tabletCursorText = TabletCursorText; seq.tabletText = TabletText; seq.finalFontSize = 72f;

                if (center.font != subtitleFont || final.font != font || seq.font != font || seq.subtitleFont != subtitleFont)
                    throw new InvalidOperationException("Font reference was lost while building.");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
                return ScenePath;
            }
            finally
            {
                if (!single)
                {
                    if (original.IsValid()) SceneManager.SetActiveScene(original);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject Stretch(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return go;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = box; r.anchoredPosition = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = size; t.enableAutoSizing = false;
            t.color = new Color(.84f, .84f, .84f, 1f);
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 10; t.raycastTarget = false; t.text = "";
            return t;
        }
    }
}

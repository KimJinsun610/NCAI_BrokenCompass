using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NightDuty.Editor
{
    /// <summary>
    /// 번역표 갱신 메뉴(2026-11-22, 로컬라이징 1단계).
    /// <list type="number">
    /// <item>원문을 모은다 — DT CSV(<c>Assets/Resources/DT</c>, xlsx 내보내기 결과)의 번역 대상 칸(<see cref="LocTable.Columns"/>) + 코드의 <c>Loc.T("키", "한국어")</c>.</item>
    /// <item>번역 담당 파일 <c>DesignData/Localization/EN.csv</c>와 합친다(새 줄 추가 · 원문 바뀜 표시 · 사라진 줄 보존).</item>
    /// <item>게임이 읽는 사본 <c>Assets/Resources/Localization/EN.csv</c>로 복사하고 번역을 다시 읽는다.</item>
    /// </list>
    /// DT를 다시 내보냈거나 EN.csv를 고쳤으면 이 메뉴를 한 번 돌린다. 엑셀로 EN.csv를 열어 둔 채면 쓰기가 실패한다 — 닫고 다시.
    /// </summary>
    public static class LocalizationSync
    {
        /// <summary>번역 담당 파일(프로젝트 루트 기준).</summary>
        public const string SourceFile = "DesignData/Localization/EN.csv";

        /// <summary>게임이 읽는 사본(프로젝트 루트 기준).</summary>
        public const string RuntimeFile = "Assets/Resources/" + Loc.ResourcePath + ".csv";

        /// <summary>마지막 갱신 결과(한 줄 요약 + 오류) — 자동화가 읽는다(프로젝트 루트 기준).</summary>
        public const string ReportFile = "Temp/LocalizationSyncReport.txt";

        /// <summary>메뉴. 씬을 여닫느라 오래 걸릴 수 있어 다음 에디터 틱에 돌린다(명령 시간 제한에 걸리지 않게).</summary>
        [MenuItem("Tools/DT/번역 목록 갱신 (EN)")]
        public static void Run()
        {
            EditorApplication.delayCall += RunNow;
        }

        private static void RunNow()
        {
            string report;
            LocTable.Result res = null;
            try
            {
                res = Sync();
                report = res == null ? "실패 — 콘솔 확인" : res + (res.Errors.Count > 0 ? "\n" + string.Join("\n", res.Errors.ToArray()) : string.Empty);
            }
            catch (Exception e)
            {
                report = "예외 — " + e;
                Debug.LogException(e);
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportFile), DateTime.Now.ToString("HH:mm:ss") + " " + report, new UTF8Encoding(false));
            if (res == null) return;
            if (res.Errors.Count > 0) Debug.LogError("[번역] " + report);
            else Debug.Log("[번역] " + res + " — " + SourceFile + " → " + RuntimeFile);
        }

        /// <summary>갱신을 돌린다(메뉴·자동화). 쓰기 실패면 null.</summary>
        public static LocTable.Result Sync()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            List<LocTable.Source> sources = new List<LocTable.Source>();

            string dtDir = Path.Combine(Application.dataPath, "Resources", InstructionDTFolder);
            foreach (KeyValuePair<string, string[]> kv in LocTable.Columns)
            {
                string path = Path.Combine(dtDir, kv.Key + ".csv");
                if (!File.Exists(path))
                {
                    Debug.LogError("[번역] DT CSV 없음: " + path + " — Tools/DT/내보내기 먼저");
                    return null;
                }

                sources.AddRange(LocTable.FromDtCsv(kv.Key, File.ReadAllText(path)));
            }

            List<string> files = new List<string>(Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            foreach (string f in files)
            {
                string rel = f.Substring(root.Length + 1).Replace('\\', '/');
                if (rel.Contains("/NOT_Lonely/") || rel.Contains("/Tests/") || rel.EndsWith("/LocTable.cs", StringComparison.Ordinal)) continue;
                string code = File.ReadAllText(f);
                if (code.IndexOf("Loc.T(", StringComparison.Ordinal) < 0) continue;
                sources.AddRange(LocTable.FromCode(rel, code));
            }

            sources.AddRange(LocTable.FromCatalogs());

            // 로딩 팁 CSV(원문 키)
            string tips = Path.Combine(root, TipsFile);
            if (File.Exists(tips)) sources.AddRange(LocTable.FromCsvColumn(TipsFile, File.ReadAllText(tips), "Text"));

            // 빌드 씬과 그 의존 프리팹·에셋의 문자열 칸(원문 키) — Unity가 읽은 값 그대로라 게임이 화면에 쓰는 글과 같다
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[번역] 플레이 중에는 씬 문구를 모을 수 없습니다 — 플레이를 끄고 다시");
                return null;
            }

            sources.AddRange(FromBuildScenes());

            string src = Path.Combine(root, SourceFile);
            string old = File.Exists(src) ? File.ReadAllText(src) : string.Empty;
            LocTable.Result res = LocTable.Merge(sources, old);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(src));
                File.WriteAllText(src, res.Csv, new UTF8Encoding(false));   // 글 앞에 BOM이 이미 있다(엑셀이 UTF-8로 연다)
                string rt = Path.Combine(root, RuntimeFile);
                Directory.CreateDirectory(Path.GetDirectoryName(rt));
                File.WriteAllText(rt, res.Csv, new UTF8Encoding(false));
            }
            catch (IOException e)
            {
                Debug.LogError("[번역] 파일을 쓰지 못했습니다(엑셀로 열려 있으면 닫고 다시): " + e.Message);
                return null;
            }

            AssetDatabase.ImportAsset(RuntimeFile);
            Loc.Reload();
            return res;
        }

        private const string InstructionDTFolder = "DT";

        /// <summary>씬 의존에 안 잡히지만 화면 글이 있는 에셋(Resources.Load로 읽음) — CCTV 채널 이름.</summary>
        public static readonly string[] ExtraAssets = { "Assets/_Game/Resources/CctvConfig.asset" };

        /// <summary>로딩 팁 CSV(프로젝트 루트 기준).</summary>
        public const string TipsFile = "Assets/0. Main/06 Data/LoadingTips.csv";

        /// <summary>
        /// 문자열 칸 중 화면 글이 아닌 것(「형식이름.칸경로」에 이 글이 들어 있으면 건너뜀) — 이름·메모 · 엔딩 효과음 이름(extraCutRisers) ·
        /// 결과창 개발용 가짜 데이터(fakeData) · 태블릿 프리팹 견본 문자(밤마다 비움) · 쓰이지 않는 예비 팁 배열(팁은 CSV — LoadingTipTable.tipCsv).
        /// </summary>
        private static readonly string[] SkipFields = { "m_Name", "note", "memo", "desc", "comment", "m_EditorClassIdentifier", "Riser", "fakeData", "TabletMessageList.messages", "LoadingTipTable.tips", "ResultController.preview" };

        /// <summary>
        /// 빌드 씬(켜진 것)과 그 의존 프리팹·에셋의 한글 문자열 칸을 모은다(<see cref="Loc.K"/>로 쓰이는 글).
        /// 열려 있지 않은 씬은 잠깐 추가로 열었다가 닫는다(저장하지 않는다 — 읽기만).
        /// </summary>
        private static List<LocTable.Source> FromBuildScenes()
        {
            List<LocTable.Source> list = new List<LocTable.Source>();
            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);
            }

            // 의존 프리팹·에셋
            HashSet<string> assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (string dep in AssetDatabase.GetDependencies(scenes.ToArray(), true))
            {
                if (dep.Contains("/NOT_Lonely/") || !dep.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (dep.EndsWith(".prefab", StringComparison.Ordinal) || dep.EndsWith(".asset", StringComparison.Ordinal)) assets.Add(dep);
            }

            foreach (string extra in ExtraAssets) assets.Add(extra);   // Resources로 읽어 씬 의존에 안 잡히는 에셋
            List<string> sortedAssets = new List<string>(assets);
            sortedAssets.Sort(StringComparer.Ordinal);
            foreach (string path in sortedAssets)
            {
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o == null) continue;
                    Collect(list, path, o);
                }
            }

            foreach (string path in scenes)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(path);
                bool opened = false;
                if (!scene.isLoaded)
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                    opened = true;
                }

                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Component c in root.GetComponentsInChildren<Component>(true))
                        {
                            if (c == null) continue;
                            Collect(list, path, c);   // 같은 글은 같은 키라 프리팹과 겹쳐도 한 줄로 모인다
                        }
                    }
                }
                finally
                {
                    if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }

            return list;
        }

        private static void Collect(List<LocTable.Source> list, string path, UnityEngine.Object o)
        {
            SerializedObject so;
            try { so = new SerializedObject(o); }
            catch (Exception) { return; }

            SerializedProperty p = so.GetIterator();
            bool enter = true;
            while (p.Next(enter))
            {
                enter = p.propertyType != SerializedPropertyType.String;
                if (p.propertyType != SerializedPropertyType.String) continue;
                string name = o.GetType().Name + "." + p.propertyPath;
                bool skip = false;
                foreach (string f in SkipFields)
                {
                    if (name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) skip = true;
                }

                if (skip) continue;
                LocTable.Source s = LocTable.FromText(path + " · " + o.GetType().Name + "." + p.propertyPath, p.stringValue);
                if (s != null) list.Add(s);
            }
        }
    }
}

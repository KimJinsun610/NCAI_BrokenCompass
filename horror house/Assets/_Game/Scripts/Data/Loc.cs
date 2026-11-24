using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>게임 글의 언어. 값은 Flow의 <c>GameLanguage.Lang</c>과 같다(0 한국어 · 1 영어).</summary>
    public enum TextLang
    {
        Korean = 0,
        English = 1,
    }

    /// <summary>
    /// 화면 글의 언어와 번역 조회(2026-11-22, 로컬라이징 1단계).
    /// <list type="bullet">
    /// <item><b>한국어가 정본</b>이다 — 한국어 글은 DT(xlsx → CSV)와 코드에 그대로 있고, 이 클래스는 그 한국어를 받아 지금 언어의 글을 돌려준다.</item>
    /// <item>영어는 <c>DesignData/Localization/EN.csv</c>(번역 담당이 고침) → 메뉴 「Tools/DT/번역 목록 갱신 (EN)」 → <see cref="ResourcePath"/>.
    /// 칸: Key · 상태 · KO_원문 · EN · 현재_KO · 메모.</item>
    /// <item><b>폴백:</b> 영어가 비었거나, 번역할 때의 한국어(KO_원문)와 지금 한국어가 다르면(원문이 바뀜) 한국어를 그대로 보인다.</item>
    /// <item>키: DT 글은 「행ID.칸이름」(예 <c>110001.Text</c> — DT ID는 모든 표에서 겹치지 않는다), 코드 글은 「영역.이름」(예 <c>duty.trust</c>).
    /// 코드 글은 반드시 <c>Loc.T("키", "한국어")</c>처럼 <b>두 인자를 글자 그대로</b> 적는다 — 번역 목록 도구가 소스에서 이 모양을 찾아 목록에 올린다.</item>
    /// <item>언어 값은 Flow의 <c>GameLanguage</c>가 넣는다(Core는 Flow를 모른다). 바뀌면 <see cref="Changed"/> — 태블릿이 다시 그린다.</item>
    /// </list>
    /// </summary>
    public static class Loc
    {
        /// <summary>번역표 Resources 경로(확장자 없이).</summary>
        public const string ResourcePath = "Localization/EN";

        private static TextLang s_current = TextLang.Korean;
        private static Dictionary<string, Entry> s_en;
        private static readonly object Gate = new object();

        internal struct Entry
        {
            public string Korean;
            public string English;
        }

        /// <summary>언어가 바뀔 때(같은 값이면 오지 않는다).</summary>
        public static event Action Changed;

        /// <summary>지금 언어.</summary>
        public static TextLang Current
        {
            get { return s_current; }
        }

        /// <summary>한국어인지(번역을 찾지 않는다).</summary>
        public static bool IsKorean
        {
            get { return s_current == TextLang.Korean; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_current = TextLang.Korean;
            Changed = null;
            lock (Gate) s_en = null;   // 플레이마다 번역표를 새로 읽는다(에디터에서 도구를 돌린 뒤 바로 반영)
            s_koToEn = null;
            s_enToKo = null;
            s_pairsFrom = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 2026-11-23: 플레이를 끄면 에디터는 한국어(정본)로 돌아온다 — 도메인 리로드 없이 플레이를 끄면 플레이 중 언어(저장된 English 등)가
        /// 에디터에 남아 EditMode 시험·도구가 영어로 돌았다.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookPlayModeExit()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.EnteredEditMode) s_current = TextLang.Korean;
        }
#endif

        /// <summary>언어를 바꾼다. 같으면 아무 일도 없다.</summary>
        public static void Set(TextLang lang)
        {
            if (lang == s_current) return;
            s_current = lang;
            if (Changed != null) Changed();
        }

        /// <summary>DT 글 — 「행ID.칸」의 지금 언어 글. <paramref name="korean"/>은 DT의 한국어(정본).</summary>
        public static string Dt(uint id, string column, string korean)
        {
            if (IsKorean || id == 0 || string.IsNullOrEmpty(korean)) return korean;
            return Find(id + "." + column, korean);
        }

        /// <summary>DT 글(ID를 글자로 — 「110001」 같은 숫자, 또는 표시 코드).</summary>
        public static string Dt(string id, string column, string korean)
        {
            if (IsKorean || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(korean)) return korean;
            uint n;
            if (!uint.TryParse(id, out n)) n = DesignData.IdOf(id);
            return Dt(n, column, korean);
        }

        /// <summary>코드 글. 반드시 <c>Loc.T("키", "한국어")</c>처럼 글자 그대로 적는다(번역 목록 도구가 찾는 모양).</summary>
        public static string T(string key, string korean)
        {
            if (IsKorean || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(korean)) return korean;
            return Find(key, korean);
        }

        /// <summary>
        /// 씬·프리팹·에셋·CSV에 저장된 글(코드에 글자로 없는 글) — 원문 자체로 번역을 찾는다(키 = <see cref="KeyOf"/>).
        /// 번역 목록 도구가 빌드 씬과 그 의존 에셋의 문자열 칸, 로딩 팁 CSV에서 원문을 모은다. 원문이 바뀌면 새 키가 된다(옛 줄은 「사라짐」으로 남음).
        /// </summary>
        public static string K(string korean)
        {
            if (IsKorean || string.IsNullOrEmpty(korean)) return korean;
            return Find(KeyOf(korean), korean);
        }

        /// <summary>원문 글의 번역 키 — 「ko.」 + FNV-1a 32비트(줄바꿈 \r\n은 \n으로 맞춘 글). 도구와 게임이 같은 함수를 쓴다.</summary>
        public static string KeyOf(string korean)
        {
            string s = korean == null ? string.Empty : korean.Replace("\r\n", "\n");
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }

            return "ko." + h.ToString("x8");
        }

        private static Dictionary<string, string> s_koToEn, s_enToKo;

        /// <summary>
        /// 화면에 이미 쓰인 글을 지금 언어로 — 번역표에 있는 한국어면 영어로, 그 영어면 한국어로(언어에 맞는 쪽). 표에 없는 글이면 null.
        /// 씬에 저장된 라벨·한 번만 만드는 안내처럼 「누가 언제 썼는지 모르는 글」을 바꿀 때 쓴다(Flow의 글자 바꿈 장치).
        /// </summary>
        public static string Swap(string shown)
        {
            if (string.IsNullOrEmpty(shown)) return null;
            EnsurePairs();
            string n = shown.Replace("\r\n", "\n");
            string other;
            if (IsKorean) return s_enToKo.TryGetValue(n, out other) ? other : null;
            return s_koToEn.TryGetValue(n, out other) ? other : null;
        }

        private static void EnsurePairs()
        {
            if (s_koToEn != null && ReferenceEquals(s_pairsFrom, Table())) return;
            Dictionary<string, Entry> t = Table();
            Dictionary<string, string> ko = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> en = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Entry> kv in t)
            {
                if (string.IsNullOrEmpty(kv.Value.English) || string.IsNullOrEmpty(kv.Value.Korean)) continue;
                string k = kv.Value.Korean.Replace("\r\n", "\n"), e = kv.Value.English.Replace("\r\n", "\n");
                if (!ko.ContainsKey(k)) ko[k] = e;
                if (!en.ContainsKey(e)) en[e] = k;
            }

            s_koToEn = ko;
            s_enToKo = en;
            s_pairsFrom = t;
        }

        private static object s_pairsFrom;

        /// <summary>그 키의 영어가 지금 한국어에 맞게 있는지(검수·도구).</summary>
        public static bool HasEnglish(string key, string korean)
        {
            Entry e;
            return Table().TryGetValue(key, out e) && Usable(e, korean);
        }

        /// <summary>번역표를 다시 읽는다(도구가 표를 고친 뒤).</summary>
        public static void Reload()
        {
            lock (Gate) s_en = null;
            if (!IsKorean && Changed != null) Changed();
        }

        private static string Find(string key, string korean)
        {
            Entry e;
            return Table().TryGetValue(key, out e) && Usable(e, korean) ? e.English : korean;
        }

        // 원문이 바뀐 줄(KO_원문 ≠ 지금 한국어)은 낡은 번역이라 쓰지 않는다. 줄바꿈 표기 차이(\r\n)만은 같게 본다.
        private static bool Usable(Entry e, string korean)
        {
            if (string.IsNullOrEmpty(e.English)) return false;
            return string.Equals(Norm(e.Korean), Norm(korean), StringComparison.Ordinal);
        }

        private static string Norm(string s)
        {
            return s == null ? string.Empty : s.Replace("\r\n", "\n").Trim();
        }

        private static Dictionary<string, Entry> Table()
        {
            Dictionary<string, Entry> t = s_en;
            if (t != null) return t;
            lock (Gate)
            {
                if (s_en != null) return s_en;
                s_en = Parse(ReadText());
                return s_en;
            }
        }

        private static string ReadText()
        {
#if UNITY_EDITOR
            // 에디터: 파일에서 바로 읽는다(DesignData와 같은 이유 — 어느 시점에 불려도 Resources.Load 제약에 걸리지 않게).
            string path = System.IO.Path.Combine(Application.dataPath, "Resources", ResourcePath + ".csv");
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
#else
            TextAsset ta = Resources.Load<TextAsset>(ResourcePath);
            return ta != null ? ta.text : string.Empty;
#endif
        }

        /// <summary>번역표 CSV(머리 한 줄: Key·상태·KO_원문·EN·…)를 읽는다. 시험도 쓴다.</summary>
        internal static Dictionary<string, Entry> Parse(string text)
        {
            Dictionary<string, Entry> map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return map;
            List<string[]> rows = InstructionDT.DTCsv.Parse(text);
            if (rows.Count == 0) return map;
            string[] head = rows[0];
            int key = Array.IndexOf(head, "Key"), ko = Array.IndexOf(head, "KO_원문"), en = Array.IndexOf(head, "EN");
            if (key < 0 || ko < 0 || en < 0) return map;
            for (int i = 1; i < rows.Count; i++)
            {
                string[] r = rows[i];
                if (r.Length <= key || r.Length <= en || string.IsNullOrEmpty(r[key])) continue;
                map[r[key]] = new Entry { Korean = ko < r.Length ? r[ko] : string.Empty, English = r[en] };
            }

            return map;
        }

        /// <summary>시험용: 번역표를 글로 바로 넣는다(null이면 파일에서 다시 읽게 비운다).</summary>
        internal static void SetTableForTest(string csv)
        {
            lock (Gate) s_en = csv == null ? null : Parse(csv);
        }
    }
}

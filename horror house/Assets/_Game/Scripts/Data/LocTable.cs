using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NightDuty
{
    /// <summary>
    /// 번역표(EN.csv) 갱신 규칙 — 파일을 읽고 쓰는 일은 에디터 메뉴(「Tools/DT/번역 목록 갱신 (EN)」)가 하고, 여기는 글만 다룬다(시험 가능).
    /// <list type="bullet">
    /// <item>번역할 원문 = DT의 <see cref="Columns"/> 칸(비어 있지 않은 것) + 코드의 <c>Loc.T("키", "한국어")</c>.</item>
    /// <item>새 원문은 줄을 더하고(상태 「번역 없음」), 번역할 때의 한국어(KO_원문)가 지금과 다르면 「원문 바뀜」 + 현재_KO에 지금 글.
    /// 원문에서 사라진 키는 지우지 않고 「사라짐」(번역을 잃지 않게).</item>
    /// <item>번역 담당은 EN 칸만 채우고, 원문이 바뀐 줄을 다시 번역했으면 KO_원문을 현재_KO로 바꾼다(그래야 게임이 그 영어를 쓴다 — <see cref="Loc"/>).</item>
    /// </list>
    /// </summary>
    public static class LocTable
    {
        /// <summary>번역 대상 DT 칸(표 이름 → 칸들). 판정·키로 쓰이는 칸(Fx Param 이름 등)과 기획 메모(Desc)는 넣지 않는다.</summary>
        public static readonly KeyValuePair<string, string[]>[] Columns =
        {
            new KeyValuePair<string, string[]>("Rule_Table", new[] { "Text", "Variant_Text", "Paradox_Text", "Blank_Answer", "Blank_Decoded", "Blank_Decoded_Variant", "Trap_Text" }),
            new KeyValuePair<string, string[]>("Task_Table", new[] { "Text", "Done_Reply", "Missed_Reply" }),
            new KeyValuePair<string, string[]>("Message_Table", new[] { "Text" }),
            new KeyValuePair<string, string[]>("Ending_Table", new[] { "Final_Text" }),
        };

        /// <summary>번역표 머리(칸 순서).</summary>
        public static readonly string[] Header = { "Key", "상태", "KO_원문", "EN", "현재_KO", "출처", "메모" };

        public const string StateTodo = "번역 없음";
        public const string StateChanged = "원문 바뀜";
        public const string StateDone = "완료";
        public const string StateGone = "사라짐";

        /// <summary>원문 한 줄.</summary>
        public sealed class Source
        {
            public string Key;
            public string Korean;
            public string Origin;
        }

        /// <summary>갱신 결과.</summary>
        public sealed class Result
        {
            public string Csv;
            public int Total, Done, Todo, Changed, Gone, Added;
            public readonly List<string> Errors = new List<string>();

            public override string ToString()
            {
                return "번역표 " + Total + "줄 — 완료 " + Done + " · 번역 없음 " + Todo + " · 원문 바뀜 " + Changed + " · 사라짐 " + Gone + " (새로 더함 " + Added + ")"
                    + (Errors.Count > 0 ? " · 오류 " + Errors.Count : string.Empty);
            }
        }

        /// <summary>DT CSV 하나(내보낸 글)에서 번역 대상 원문을 뽑는다. 표가 <see cref="Columns"/>에 없으면 빈 목록.</summary>
        public static List<Source> FromDtCsv(string sheet, string csv)
        {
            List<Source> list = new List<Source>();
            string[] cols = null;
            foreach (KeyValuePair<string, string[]> kv in Columns)
            {
                if (kv.Key == sheet) cols = kv.Value;
            }

            if (cols == null || string.IsNullOrEmpty(csv)) return list;
            List<string[]> rows = InstructionDT.DTCsv.Parse(csv);
            if (rows.Count < 2) return list;
            string[] head = rows[0];
            int id = Array.IndexOf(head, "ID");
            if (id < 0) return list;
            for (int r = 2; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (row.Length <= id || string.IsNullOrEmpty(row[id])) continue;
                foreach (string c in cols)
                {
                    int i = Array.IndexOf(head, c);
                    if (i < 0 || i >= row.Length || string.IsNullOrEmpty(row[i])) continue;
                    list.Add(new Source { Key = row[id] + "." + c, Korean = row[i], Origin = sheet + "." + c });
                }
            }

            return list;
        }

        /// <summary>코드 카탈로그가 키를 조합해 쓰는 글(점검 항목 짧은 이름 — <see cref="InspectionItem.ShownName"/>).</summary>
        public static List<Source> FromCatalogs()
        {
            List<Source> list = new List<Source>();
            foreach (InspectionItem item in InspectionCatalog.All)
            {
                if (item == null || string.IsNullOrEmpty(item.Name) || InspectionCatalog.IsRetired(item.Id)) continue;
                list.Add(new Source { Key = InspectionItem.NameKey(item.Id), Korean = item.Name, Origin = "InspectionCatalog.Name" });
            }

            return list;
        }

        /// <summary>원문 자체가 키인 글(<see cref="Loc.K"/>) — 씬·프리팹·에셋의 문자열 칸, CSV 칸. 한글이 없는 글은 건너뛴다.</summary>
        public static Source FromText(string origin, string korean)
        {
            if (string.IsNullOrEmpty(korean) || !HasHangul(korean)) return null;
            string text = korean.Replace("\r\n", "\n");
            return new Source { Key = Loc.KeyOf(text), Korean = text, Origin = origin };
        }

        /// <summary>CSV(머리 한 줄, 「#」 줄은 주석) 한 칸의 글을 원문 키로 모은다 — 로딩 팁.</summary>
        public static List<Source> FromCsvColumn(string origin, string csv, string column)
        {
            List<Source> list = new List<Source>();
            if (string.IsNullOrEmpty(csv)) return list;
            List<string[]> rows = InstructionDT.DTCsv.Parse(csv);
            if (rows.Count == 0) return list;
            int c = Array.IndexOf(rows[0], column);
            if (c < 0) return list;
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (row.Length == 0 || row[0].StartsWith("#", StringComparison.Ordinal) || c >= row.Length) continue;
                Source s = FromText(origin, row[c]);
                if (s != null) list.Add(s);
            }

            return list;
        }

        /// <summary>한글(음절·자모)이 있는지.</summary>
        public static bool HasHangul(string s)
        {
            if (s == null) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if ((ch >= '가' && ch <= '힣') || (ch >= 'ㄱ' && ch <= 'ㆎ')) return true;
            }

            return false;
        }

        private static readonly Regex CodeCall = new Regex("Loc\\.T\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*,\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*\\)", RegexOptions.Compiled);

        /// <summary>C# 소스 하나에서 <c>Loc.T("키", "한국어")</c>를 뽑는다.</summary>
        public static List<Source> FromCode(string file, string code)
        {
            List<Source> list = new List<Source>();
            if (string.IsNullOrEmpty(code)) return list;
            foreach (string line in code.Split('\n'))
            {
                string t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal)) continue;   // 주석 속 예시는 원문이 아니다
                foreach (Match m in CodeCall.Matches(line))
                {
                    list.Add(new Source { Key = Unescape(m.Groups[1].Value), Korean = Unescape(m.Groups[2].Value), Origin = file });
                }
            }

            return list;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    default: sb.Append(n); break;
                }
            }

            return sb.ToString();
        }

        /// <summary>지금 원문 목록과 옛 번역표를 합쳐 새 번역표를 만든다.</summary>
        public static Result Merge(IList<Source> sources, string oldCsv)
        {
            Result res = new Result();

            // 옛 표: 키 → 칸들
            Dictionary<string, Dictionary<string, string>> old = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            List<string> oldOrder = new List<string>();
            if (!string.IsNullOrEmpty(oldCsv))
            {
                List<string[]> rows = InstructionDT.DTCsv.Parse(oldCsv);
                if (rows.Count > 0)
                {
                    string[] head = rows[0];
                    for (int r = 1; r < rows.Count; r++)
                    {
                        Dictionary<string, string> m = new Dictionary<string, string>(StringComparer.Ordinal);
                        for (int c = 0; c < head.Length && c < rows[r].Length; c++) m[head[c]] = rows[r][c];
                        string key;
                        if (!m.TryGetValue("Key", out key) || string.IsNullOrEmpty(key) || old.ContainsKey(key)) continue;
                        old[key] = m;
                        oldOrder.Add(key);
                    }
                }
            }

            // 지금 원문: 키 중복 검사(같은 키에 다른 한국어면 오류)
            Dictionary<string, Source> now = new Dictionary<string, Source>(StringComparer.Ordinal);
            List<Source> order = new List<Source>();
            foreach (Source s in sources)
            {
                Source prev;
                if (now.TryGetValue(s.Key, out prev))
                {
                    if (prev.Korean != s.Korean) res.Errors.Add("키 " + s.Key + " — 한국어가 둘(" + prev.Origin + " · " + s.Origin + ")");
                    continue;
                }

                now[s.Key] = s;
                order.Add(s);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append('﻿');
            Line(sb, Header);
            foreach (Source s in order)
            {
                Dictionary<string, string> o;
                string ko = s.Korean, en = string.Empty, memo = string.Empty, current = string.Empty, state;
                if (old.TryGetValue(s.Key, out o))
                {
                    en = Get(o, "EN");
                    memo = Get(o, "메모");
                    string oldKo = Get(o, "KO_원문");
                    if (!string.IsNullOrEmpty(en) && Norm(oldKo) != Norm(s.Korean))
                    {
                        ko = oldKo;   // 번역할 때의 원문을 남긴다 — 번역 담당이 비교해 다시 번역
                        current = s.Korean;
                    }
                }
                else
                {
                    res.Added++;
                }

                if (string.IsNullOrEmpty(en)) { state = StateTodo; res.Todo++; }
                else if (current.Length > 0) { state = StateChanged; res.Changed++; }
                else { state = StateDone; res.Done++; }

                Line(sb, new[] { s.Key, state, ko, en, current, s.Origin, memo });
                res.Total++;
            }

            // 원문에서 사라진 키 — 번역을 잃지 않게 맨 아래에 남긴다(번역도 메모도 없는 줄은 남길 것이 없어 지운다)
            foreach (string key in oldOrder)
            {
                if (now.ContainsKey(key)) continue;
                Dictionary<string, string> o = old[key];
                if (string.IsNullOrEmpty(Get(o, "EN")) && string.IsNullOrEmpty(Get(o, "메모"))) continue;
                Line(sb, new[] { key, StateGone, Get(o, "KO_원문"), Get(o, "EN"), string.Empty, Get(o, "출처"), Get(o, "메모") });
                res.Gone++;
                res.Total++;
            }

            res.Csv = sb.ToString();
            return res;
        }

        private static string Get(Dictionary<string, string> m, string k)
        {
            string v;
            return m.TryGetValue(k, out v) ? v ?? string.Empty : string.Empty;
        }

        private static string Norm(string s)
        {
            return s == null ? string.Empty : s.Replace("\r\n", "\n").Trim();
        }

        private static void Line(StringBuilder sb, string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string v = cells[i] ?? string.Empty;
                if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || v.StartsWith(" ", StringComparison.Ordinal) || v.EndsWith(" ", StringComparison.Ordinal))
                {
                    sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
                }
                else
                {
                    sb.Append(v);
                }
            }

            sb.Append("\r\n");
        }
    }
}

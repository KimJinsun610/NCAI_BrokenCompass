// INSTRUCTION DT Loader (런타임)
// Resources/DT/<시트이름>.csv (DTExporter 가 씀) → 표별 DTTable<행>.
// 사용:
//   DT.LoadAll();                                  // 게임 시작 시 1번
//   var rule = DT.Rule.Get(110004);                // ID 로 한 행
//   foreach (var t in DT.Task.All) { ... }         // 전체 행(엑셀 순서)
//   string n = DT.EnumName(520105);                // enum ID → 이름
//   float v = DT.SettingValue(390001);             // 설정값
//   IDTRow any = DT.Find(330002);                  // 어느 표인지 몰라도 ID 로 찾기
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace InstructionDT
{
    public interface IDTRow { uint Id { get; } }

    // ───────────────────────── CSV ─────────────────────────
    public sealed class DTCsv
    {
        public readonly string Name;
        public readonly string[] Columns, Types;
        public readonly List<string[]> Rows = new List<string[]>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        public DTCsv(string name, string text)
        {
            Name = name;
            var all = Parse(text);
            if (all.Count < 2) throw new FormatException(name + ": CSV 머리 2줄(칸 이름·자료형)이 없음");
            Columns = all[0]; Types = all[1];
            for (int i = 0; i < Columns.Length; i++) _index[Columns[i]] = i;
            for (int i = 2; i < all.Count; i++) Rows.Add(all[i]);
        }

        public int IndexOf(string column)
        {
            int i;
            if (!_index.TryGetValue(column, out i)) throw new FormatException(Name + ": 칸 '" + column + "' 이 CSV 에 없음 — 다시 내보낼 것");
            return i;
        }

        /// RFC 4180 (따옴표 안 쉼표·따옴표("")·줄바꿈 허용)
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder();
            bool q = false; int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1;
            for (; i < text.Length; i++)
            {
                char ch = text[i];
                if (q)
                {
                    if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; }
                    else cell.Append(ch);
                }
                else if (ch == '"') q = true;
                else if (ch == ',') { row.Add(cell.ToString()); cell.Length = 0; }
                else if (ch == '\r') { }
                else if (ch == '\n') { row.Add(cell.ToString()); cell.Length = 0; rows.Add(row.ToArray()); row.Clear(); }
                else cell.Append(ch);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }

        /// 한 행 읽기 도우미(생성 코드가 씀). 빈칸이면 기본값 + blank 목록에 칸 이름 추가.
        public struct Row
        {
            readonly DTCsv _csv; readonly string[] _v;
            public Row(DTCsv csv, string[] v) { _csv = csv; _v = v; }
            string Raw(string col, ref HashSet<string> blank)
            {
                int i = _csv.IndexOf(col);
                var s = i < _v.Length ? _v[i] : "";
                if (s.Length == 0) { if (blank == null) blank = new HashSet<string>(); blank.Add(col); }
                return s;
            }
            public uint U(string c, ref HashSet<string> b) { var s = Raw(c, ref b); return s.Length == 0 ? 0u : uint.Parse(s, CultureInfo.InvariantCulture); }
            public int I(string c, ref HashSet<string> b) { var s = Raw(c, ref b); return s.Length == 0 ? 0 : int.Parse(s, CultureInfo.InvariantCulture); }
            public float F(string c, ref HashSet<string> b) { var s = Raw(c, ref b); return s.Length == 0 ? 0f : float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
            public bool B(string c, ref HashSet<string> b) { var s = Raw(c, ref b); return s == "TRUE"; }
            public string S(string c, ref HashSet<string> b) { return Raw(c, ref b); }
        }
    }

    // ───────────────────────── 표 ─────────────────────────
    public sealed class DTTable<T> where T : class, IDTRow
    {
        public readonly string Sheet;
        readonly List<T> _rows;
        readonly Dictionary<uint, T> _byId;
        public IReadOnlyList<T> All { get { return _rows; } }
        public int Count { get { return _rows.Count; } }

        DTTable(string sheet, List<T> rows)
        {
            Sheet = sheet; _rows = rows; _byId = new Dictionary<uint, T>(rows.Count);
            foreach (var r in rows)
            {
                if (_byId.ContainsKey(r.Id)) throw new FormatException(sheet + ": ID " + r.Id + " 중복");
                _byId[r.Id] = r;
            }
        }

        internal static DTTable<T> Build(string sheet, DTCsv csv, Func<DTCsv.Row, T> parse)
        {
            var list = new List<T>(csv.Rows.Count);
            for (int i = 0; i < csv.Rows.Count; i++)
            {
                try { list.Add(parse(new DTCsv.Row(csv, csv.Rows[i]))); }
                catch (FormatException e) { throw new FormatException(sheet + " " + (i + 3) + "번째 줄: " + e.Message, e); }
            }
            return new DTTable<T>(sheet, list);
        }

        /// 없으면 null (0 = 빈 참조도 null)
        public T Get(uint id) { T r; return id != 0 && _byId.TryGetValue(id, out r) ? r : null; }
        public bool TryGet(uint id, out T row) { return _byId.TryGetValue(id, out row); }
        public bool Contains(uint id) { return _byId.ContainsKey(id); }
        public List<T> Where(Predicate<T> match) { return _rows.FindAll(match); }
    }

    // ───────────────────────── 진입점 ─────────────────────────
    public static partial class DT
    {
        public const string ResourceFolder = "DT";   // Assets/Resources/DT
        public static bool Loaded { get; private set; }
        static readonly Dictionary<uint, IDTRow> _all = new Dictionary<uint, IDTRow>();
        static readonly Dictionary<uint, string> _enumName = new Dictionary<uint, string>();
        static readonly Dictionary<uint, string> _enumCat = new Dictionary<uint, string>();

        /// Resources 에서 전부 읽음. 다시 부르면 새로 읽음(에디터에서 수정 후 재시작 없이 확인할 때).
        public static void LoadAll()
        {
            LoadAll(name =>
            {
                var ta = Resources.Load<TextAsset>(ResourceFolder + "/" + name);
                if (ta == null) throw new InvalidOperationException("Resources/" + ResourceFolder + "/" + name + ".csv 없음 — Tools/DT/내보내기 실행");
                return ta.text;
            });
        }

        /// CSV 문자열을 주는 함수를 직접 넘김(테스트, 어드레서블, 파일 등).
        public static void LoadAll(Func<string, string> readCsv)
        {
            _all.Clear(); _enumName.Clear(); _enumCat.Clear(); Loaded = false;
            LoadGenerated(name => new DTCsv(name, readCsv(name)));
            Loaded = true;
            Debug.Log("[DT] 표 " + TableCount + "개, ID " + _all.Count + "개 읽음");
        }

        static void Register(params object[] tables)
        {
            foreach (var t in tables)
                foreach (IDTRow r in (System.Collections.IEnumerable)t.GetType().GetProperty("All").GetValue(t, null))
                {
                    if (_all.ContainsKey(r.Id)) throw new FormatException("ID " + r.Id + " 가 두 표에 있음");
                    _all[r.Id] = r;
                }
        }

        static void RegisterEnums(params object[] tables)
        {
            foreach (var t in tables)
                foreach (var r in (System.Collections.IEnumerable)t.GetType().GetProperty("All").GetValue(t, null))
                {
                    var ty = r.GetType();
                    uint id = (uint)ty.GetField("ID").GetValue(r);
                    _enumName[id] = (string)ty.GetField("Name").GetValue(r);
                    _enumCat[id] = (string)ty.GetField("Category").GetValue(r);
                }
        }

        /// 어느 표의 ID 든 찾음. 없으면 null.
        public static IDTRow Find(uint id) { IDTRow r; return id != 0 && _all.TryGetValue(id, out r) ? r : null; }
        public static T Find<T>(uint id) where T : class, IDTRow { return Find(id) as T; }

        /// enum ID → 이름(예: 520105 → "응시 각도"). 없으면 "".
        public static string EnumName(uint id) { string s; return _enumName.TryGetValue(id, out s) ? s : ""; }
        /// enum ID → 구분(예: "판정 방식")
        public static string EnumCategory(uint id) { string s; return _enumCat.TryGetValue(id, out s) ? s : ""; }
        /// 구분에 든 enum ID 들(엑셀 순서)
        public static List<uint> EnumIds(string category)
        {
            var l = new List<uint>();
            foreach (var kv in _enumCat) if (kv.Value == category) l.Add(kv.Key);
            l.Sort(); return l;
        }

        /// 설정값. 없거나 빈칸이면 fallback.
        public static float SettingValue(uint id, float fallback = 0f)
        {
            var s = Setting.Get(id);
            return s == null || s.IsBlank("Value") ? fallback : s.Value;
        }
        public static bool SettingBool(uint id, bool fallback = false)
        {
            var s = Setting.Get(id);
            return s == null || s.IsBlank("Value") ? fallback : s.Value != 0f;
        }
    }
}

// INSTRUCTION DT Exporter (Unity Editor)
// xlsx(기획 원본) → 검사 → CSV(Assets/Resources/DT) + C# 행 클래스(Assets/DT/Generated)
// 외부 DLL 없이 System.IO.Compression + System.Xml 로 xlsx 를 직접 읽음.
//
// 시트 형식(모든 표 공통)
//   A1 = 표 제목(한글), A2 "#DESC" / A3 "종류" / A4 "Data_type"
//   B2.. = 칸 설명(한글), B3.. = 칸 이름(영문), B4.. = 자료형(uint|int|float|bool|string)
//   B5.. = 데이터. B열 = ID(6자리, 모든 표 통틀어 중복 금지)
//   시트 이름이 "_Table" 또는 "_enum_table" 로 끝나는 것만 내보냄(메모 시트는 무시).
//
// 검사
//   1) 자료형: uint(0 이상 정수) / int(정수) / float / bool(TRUE·FALSE·1·0) / string, 빈칸 허용
//   2) ID: 비었으면 오류(데이터가 있는 행), 6자리, 모든 표 통틀어 중복 금지
//   3) 참조: 이름이 _ID 로 끝나거나 Day_From/Day_To 인 uint 칸 → 값이 어떤 표의 ID 여야 함,
//            그리고 한 칸의 값들은 모두 같은 표를 가리켜야 함(ID 앞 2자리로 표 판별)
//   오류가 하나라도 있으면 파일을 쓰지 않음.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace InstructionDT.EditorTools
{
    public class DTColumn { public string Name, Type, Desc; public int Index; }

    public class DTSheet
    {
        public string Name, Title;
        public List<DTColumn> Columns = new List<DTColumn>();
        public List<string[]> Rows = new List<string[]>();   // 정규화된 문자열(빈칸 = "")
        public List<int> ExcelRow = new List<int>();
        public bool IsEnum { get { return Name.EndsWith("_enum_table"); } }
    }

    public class DTReport
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<DTSheet> Sheets = new List<DTSheet>();
        public bool Ok { get { return Errors.Count == 0; } }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("표 {0}개, 행 {1}개, 오류 {2}, 경고 {3}", Sheets.Count, Sheets.Sum(s => s.Rows.Count), Errors.Count, Warnings.Count));
            foreach (var e in Errors) sb.AppendLine("[오류] " + e);
            foreach (var w in Warnings) sb.AppendLine("[경고] " + w);
            return sb.ToString();
        }
    }

    // ───────────────────────── xlsx 읽기 ─────────────────────────
    public static class XlsxReader
    {
        const string NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RNS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// 시트 이름 → (행 번호, 열 번호) → 문자열 값. 숫자는 xlsx 저장값 그대로, bool 은 "TRUE"/"FALSE".
        public static Dictionary<string, Dictionary<long, string>> Read(string path, out List<string> order)
        {
            var result = new Dictionary<string, Dictionary<long, string>>();
            order = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) // 엑셀이 열어 둔 상태에서도 읽기
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var shared = new List<string>();
                var sst = zip.GetEntry("xl/sharedStrings.xml");
                if (sst != null)
                {
                    var d = Load(sst);
                    foreach (XmlElement si in d.GetElementsByTagName("si", NS)) shared.Add(InnerText(si));
                }
                var wb = Load(zip.GetEntry("xl/workbook.xml"));
                var rels = Load(zip.GetEntry("xl/_rels/workbook.xml.rels"));
                var target = new Dictionary<string, string>();
                foreach (XmlElement r in rels.GetElementsByTagName("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"))
                {
                    var t = r.GetAttribute("Target");
                    t = t.StartsWith("/") ? t.Substring(1) : "xl/" + t;
                    target[r.GetAttribute("Id")] = t;
                }
                foreach (XmlElement sh in wb.GetElementsByTagName("sheet", NS))
                {
                    var name = sh.GetAttribute("name");
                    var entry = zip.GetEntry(target[sh.GetAttribute("id", RNS)]);
                    var cells = new Dictionary<long, string>();
                    var d = Load(entry);
                    foreach (XmlElement c in d.GetElementsByTagName("c", NS))
                    {
                        int row, col; Ref(c.GetAttribute("r"), out row, out col);
                        string t = c.GetAttribute("t"), v = null;
                        var ve = c.GetElementsByTagName("v", NS);
                        string raw = ve.Count > 0 ? ve[0].InnerText : null;
                        if (t == "s") v = raw == null ? null : shared[int.Parse(raw, CultureInfo.InvariantCulture)];
                        else if (t == "inlineStr") { var isel = c.GetElementsByTagName("is", NS); v = isel.Count > 0 ? InnerText((XmlElement)isel[0]) : null; }
                        else if (t == "b") v = raw == null ? null : (raw == "1" ? "TRUE" : "FALSE");
                        else if (t == "e") v = "#ERROR:" + raw;
                        else v = raw;   // n, str(수식 결과) — 수식은 저장된 결과값을 씀
                        if (v != null) cells[Key(row, col)] = v;
                    }
                    result[name] = cells; order.Add(name);
                }
            }
            return result;
        }

        public static long Key(int row, int col) { return ((long)row << 16) | (uint)col; }

        static XmlDocument Load(ZipArchiveEntry e)
        {
            var d = new XmlDocument();
            using (var s = e.Open()) d.Load(s);
            return d;
        }

        static string InnerText(XmlElement si)
        {   // <t> 들만 이어붙임(서식 run 포함, 발음 표기 rPh 제외)
            var sb = new StringBuilder();
            foreach (XmlElement t in si.GetElementsByTagName("t", NS))
                if (t.ParentNode.LocalName != "rPh") sb.Append(t.InnerText);
            return sb.ToString();
        }

        static void Ref(string r, out int row, out int col)
        {
            col = 0; int i = 0;
            while (i < r.Length && char.IsLetter(r[i])) { col = col * 26 + (char.ToUpperInvariant(r[i]) - 'A' + 1); i++; }
            row = int.Parse(r.Substring(i), CultureInfo.InvariantCulture);
        }
    }

    // ───────────────────────── 검사 · 내보내기 ─────────────────────────
    public static class DTExportCore
    {
        static readonly string[] Types = { "uint", "int", "float", "bool", "string" };

        public static bool IsRefColumn(DTColumn c)
        {
            return c.Type == "uint" && c.Index > 0 && (c.Name.EndsWith("_ID") || c.Name == "Day_From" || c.Name == "Day_To");
        }

        public static DTReport Validate(string xlsxPath)
        {
            var rep = new DTReport();
            List<string> order;
            var book = XlsxReader.Read(xlsxPath, out order);
            foreach (var name in order)
            {
                if (!(name.EndsWith("_Table") || name.EndsWith("_enum_table"))) continue;
                var cells = book[name];
                Func<int, int, string> at = (r, c) => { string v; return cells.TryGetValue(XlsxReader.Key(r, c), out v) ? v : null; };
                var sh = new DTSheet { Name = name, Title = at(1, 1) ?? "" };
                if (at(2, 1) != "#DESC" || at(3, 1) != "종류" || at(4, 1) != "Data_type")
                    rep.Errors.Add(name + ": A2~A4 가 #DESC / 종류 / Data_type 이 아님(형식이 다른 시트)");
                for (int c = 2; ; c++)
                {
                    var en = at(3, c);
                    if (string.IsNullOrWhiteSpace(en)) break;
                    var ty = (at(4, c) ?? "").Trim();
                    if (Array.IndexOf(Types, ty) < 0) rep.Errors.Add(string.Format("{0}!{1}4: 자료형 '{2}' 은 uint/int/float/bool/string 이 아님", name, Col(c), ty));
                    if (sh.Columns.Any(x => x.Name == en.Trim())) rep.Errors.Add(string.Format("{0}: 칸 이름 '{1}' 중복", name, en));
                    sh.Columns.Add(new DTColumn { Name = en.Trim(), Type = ty, Desc = at(2, c) ?? "", Index = sh.Columns.Count });
                }
                if (sh.Columns.Count == 0 || sh.Columns[0].Name != "ID") rep.Errors.Add(name + ": B3 이 ID 가 아님");
                int maxRow = cells.Keys.Select(k => (int)(k >> 16)).DefaultIfEmpty(0).Max();
                for (int r = 5; r <= maxRow; r++)
                {
                    var vals = new string[sh.Columns.Count]; bool any = false;
                    for (int i = 0; i < sh.Columns.Count; i++)
                    {
                        var raw = at(r, i + 2);
                        if (raw != null && raw.Trim().Length > 0) any = true;
                        vals[i] = Normalize(raw, sh.Columns[i], string.Format("{0}!{1}{2}", name, Col(i + 2), r), rep);
                    }
                    if (!any) continue;                                   // 빈 행은 건너뜀
                    if (vals[0] == "") { rep.Errors.Add(string.Format("{0}!B{1}: 데이터가 있는데 ID 가 비었음", name, r)); continue; }
                    sh.Rows.Add(vals); sh.ExcelRow.Add(r);
                }
                rep.Sheets.Add(sh);
            }
            // ID: 6자리 · 전체 중복
            var owner = new Dictionary<uint, string>();
            foreach (var sh in rep.Sheets)
                for (int i = 0; i < sh.Rows.Count; i++)
                {
                    uint id;
                    if (!uint.TryParse(sh.Rows[i][0], NumberStyles.None, CultureInfo.InvariantCulture, out id)) continue; // 형식 오류는 위에서 보고
                    if (id < 100000 || id > 999999) rep.Errors.Add(string.Format("{0}!B{1}: ID {2} 가 6자리가 아님", sh.Name, sh.ExcelRow[i], id));
                    string prev;
                    if (owner.TryGetValue(id, out prev)) rep.Errors.Add(string.Format("{0}!B{1}: ID {2} 중복(이미 {3})", sh.Name, sh.ExcelRow[i], id, prev));
                    else owner[id] = sh.Name;
                }
            // 표 앞 2자리 → 표(경고: 한 앞자리를 두 표가 같이 쓰면)
            var prefix = new Dictionary<string, string>();
            foreach (var kv in owner)
            {
                var p = kv.Key.ToString(CultureInfo.InvariantCulture).Substring(0, 2); string o;
                if (prefix.TryGetValue(p, out o) && o != kv.Value) rep.Warnings.Add(string.Format("ID 앞 2자리 {0} 를 {1} 와 {2} 가 같이 씀", p, o, kv.Value));
                else prefix[p] = kv.Value;
            }
            // 참조
            foreach (var sh in rep.Sheets)
                foreach (var c in sh.Columns.Where(IsRefColumn))
                {
                    var targets = new HashSet<string>();
                    for (int i = 0; i < sh.Rows.Count; i++)
                    {
                        var v = sh.Rows[i][c.Index]; uint id;
                        if (v == "" || !uint.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out id)) continue;
                        string t;
                        if (!owner.TryGetValue(id, out t)) rep.Errors.Add(string.Format("{0}!{1}{2}: {3} = {4} 는 어느 표에도 없는 ID", sh.Name, Col(c.Index + 2), sh.ExcelRow[i], c.Name, id));
                        else targets.Add(t);
                    }
                    if (targets.Count > 1) rep.Errors.Add(string.Format("{0}.{1}: 한 칸이 여러 표를 가리킴({2})", sh.Name, c.Name, string.Join(", ", targets.ToArray())));
                }
            return rep;
        }

        static string Normalize(string raw, DTColumn c, string where, DTReport rep)
        {
            if (raw == null) return "";
            if (raw.StartsWith("#ERROR:")) { rep.Errors.Add(where + ": 수식 오류 " + raw.Substring(7)); return ""; }
            var s = c.Type == "string" ? raw.Replace("\r\n", "\n") : raw.Trim();
            if (s.Length == 0) return "";
            double d;
            switch (c.Type)
            {
                case "string": return s;
                case "bool":
                    var u = s.ToUpperInvariant();
                    if (u == "TRUE" || u == "1") return "TRUE";
                    if (u == "FALSE" || u == "0") return "FALSE";
                    rep.Errors.Add(string.Format("{0}: bool 칸에 '{1}'", where, s)); return "";
                case "float":
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) { rep.Errors.Add(string.Format("{0}: float 칸에 '{1}'", where, s)); return ""; }
                    return FormatNumber(d);
                default: // uint, int
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d != Math.Floor(d) || Math.Abs(d) > 4294967295.0)
                    { rep.Errors.Add(string.Format("{0}: {1} 칸에 정수가 아닌 값 '{2}'", where, c.Type, s)); return ""; }
                    if (c.Type == "uint" && d < 0) { rep.Errors.Add(string.Format("{0}: uint 칸에 음수 {1}", where, s)); return ""; }
                    if (c.Type == "int" && (d < int.MinValue || d > int.MaxValue)) { rep.Errors.Add(string.Format("{0}: int 범위 밖 {1}", where, s)); return ""; }
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
        }

        public static string FormatNumber(double d)
        {
            if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Col(int c)
        {
            var s = ""; while (c > 0) { int m = (c - 1) % 26; s = (char)('A' + m) + s; c = (c - 1) / 26; }
            return s;
        }

        // ───── CSV ─────
        public static string ToCsv(DTSheet sh)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", sh.Columns.Select(c => Esc(c.Name)).ToArray())).Append('\n');
            sb.Append(string.Join(",", sh.Columns.Select(c => Esc(c.Type)).ToArray())).Append('\n');
            foreach (var r in sh.Rows) sb.Append(string.Join(",", r.Select(Esc).ToArray())).Append('\n');
            return sb.ToString();
        }

        static string Esc(string v)
        {
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || (v.Length > 0 && (v[0] == ' ' || v[v.Length - 1] == ' ')))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        // ───── C# 생성 ─────
        public static string ClassName(string sheet)
        {   // Rule_Table → RuleRow, Rule_enum_table → RuleEnumRow
            if (sheet.EndsWith("_enum_table")) return sheet.Substring(0, sheet.Length - 11).Replace("_", "") + "EnumRow";
            return sheet.Substring(0, sheet.Length - 6).Replace("_", "") + "Row";
        }

        public static string PropName(string sheet)
        {   // Rule_Table → Rule, Rule_enum_table → RuleEnum
            if (sheet.EndsWith("_enum_table")) return sheet.Substring(0, sheet.Length - 11).Replace("_", "") + "Enum";
            return sheet.Substring(0, sheet.Length - 6).Replace("_", "");
        }

        static string CsType(string t) { return t == "string" ? "string" : t; }

        public static string GenerateRows(List<DTSheet> sheets)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated> DTExporter 가 만든 파일. 직접 고치지 말고 xlsx 를 고친 뒤 다시 내보낼 것.\n");
            sb.Append("using System.Collections.Generic;\n\nnamespace InstructionDT\n{\n");
            foreach (var sh in sheets)
            {
                var cn = ClassName(sh.Name);
                sb.AppendFormat("    /// <summary>{0} ({1})</summary>\n", Xml(sh.Title), sh.Name);
                sb.AppendFormat("    public sealed partial class {0} : IDTRow\n    {{\n", cn);
                sb.AppendFormat("        public const string Sheet = \"{0}\";\n", sh.Name);
                foreach (var c in sh.Columns)
                {
                    sb.AppendFormat("        /// <summary>{0}{1}</summary>\n", Xml(c.Desc.Replace("\n", " ")), IsRefColumn(c) ? " (ID 참조, 0 = 없음)" : "");
                    sb.AppendFormat("        public {0} {1};\n", CsType(c.Type), c.Name);
                }
                sb.Append("        uint IDTRow.Id { get { return ID; } }\n");
                sb.Append("        HashSet<string> _blank;\n");
                sb.Append("        /// <summary>엑셀에서 빈칸(값 없음·미정)이었는지. 빈칸이면 필드는 0 / false / \"\".</summary>\n");
                sb.Append("        public bool IsBlank(string column) { return _blank != null && _blank.Contains(column); }\n");
                sb.AppendFormat("        internal static {0} Parse(DTCsv.Row r)\n        {{\n            var o = new {0}();\n", cn);
                foreach (var c in sh.Columns)
                {
                    var fn = c.Type == "uint" ? "U" : c.Type == "int" ? "I" : c.Type == "float" ? "F" : c.Type == "bool" ? "B" : "S";
                    sb.AppendFormat("            o.{0} = r.{1}(\"{0}\", ref o._blank);\n", c.Name, fn);
                }
                sb.Append("            return o;\n        }\n    }\n\n");
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        public static string GenerateRegistry(List<DTSheet> sheets)
        {
            var sb = new StringBuilder();
            sb.Append("// <auto-generated> DTExporter 가 만든 파일. 직접 고치지 말 것.\n");
            sb.Append("namespace InstructionDT\n{\n    public static partial class DT\n    {\n");
            foreach (var sh in sheets)
                sb.AppendFormat("        public static DTTable<{0}> {1} {{ get; private set; }}\n", ClassName(sh.Name), PropName(sh.Name));
            sb.Append("\n        static void LoadGenerated(System.Func<string, DTCsv> open)\n        {\n");
            foreach (var sh in sheets)
                sb.AppendFormat("            {0} = DTTable<{1}>.Build(\"{2}\", open(\"{2}\"), {1}.Parse);\n", PropName(sh.Name), ClassName(sh.Name), sh.Name);
            sb.Append("            Register(");
            sb.Append(string.Join(", ", sheets.Select(s => PropName(s.Name)).ToArray()));
            sb.Append(");\n");
            // enum 표 목록(이름 찾기용)
            sb.Append("            RegisterEnums(");
            sb.Append(string.Join(", ", sheets.Where(s => s.IsEnum).Select(s => PropName(s.Name)).ToArray()));
            sb.Append(");\n        }\n");
            sb.AppendFormat("\n        public const int TableCount = {0};\n", sheets.Count);
            sb.Append("    }\n}\n");
            return sb.ToString();
        }

        static string Xml(string s) { return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }

        /// 검사 후 오류가 없으면 CSV · C# 을 씀. zipPath 가 있으면 CSV 묶음 zip 도 만듦.
        public static DTReport Export(string xlsxPath, string csvDir, string codeDir, string zipPath)
        {
            var rep = Validate(xlsxPath);
            if (!rep.Ok) return rep;
            var enc = new UTF8Encoding(false);
            // CSV 는 BOM 포함 UTF-8: 한국어 윈도우 엑셀이 BOM 없는 CSV 를 CP949 로 읽어 한글이 깨지는 것을 막음.
            // (DTLoader 는 맨 앞 BOM 을 건너뜀)
            var csvEnc = new UTF8Encoding(true);
            Directory.CreateDirectory(csvDir);
            Directory.CreateDirectory(codeDir);
            var keep = new HashSet<string>(rep.Sheets.Select(s => s.Name + ".csv"));
            foreach (var f in Directory.GetFiles(csvDir, "*.csv"))           // 시트가 없어진 CSV 정리
                if (!keep.Contains(Path.GetFileName(f))) { File.Delete(f); rep.Warnings.Add("지운 CSV: " + Path.GetFileName(f)); }
            foreach (var sh in rep.Sheets) File.WriteAllText(Path.Combine(csvDir, sh.Name + ".csv"), ToCsv(sh), csvEnc);
            File.WriteAllText(Path.Combine(codeDir, "DTRows.g.cs"), GenerateRows(rep.Sheets), enc);
            File.WriteAllText(Path.Combine(codeDir, "DT.g.cs"), GenerateRegistry(rep.Sheets), enc);
            if (!string.IsNullOrEmpty(zipPath))
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using (var zs = new FileStream(zipPath, FileMode.Create))
                using (var z = new ZipArchive(zs, ZipArchiveMode.Create))   // ZipFile 대신 ZipArchive(유니티 API 호환 수준과 무관하게 동작)
                    foreach (var sh in rep.Sheets)
                    {
                        var e = z.CreateEntry(sh.Name + ".csv");
                        using (var w = new StreamWriter(e.Open(), csvEnc)) w.Write(ToCsv(sh));
                    }
            }
            return rep;
        }
    }

#if UNITY_EDITOR
    public static class DTExporterMenu
    {
        // 경로는 프로젝트 기준. 바꾸려면 여기만 고치면 됨.
        public const string XlsxPath = "DesignData/INSTRUCTION_DT_v6.xlsx";   // Assets 밖(엑셀 임시 파일이 임포트되지 않도록)
        public const string CsvDir = "Assets/Resources/DT";
        public const string CodeDir = "Assets/DT/Generated";
        public const string ZipPath = "DesignData/INSTRUCTION_DT_csv.zip";

        [UnityEditor.MenuItem("Tools/DT/검사만 (Validate)")]
        static void ValidateOnly()
        {
            var rep = DTExportCore.Validate(XlsxPath);
            Log(rep, "검사");
        }

        [UnityEditor.MenuItem("Tools/DT/내보내기 (Export)")]
        static void Export() { Run(null); }

        [UnityEditor.MenuItem("Tools/DT/내보내기 + CSV zip")]
        static void ExportZip() { Run(ZipPath); }

        static void Run(string zip)
        {
            DTReport rep;
            try { rep = DTExportCore.Export(XlsxPath, CsvDir, CodeDir, zip); }
            catch (IOException e) { UnityEngine.Debug.LogError("[DT] xlsx 를 읽지 못함: " + e.Message); return; }
            Log(rep, "내보내기");
            if (rep.Ok) UnityEditor.AssetDatabase.Refresh();
        }

        static void Log(DTReport rep, string what)
        {
            if (rep.Ok) UnityEngine.Debug.Log("[DT] " + what + " 완료\n" + rep);
            else UnityEngine.Debug.LogError("[DT] " + what + " 실패 — 파일을 쓰지 않음\n" + rep);
        }
    }
#endif
}

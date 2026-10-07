using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="LoadingTipTable"/> 인스펙터에 팁 이미지 도구를 붙인다.
/// <list type="bullet">
/// <item><b>자동 채우기</b> — CSV의 Id마다 <see cref="LoadingTipTable.tipImageFolder"/>에서 파일 이름이 같은 이미지(TIP_001.png 등)를 찾아 연결한다.
///   Sprite로 가져오지 않은 이미지는 Sprite로 바꾼다. 못 찾은 Id는 손으로 넣은 이미지를 그대로 둔다.</item>
/// <item><b>검사</b> — 이미지가 없는 Id · CSV에 없는 Id · 같은 Id 두 번을 알려 준다.</item>
/// </list>
/// </summary>
[CustomEditor(typeof(LoadingTipTable))]
public class LoadingTipTableEditor : Editor
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff", ".bmp" };

    private string report;
    private MessageType reportType = MessageType.Info;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var table = (LoadingTipTable)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("팁 이미지 도구", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(table.tipCsv == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이미지 자동 채우기 (파일 이름 = Id)")) AutoFill(table);
                if (GUILayout.Button("검사")) Check(table);
            }
        }
        if (table.tipCsv == null) EditorGUILayout.HelpBox("Tip Csv를 먼저 넣으십시오 — Id가 CSV에 있습니다.", MessageType.None);

        if (GUILayout.Button("이미지 폴더 열기 (없으면 만듦)")) RevealFolder(table);

        if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, reportType);
    }

    // ───────────────────────── 자동 채우기 ─────────────────────────

    private void AutoFill(LoadingTipTable table)
    {
        EnsureFolder(table.tipImageFolder);
        Dictionary<string, string> files = ImagesInFolder(table.tipImageFolder);

        // 손으로 넣은 이미지는 지키고, 폴더에 같은 이름 파일이 있으면 그것으로 바꾼다
        var old = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (LoadingTipTable.TipImage t in table.tipImages)
        {
            if (t != null && !string.IsNullOrWhiteSpace(t.id) && !old.ContainsKey(t.id.Trim())) old[t.id.Trim()] = t.sprite;
        }

        Undo.RecordObject(table, "팁 이미지 자동 채우기");
        var result = new List<LoadingTipTable.TipImage>();
        var csvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int fromFolder = 0, kept = 0, converted = 0;
        var missing = new List<string>();

        foreach (LoadingTipRow row in table.CsvRows)
        {
            if (string.IsNullOrEmpty(row.Id) || !csvIds.Add(row.Id)) continue;

            Sprite sprite = null;
            if (files.TryGetValue(row.Id, out string path))
            {
                if (MakeSprite(path)) converted++;
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) fromFolder++;
            }
            if (sprite == null && old.TryGetValue(row.Id, out Sprite manual) && manual != null)
            {
                sprite = manual;
                kept++;
            }
            if (sprite == null) missing.Add(row.Id);

            result.Add(new LoadingTipTable.TipImage { id = row.Id, sprite = sprite });
        }

        // CSV에서 빠진 Id라도 이미지를 넣어 둔 줄은 지우지 않고 뒤에 남긴다(문구를 잠시 뺀 경우)
        var orphans = new List<string>();
        foreach (LoadingTipTable.TipImage t in table.tipImages)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.id) || csvIds.Contains(t.id.Trim()) || t.sprite == null) continue;
            result.Add(t);
            orphans.Add(t.id);
        }

        table.tipImages = result;
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssetIfDirty(table);

        var sb = new StringBuilder();
        sb.AppendLine($"Id {csvIds.Count}개 — 폴더에서 연결 {fromFolder} · 손으로 넣은 것 유지 {kept} · 이미지 없음 {missing.Count}");
        if (converted > 0) sb.AppendLine($"Sprite로 가져오기를 바꾼 이미지 {converted}개");
        if (missing.Count > 0) sb.AppendLine("이미지 없음(문구만 나옴): " + string.Join(", ", missing));
        if (orphans.Count > 0) sb.AppendLine("CSV에 없는 Id(남겨 둠): " + string.Join(", ", orphans));
        report = sb.ToString().TrimEnd();
        reportType = missing.Count > 0 || orphans.Count > 0 ? MessageType.Warning : MessageType.Info;
        Debug.Log("[LoadingTipTable] " + report, table);
    }

    /// <summary>폴더 안(하위 폴더 포함) 이미지: 파일 이름(확장자 뺌) → 경로.</summary>
    private static Dictionary<string, string> ImagesInFolder(string folder)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!AssetDatabase.IsValidFolder(folder)) return map;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(ImageExtensions, ext) < 0) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            if (!map.ContainsKey(name)) map[name] = path;
            else Debug.LogWarning($"[LoadingTipTable] 이름이 같은 이미지가 둘입니다: {map[name]} · {path} — 앞의 것을 씁니다.");
        }
        return map;
    }

    /// <summary>Sprite(2D and UI)로 가져오지 않았으면 바꾼다. 바꿨으면 true.</summary>
    private static bool MakeSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return false;
        if (imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single) return false;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return true;
    }

    // ───────────────────────── 검사 ─────────────────────────

    private void Check(LoadingTipTable table)
    {
        var csvIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupCsv = new List<string>();
        foreach (LoadingTipRow row in table.CsvRows)
        {
            if (string.IsNullOrEmpty(row.Id)) continue;
            if (!csvIds.Add(row.Id)) dupCsv.Add(row.Id);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupImg = new List<string>();
        var orphans = new List<string>();
        foreach (LoadingTipTable.TipImage t in table.tipImages)
        {
            if (t == null || string.IsNullOrWhiteSpace(t.id)) continue;
            string id = t.id.Trim();
            if (!seen.Add(id)) dupImg.Add(id);
            if (!csvIds.Contains(id)) orphans.Add(id);
        }

        var missing = new List<string>();
        foreach (string id in csvIds)
        {
            if (table.ImageFor(id) == null) missing.Add(id);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"CSV Id {csvIds.Count}개 · 이미지 있음 {csvIds.Count - missing.Count}개");
        if (missing.Count > 0) sb.AppendLine("이미지 없음(문구만 나옴): " + string.Join(", ", missing));
        if (orphans.Count > 0) sb.AppendLine("CSV에 없는 Id(쓰이지 않음): " + string.Join(", ", orphans));
        if (dupImg.Count > 0) sb.AppendLine("이미지 목록에 같은 Id 두 번(마지막 것을 씀): " + string.Join(", ", dupImg));
        if (dupCsv.Count > 0) sb.AppendLine("CSV에 같은 Id 두 번: " + string.Join(", ", dupCsv));
        bool clean = missing.Count == 0 && orphans.Count == 0 && dupImg.Count == 0 && dupCsv.Count == 0;
        if (clean) sb.AppendLine("문제 없음 — 모든 팁에 이미지가 1:1로 있습니다.");

        report = sb.ToString().TrimEnd();
        reportType = clean ? MessageType.Info : MessageType.Warning;
    }

    // ───────────────────────── 폴더 ─────────────────────────

    private static void RevealFolder(LoadingTipTable table)
    {
        EnsureFolder(table.tipImageFolder);
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(table.tipImageFolder);
        if (folder != null)
        {
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

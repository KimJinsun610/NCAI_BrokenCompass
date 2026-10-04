using UnityEditor;
using UnityEngine;

/// <summary>
/// 야간근무 소리 가져오기 규칙(2026-10-04, 사운드 전달본). <b>처음 가져올 때만</b> 적용한다 — 손으로 바꾼 설정은 다시 덮지 않는다.
/// <list type="bullet">
/// <item>대상: <c>Assets/_Game/Audio/</c>, <c>Assets/_Game/Resources/Ambience/</c>, <c>Assets/_Game/Resources/Cctv/</c>.</item>
/// <item>모노로 합친다(3D로 내는 소리가 대부분이고 전달본도 모노다).</item>
/// <item>1.5MB 넘는 파일(30초 룸톤·험 루프) = 스트리밍 + Vorbis 0.5 · 200KB~1.5MB = 메모리 압축 + Vorbis 0.7 · 그보다 작으면 = 불러올 때 풀기 + Vorbis 0.8.</item>
/// </list>
/// </summary>
public sealed class NightDutyAudioImportRules : AssetPostprocessor
{
    private const long StreamBytes = 1536 * 1024;
    private const long SmallBytes = 200 * 1024;

    private void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/_Game/Audio/") && !path.StartsWith("Assets/_Game/Resources/Ambience/") && !path.StartsWith("Assets/_Game/Resources/Cctv/")) return;

        AudioImporter importer = (AudioImporter)assetImporter;
        if (!importer.importSettingsMissing) return;   // 이미 있던 설정은 건드리지 않는다

        long bytes = 0;
        try { bytes = new System.IO.FileInfo(path).Length; }
        catch (System.Exception) { }

        importer.forceToMono = true;
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        if (bytes > StreamBytes)
        {
            s.loadType = AudioClipLoadType.Streaming;
            s.quality = 0.5f;
            importer.loadInBackground = true;
        }
        else if (bytes > SmallBytes)
        {
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.quality = 0.7f;
        }
        else
        {
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.quality = 0.8f;
        }

        importer.defaultSampleSettings = s;
    }
}

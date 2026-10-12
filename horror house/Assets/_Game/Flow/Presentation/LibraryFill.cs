using System.Collections.Generic;
using NightDuty;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 103차 후속(민: 「도서관이 너무 어두우니까 살짝만 기본 빛을 — 앞이 살짝만 보일 정도로」) — 도서관만 아주 옅게 밝힌다.
/// <list type="bullet">
/// <item>원인: 도서관에는 실시간 등이 없고(CCTV 적외선 등 둘은 꺼져 있다) 구운 빛도 거의 없어, 손전등 없이는 화면이 완전히 검었다(주변광 0.02).</item>
/// <item>도서관 방 상자 위쪽 네 자리에 그림자 없는 차가운 점광(<see cref="Intensity"/>, 범위 <see cref="Range"/>m)을 단다.
/// 점광은 렌더링 레이어 <see cref="Layer"/>만 비추고, 도서관 상자(벽 두께만큼 넓힘) 안의 렌더러에만 그 비트를 더한다 — 벽 너머 복도·창밖으로 새지 않는다.</item>
/// <item>도서관에 들어오는 몹(노란 얼굴 등 스킨드 메시)도 <see cref="RetagSeconds"/>초마다 그 비트를 더해 방과 같은 빛을 받는다(플레이어 몸·손은 빼고).</item>
/// </list>
/// 판정과 무관. 씬은 고치지 않는다(런타임). 근무 씬이면 스스로 선다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LibraryFill : MonoBehaviour
{
    /// <summary>이 빛이 비추는 렌더링 레이어(창밖 얼굴 조명은 7).</summary>
    public const int Layer = 6;

    /// <summary>점광 하나의 세기 — 책장·바닥 윤곽만 살짝(PlayScene 비교: 0.4는 너무 밝고 0.16은 거의 안 보임).</summary>
    public const float Intensity = 0.22f;

    /// <summary>점광 범위(m).</summary>
    public const float Range = 8f;

    /// <summary>천장에서 아래로(m).</summary>
    public const float BelowCeiling = 0.8f;

    /// <summary>방 상자를 벽 두께만큼 옆으로 넓혀(m) 벽면도 받게 한다.</summary>
    public const float WallMargin = 1.6f;

    /// <summary>몹에 비트를 다시 더하는 간격(초).</summary>
    public const float RetagSeconds = 1f;

    /// <summary>달빛 같은 차가운 색.</summary>
    public static readonly Color Tint = new Color(0.72f, 0.8f, 1f);

    private readonly List<Light> _lights = new List<Light>();
    private Bounds _tagBox;
    private bool _built;
    private float _retry;
    private float _retag;

    /// <summary>지금 근무 씬의 것. 없으면 null.</summary>
    public static LibraryFill Active { get; private set; }

    /// <summary>단 점광(검수용).</summary>
    public IList<Light> Lights
    {
        get { return _lights; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        Active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForFirstScene()
    {
        EnsureFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureFor(scene);
    }

    private static void EnsureFor(Scene scene)
    {
        if (!FlowAutoInstall.IsDutyScene(scene) || FlowAutoInstall.Exists<LibraryFill>(scene)) return;
        FlowAutoInstall.CreateHost<LibraryFill>(scene, "LibraryFill (auto)");
    }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void Update()
    {
        if (!_built)
        {
            _retry -= Time.deltaTime;
            if (_retry > 0f) return;
            _retry = 1f;   // 공간 상자가 아직 없으면 1초 뒤 다시
            Build();
            return;
        }

        _retag -= Time.deltaTime;
        if (_retag > 0f) return;
        _retag = RetagSeconds;
        Transform player = PlayerRoot();
        foreach (SkinnedMeshRenderer r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) Tag(r, player);
    }

    private void Build()
    {
        SpaceZones zones = FindFirstObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(SpaceId.Library, out box)) return;
        _built = true;
        _tagBox = box;
        _tagBox.Expand(new Vector3(WallMargin * 2f, 0f, WallMargin * 2f));

        Transform player = PlayerRoot();
        int tagged = 0;
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (Tag(r, player)) tagged++;
        }

        uint bit = 1u << Layer;
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                GameObject go = new GameObject("도서관 기본 빛 " + (i * 2 + j + 1));
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(
                    Mathf.Lerp(box.min.x, box.max.x, 0.25f + 0.5f * i),
                    box.max.y - BelowCeiling,
                    Mathf.Lerp(box.min.z, box.max.z, 0.25f + 0.5f * j));
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Tint;
                light.intensity = Intensity;
                light.range = Range;
                light.shadows = LightShadows.None;
                UniversalAdditionalLightData data = go.GetComponent<UniversalAdditionalLightData>();
                if (data == null) data = go.AddComponent<UniversalAdditionalLightData>();
                data.renderingLayers = bit;
                _lights.Add(light);
            }
        }

        Debug.Log("[LibraryFill] 도서관 기본 빛 " + _lights.Count + "개(세기 " + Intensity + ") · 받는 렌더러 " + tagged);
    }

    /// <summary>도서관 상자 안(가운데 기준)이면 그 렌더러에 비트를 더한다. 더했으면(이미 있었어도) true.</summary>
    private bool Tag(Renderer r, Transform player)
    {
        if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
        if (player != null && r.transform.IsChildOf(player)) return false;
        if (!_tagBox.Contains(r.bounds.center)) return false;
        r.renderingLayerMask |= 1u << Layer;
        return true;
    }

    private static Transform PlayerRoot()
    {
        if (PlayerSensors.Active != null && PlayerSensors.Active.PlayerRoot != null) return PlayerSensors.Active.PlayerRoot;
        FPController fp = FindFirstObjectByType<FPController>();
        return fp != null ? fp.transform : null;
    }
}

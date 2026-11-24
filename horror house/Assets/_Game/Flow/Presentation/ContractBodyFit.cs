using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 계약서 본문이 서명 칸을 덮지 않게 글자 크기를 맞춘다(2026-11-24, 로컬라이징 — 영어 본문이 한국어보다 길어 서명 칸 위로 내려왔다).
/// <list type="bullet">
/// <item>본문(<see cref="BodyName"/>)의 아래 끝이 서명 칸(<see cref="SignaturePad"/>) 위쪽보다 <see cref="Margin"/>만큼 위에 오도록, 넘칠 때만 0.5씩 줄인다(원래 크기의 <see cref="MinScale"/>까지).</item>
/// <item>들어가는 글(지금 한국어)은 원래 크기 그대로다. 글이 바뀌면(언어 전환·<see cref="LocTextSwapper"/>가 늦게 바꿈) 원래 크기에서 다시 잰다.</item>
/// <item>씬은 고치지 않는다 — 서명 칸이 있는 씬에 스스로 붙는다.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class ContractBodyFit : MonoBehaviour
{
    /// <summary>본문 TMP 이름(ContractScene).</summary>
    public const string BodyName = "Txt_Body";

    /// <summary>본문 아래 끝과 서명 칸 사이에 남길 틈(화면 단위).</summary>
    public const float Margin = 12f;

    /// <summary>줄일 수 있는 한도(원래 크기 대비).</summary>
    public const float MinScale = 0.75f;

    private TMP_Text _body;
    private RectTransform _pad;
    private float _baseSize = -1f;
    private string _fittedFor;

    /// <summary>마지막으로 맞춘 글자 크기(검수).</summary>
    public float FittedSize { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
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
        if (!scene.IsValid() || !scene.isLoaded) return;
        SignaturePad pad = null;
        foreach (SignaturePad p in FindObjectsByType<SignaturePad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.gameObject.scene == scene) pad = p;
        }

        if (pad == null) return;
        foreach (ContractBodyFit f in FindObjectsByType<ContractBodyFit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (f.gameObject.scene == scene) return;
        }

        GameObject go = new GameObject("ContractBodyFit (auto)");
        SceneManager.MoveGameObjectToScene(go, scene);
        ContractBodyFit fit = go.AddComponent<ContractBodyFit>();
        fit._pad = pad.transform as RectTransform;
    }

    private void LateUpdate()
    {
        if (_body == null)
        {
            foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == BodyName && t.gameObject.scene == gameObject.scene) _body = t;
            }

            if (_body == null) return;
            _baseSize = _body.fontSize;
        }

        if (_pad == null) return;

        // 글이 바뀌었거나, 칸의 폭·위치가 바뀌었으면(첫 프레임엔 캔버스 배치가 끝나지 않아 폭이 다르다) 다시 잰다.
        _body.rectTransform.GetWorldCorners(_corners);
        float top = _corners[1].y;
        _pad.GetWorldCorners(_corners);
        float floor = _corners[1].y + Margin;
        float width = _body.rectTransform.rect.width;
        if (_body.text == _fittedFor && Mathf.Abs(width - _fittedWidth) < 0.5f && Mathf.Abs(top - _fittedTop) < 0.5f && Mathf.Abs(floor - _fittedFloor) < 0.5f) return;
        Fit(top, floor, width);
    }

    private readonly Vector3[] _corners = new Vector3[4];
    private float _fittedWidth, _fittedTop, _fittedFloor;

    private void Fit(float top, float floor, float width)
    {
        _fittedFor = _body.text;
        _fittedWidth = width;
        _fittedTop = top;
        _fittedFloor = floor;
        float scale = Mathf.Max(0.0001f, _body.rectTransform.lossyScale.y);

        float size = _baseSize;
        float min = _baseSize * MinScale;
        _body.fontSize = size;
        while (size > min && top - _body.GetPreferredValues(_fittedFor, width, 0f).y * scale < floor)
        {
            size -= 0.5f;
            _body.fontSize = size;
        }

        FittedSize = size;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 엔딩 크레딧(HUD_EndingCredits_Design) 한 번 — 두 엔딩이 같이 쓴다.
///  · 엔딩 1(창밖 남자를 바라봄): 피날레 마지막 문구 뒤, 메인으로 가기 전(<c>FinaleDirector</c>).
///  · 엔딩 2(바라보지 않고 전화로 퇴근): 성공 엔딩 씬이 끝난 뒤(<see cref="SuccessEndingFlow"/>).
/// <list type="number">
/// <item>크레딧을 세우고 엔딩 번호 칸(<see cref="EndingLabelName"/>)에 「엔딩 N」을 쓴다.</item>
/// <item>검은 덮개(Fade)가 <see cref="FadeInSeconds"/>에 걷힌다. QUIT 버튼은 프리팹의 DOTween으로 나타난다.</item>
/// <item>QUIT 글자가 다 나타난 뒤부터 버튼이 호버(ui_hover_kill)·클릭을 받는다 — QUIT · 그 뒤 아무 키 · <see cref="AutoReturnAfter"/>초면 끝.</item>
/// <item>덮개를 다시 덮고(<see cref="FadeOutSeconds"/>) 코루틴이 끝난다 — 어디로 갈지는 부른 쪽이 정한다.</item>
/// </list>
/// 크레딧 프리팹의 DOTween은 게임 시간으로 돈다 — 부르기 전에 timeScale을 1로 둘 것.
/// </summary>
public sealed class EndingCredits
{
    /// <summary>크레딧 프리팹 안 엔딩 번호 칸 이름.</summary>
    public const string EndingLabelName = "Txt_Ending";

    /// <summary>엔딩 번호 문구(「엔딩 1」).</summary>
    public string LabelFormat = "엔딩 {0}";

    /// <summary>검은 덮개가 걷히는 시간(초).</summary>
    public float FadeInSeconds = 1.5f;

    /// <summary>QUIT 글자를 못 찾을 때 이 시간이 지나면 버튼을 켠다(프리팹 DOTween 2.5 + 4초).</summary>
    public float SkipAfter = 6.5f;

    /// <summary>크레딧이 뜬 뒤 이 시간이 지나면 저절로 끝. 0이면 저절로 끝나지 않는다.</summary>
    public float AutoReturnAfter = 15f;

    /// <summary>끝날 때 다시 검게 덮는 시간(초).</summary>
    public float FadeOutSeconds = 1f;

    private CanvasGroup _fade;
    private bool _leaving;

    /// <summary>세운 크레딧(없으면 null).</summary>
    public GameObject Instance { get; private set; }

    /// <summary>
    /// 크레딧을 띄우고 플레이어가 넘길 때까지 기다린 뒤 검게 덮고 끝난다. 프리팹이 없으면 바로 끝난다.
    /// </summary>
    public IEnumerator Play(GameObject prefab, int ending)
    {
        _leaving = false;
        if (prefab == null)
        {
            Debug.LogWarning("[EndingCredits] 크레딧 프리팹이 비어 있어 건너뜁니다.");
            yield break;
        }

        EnsureEventSystem();
        Instance = Object.Instantiate(prefab);
        Transform fade = FindDeep(Instance.transform, "Fade");
        _fade = fade != null ? fade.GetComponent<CanvasGroup>() : null;

        Transform labelT = FindDeep(Instance.transform, EndingLabelName);
        TMPro.TMP_Text label = labelT != null ? labelT.GetComponent<TMPro.TMP_Text>() : null;
        if (label != null) label.text = string.Format(LabelFormat, ending);

        Button quit = Instance.GetComponentInChildren<Button>(true);
        TMPro.TMP_Text quitLabel = quit != null ? quit.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (quit != null)
        {
            quit.onClick.AddListener(Leave);
            SetQuitReady(quit, false);   // 글자가 다 나타나기 전에는 누를 수 없다
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        yield return FadeTo(0f, FadeInSeconds);

        float t = 0f;
        bool ready = quit == null;
        while (!_leaving)
        {
            t += Time.unscaledDeltaTime;

            // QUIT 글자가 다 나타나면 그때부터 호버·클릭을 받는다. 글자를 못 찾으면 SkipAfter.
            if (!ready && (LabelShown(quitLabel) || t >= SkipAfter))
            {
                ready = true;
                SetQuitReady(quit, true);
            }

            bool skip = ready && Input.anyKeyDown && !Input.GetMouseButtonDown(0);   // 클릭은 버튼이 받는다
            bool timeUp = AutoReturnAfter > 0f && t >= AutoReturnAfter;
            if (skip || timeUp) _leaving = true;
            yield return null;
        }

        yield return FadeTo(1f, FadeOutSeconds);
    }

    /// <summary>넘긴다(QUIT 버튼도 이것을 부른다).</summary>
    public void Leave()
    {
        _leaving = true;
    }

    private IEnumerator FadeTo(float to, float seconds)
    {
        if (_fade == null) yield break;
        float from = _fade.alpha;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
            yield return null;
        }

        _fade.alpha = to;
    }

    /// <summary>
    /// QUIT 버튼을 켜거나 끈다. 크레딧 프리팹의 QUIT은 다른 HUD 버튼(메인 「시작」)처럼 배경이 투명 이미지(ui_none)이고
    /// 마우스를 올리면 호버 이미지(ui_hover_kill)로 바뀌는 Sprite Swap인데, 그 배경 Image가 꺼져 있어 호버도 클릭도 안 됐다.
    /// 켤 때 배경 Image를 켜서 호버·클릭을 받게 한다(프리팹은 그대로). 배경이 없으면 글자가 클릭을 받는다.
    /// </summary>
    private static void SetQuitReady(Button button, bool ready)
    {
        if (button == null) return;
        button.interactable = ready;
        Graphic target = button.targetGraphic;
        if (target != null)
        {
            target.enabled = ready;
            target.raycastTarget = true;
            return;
        }

        foreach (TMPro.TMP_Text label in button.GetComponentsInChildren<TMPro.TMP_Text>(true)) label.raycastTarget = ready;
    }

    /// <summary>QUIT 글자가 다 나타났는지(페이드가 끝났는지).</summary>
    private static bool LabelShown(TMPro.TMP_Text label)
    {
        return label != null && label.color.a >= 0.99f && label.alpha >= 0.99f;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>EventSystem이 없으면(엔딩 씬·근무 씬) 하나 만든다 — QUIT 버튼을 누를 수 있게.</summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        // 다른 씬(Result·Main)과 같은 입력 모듈.
        // 실행 중에 붙이면 인스펙터에서 붙일 때처럼 기본 액션(Point·Click …)이 들어가지 않아 클릭을 못 받는다 — 직접 넣는다.
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        UnityEngine.InputSystem.UI.InputSystemUIInputModule module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (module.actionsAsset == null || module.point == null || module.leftClick == null) module.AssignDefaultActions();
    }
}

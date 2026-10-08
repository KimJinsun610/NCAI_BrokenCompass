using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 위 조작 안내(HUD의 <c>Info</c> — 「F : 손전등 / Tab : 태블릿」)를 초반 며칠만 보여 준다.
/// 근무일(<see cref="GameSession.CurrentDay"/>)이 <see cref="lastDay"/>를 넘으면 안내와 그 아래 그림(Graphic)을 모두 끈다.
/// 오브젝트는 켜 둔 채 그림만 끄므로, 같은 씬에서 날이 바뀌어도(디버그 다시 열기 등) 다음 프레임에 맞춰진다.
///
/// <para>씬에 직접 붙어 있지 않아도 된다 — 씬이 열릴 때 HUD 캔버스(<c>HUD_Play</c>로 시작하는 이름) 아래 <c>Info</c>를 찾아
/// 스스로 붙는다. 근무 씬(0. Main/PlayScene)의 HUD는 프리팹 인스턴스가 아니라서 씬 파일을 고치지 않으려고 이렇게 했다.</para>
/// </summary>
[DisallowMultipleComponent]
public class HudKeyGuide : MonoBehaviour
{
    private const string HudPrefix = "HUD_Play";
    private const string GuideName = "Info";

    [Tooltip("이 근무일까지만 보인다(1일차부터). 2면 1·2일차에 보이고 3일차부터 안 보인다.")]
    [SerializeField, Min(1)] private int lastDay = 2;

    [Tooltip("켜고 끌 그림들. 비우면 이 오브젝트와 자식의 Graphic 전부(처음에 켜져 있던 것만).")]
    [SerializeField] private Graphic target;

    private readonly List<Graphic> graphics = new List<Graphic>();
    private int shownDay = -1;

    private void Awake()
    {
        graphics.Clear();
        if (target != null) graphics.Add(target);
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
        {
            if (g != target && g.enabled) graphics.Add(g);   // 원래 꺼져 있던 그림은 건드리지 않는다
        }
        Apply();
    }

    private void Update()
    {
        if (shownDay != GameSession.CurrentDay) Apply();
    }

    private void Apply()
    {
        shownDay = GameSession.CurrentDay;
        bool show = shownDay <= lastDay;
        foreach (Graphic g in graphics) if (g != null) g.enabled = show;
    }

    // ─────────────────────────────── 자동 부착 ───────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachAll();
    }

    private static void AttachAll()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || !c.name.StartsWith(HudPrefix)) continue;
            Transform guide = c.transform.Find(GuideName);
            if (guide == null || guide.GetComponent<HudKeyGuide>() != null) continue;
            guide.gameObject.AddComponent<HudKeyGuide>();
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상호작용 안내. <see cref="PlayerInteractor"/>가 고른 대상을 화면에 알린다 — 하는 일은 둘뿐이다.
///
/// <list type="number">
/// <item><b>조준선을 밝힌다.</b> <c>HUD_Play/Img_Reticle</c>은 평소 알파 0.25로 거의 안 보인다.
/// 대상을 잡으면 또렷해져서, <b>겨눔이 맞았는지를 손이 아니라 눈으로</b> 알 수 있다.</item>
/// <item><b>조준선 아래에 한 줄</b>을 띄운다. 「[E] 문 열기」·「잠겨 있습니다」.</item>
/// </list>
///
/// <para><b>씬에 이미 있는 HUD를 쓴다.</b> <c>HUD_Play</c> 캔버스와 <c>Img_Reticle</c>은 이미 씬에 있다.
/// 안내 줄만 없어서, 없으면 런타임에 만들고 글꼴은 같은 캔버스의 다른 글씨에서 빌린다(한글이 깨지지 않게).
/// <b>민이 씬에 <c>Txt_Prompt</c>를 만들어 꽂으면 그쪽을 쓴다</b> — 그때 이 런타임 생성은 아무 일도 하지 않는다.</para>
///
/// <para>읽기만 한다. 판정에도 상호작용에도 관여하지 않으므로, 이 컴포넌트가 없어도 문은 그대로 열린다.</para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class InteractionHud : MonoBehaviour
{
    private const string CanvasName = "HUD_Play";
    private const string ReticleName = "Img_Reticle";
    private const string PromptName = "Txt_Prompt";

    /// <summary>대상을 잡았을 때 조준선 알파.</summary>
    private const float ReticleHot = 0.95f;

    [Header("붙일 곳 (비우면 씬에서 찾는다)")]
    [SerializeField] private Canvas hud;
    [SerializeField] private Image reticle;
    [SerializeField] private TMP_Text prompt;

    [Header("안내 줄")]
    [Tooltip("조준선에서 아래로 띄울 거리(px, 1080 기준).")]
    [SerializeField] private float promptOffset = 64f;

    [SerializeField] private float promptFontSize = 28f;

    private float _reticleCool = 0.25f;
    private bool _resolved;

    /// <summary>
    /// 안내가 없으면 만든다. <b><see cref="PlayerInteractor"/>가 부른다</b> —
    /// 각자 <c>RuntimeInitializeOnLoadMethod</c>로 서면 둘의 순서가 보장되지 않아,
    /// 안내가 상호작용기보다 먼저 서면 서로를 못 찾고 그냥 없어진다(2026-09-22 실측).
    /// 상호작용기를 따라다니므로 민이 <c>PlaySystems</c>에 손으로 붙인 경우에도 같이 선다.
    /// </summary>
    public static void EnsureExists()
    {
        if (FindAnyObjectByType<InteractionHud>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject go = new GameObject("InteractionHud (auto)");
        go.AddComponent<InteractionHud>();
    }

    private void LateUpdate()
    {
        Resolve();

        PlayerInteractor player = PlayerInteractor.Active;
        string line = player != null ? player.Prompt : string.Empty;
        bool hot = player != null && player.HasAction;

        if (prompt != null)
        {
            if (prompt.gameObject.activeSelf != (line.Length > 0))
            {
                prompt.gameObject.SetActive(line.Length > 0);
            }

            if (line.Length > 0 && prompt.text != line)
            {
                prompt.text = line;
            }
        }

        if (reticle == null)
        {
            return;
        }

        Color c = reticle.color;
        float want = hot ? ReticleHot : _reticleCool;
        if (!Mathf.Approximately(c.a, want))
        {
            c.a = want;
            reticle.color = c;
        }
    }

    // ─────────────────────────────── 찾기와 만들기 ───────────────────────────────

    private void Resolve()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;

        if (hud == null)
        {
            hud = FindHud();
        }

        if (hud == null)
        {
            Debug.LogWarning("[상호작용 HUD] 화면 캔버스를 찾지 못했습니다. 안내를 띄우지 않습니다.", this);
            return;
        }

        if (reticle == null)
        {
            Transform t = hud.transform.Find(ReticleName);
            reticle = t != null ? t.GetComponent<Image>() : null;
        }

        if (reticle != null)
        {
            _reticleCool = reticle.color.a;
        }

        if (prompt == null)
        {
            Transform t = hud.transform.Find(PromptName);
            prompt = t != null ? t.GetComponent<TMP_Text>() : null;
        }

        if (prompt == null)
        {
            prompt = CreatePrompt();
        }

        if (prompt != null)
        {
            prompt.gameObject.SetActive(false);
        }
    }

    /// <summary>이름으로 먼저 찾고, 없으면 화면 겹침 캔버스 중 정렬 순서가 가장 낮은 것(= 본 화면)을 쓴다.</summary>
    private static Canvas FindHud()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas fallback = null;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].renderMode != RenderMode.ScreenSpaceOverlay)
            {
                continue;
            }

            if (all[i].name == CanvasName)
            {
                return all[i];
            }

            if (fallback == null || all[i].sortingOrder < fallback.sortingOrder)
            {
                fallback = all[i];
            }
        }

        return fallback;
    }

    /// <summary>
    /// 안내 줄을 만든다. <b>글꼴은 같은 캔버스의 다른 글씨에서 빌린다</b> —
    /// TMP 기본 글꼴에는 한글이 없어서 그대로 두면 네모로 나온다.
    /// </summary>
    private TMP_Text CreatePrompt()
    {
        GameObject go = new GameObject(PromptName, typeof(RectTransform));
        go.transform.SetParent(hud.transform, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = promptFontSize;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = Color.white;

        TMP_Text donor = FindDonorFont();
        if (donor != null)
        {
            text.font = donor.font;
            text.fontSharedMaterial = donor.fontSharedMaterial;
        }
        else
        {
            Debug.LogWarning("[상호작용 HUD] 빌려 올 글꼴을 찾지 못했습니다. 한글이 깨질 수 있습니다.", this);
        }

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800f, 60f);
        rt.anchoredPosition = new Vector2(0f, -promptOffset);

        return text;
    }

    private TMP_Text FindDonorFont()
    {
        TMP_Text[] all = hud.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].font != null)
            {
                return all[i];
            }
        }

        return null;
    }
}

using NightDuty;
using UnityEngine;

/// <summary>
/// 79차 — 역설 문자가 가리키는 「점검」 미끼(민: T1 「변기 수압을 점검해주십시오.」 → 물 내려가는 변기 · L1 「도서관 무너진 가구들을 점검해주십시오.」 → 쓰러진 책장).
/// <list type="bullet">
/// <item>그 수칙의 역설 문자가 온 뒤(<see cref="NightRun.Paradox"/>), 그 수칙을 아직 어기지 않았을 때만 켜진다 — 점검 대상처럼 외곽선이 뜨고 [E]를 누르고 있으면 「점검」한다.</item>
/// <item>점검표의 정상/이상 보고가 아니다 — 판정·점검판에 아무것도 보내지 않는다. 점검하느라 머무는 동안 수칙(T1 물 내려가기 전에 나오기 · L1 책장 곁에 서지 않기)을 어기게 되는 것이 요점.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public sealed class ParadoxLure : MonoBehaviour
{
    /// <summary>[E]를 누르고 있어야 하는 시간(초).</summary>
    public const float HoldSeconds = 2.5f;

    private const float Reach = 2f;
    private const float AimRadius = 0.08f;

    private string _ruleId = string.Empty;
    private float _until = -1f;
    private float _held;
    private float _doneShownUntil = -1f;
    private bool _done;
    private bool _ownsPrompt;

    /// <summary>미끼가 가리키는 수칙.</summary>
    public string RuleId
    {
        get { return _ruleId; }
    }

    /// <summary>지금 외곽선·상호작용이 켜져 있는지(시험·디버그).</summary>
    public bool Armed
    {
        get
        {
            if (_done || _ruleId.Length == 0 || !NightRun.IsNightActive || NightRun.IsCaptured) return false;
            if (_until >= 0f && Time.time > _until) return false;
            ParadoxRun p = NightRun.Paradox;
            if (p == null || !p.Sent || p.RuleId != _ruleId) return false;
            FinalRuleBook book = NightRun.FinalRules;
            FinalJudge j = book != null ? book.Judge(_ruleId) : null;
            return j == null || !j.Violated;
        }
    }

    /// <summary>그 오브젝트에 미끼를 단다(이미 있으면 그것). <paramref name="seconds"/> &gt; 0이면 그 시간 뒤 꺼진다(T1 — 물이 다 내려가면).</summary>
    public static ParadoxLure Attach(GameObject target, string ruleId, float seconds)
    {
        if (target == null) return null;
        ParadoxLure l = target.GetComponent<ParadoxLure>();
        if (l == null) l = target.AddComponent<ParadoxLure>();
        l._ruleId = ruleId ?? string.Empty;
        l._until = seconds > 0f ? Time.time + seconds : -1f;
        l._held = 0f;
        l._done = false;
        l.enabled = true;
        return l;
    }

    private void OnDisable()
    {
        ReleasePrompt();
    }

    private void Update()
    {
        if (_doneShownUntil > Time.time)
        {
            if (Aimed()) Claim("점검했습니다.", false);
            else ReleasePrompt();
            return;
        }

        if (!Armed || !Aimed())
        {
            _held = 0f;
            ReleasePrompt();
            return;
        }

        InteractionOutline.Request(transform);
#if ENABLE_LEGACY_INPUT_MANAGER
        bool holding = Input.GetKey(KeyCode.E);
#else
        bool holding = false;
#endif
        _held = holding ? _held + Time.deltaTime : 0f;
        if (_held >= HoldSeconds)
        {
            _done = true;
            _doneShownUntil = Time.time + 1.5f;
            Debug.Log("[역설 미끼] " + _ruleId + " 「점검」 완료 — " + name, this);
            return;
        }

        Claim(_held > 0f ? "점검 중… " + Mathf.RoundToInt(_held / HoldSeconds * 100f) + "%" : "[E 길게] 점검", true);
    }

    private bool Aimed()
    {
        if (Time.timeScale <= 0f || PlayerSensors.TabletRaised) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;
        RaycastHit hit;
        if (!Physics.SphereCast(cam.transform.position, AimRadius, cam.transform.forward, out hit, Reach, ~0, QueryTriggerInteraction.Ignore)) return false;
        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    private void Claim(string line, bool hot)
    {
        _ownsPrompt = true;
        InteractionHud.ExternalPrompt = line;
        InteractionHud.ExternalHot = hot;
    }

    private void ReleasePrompt()
    {
        if (!_ownsPrompt) return;
        _ownsPrompt = false;
        InteractionHud.ExternalPrompt = string.Empty;
        InteractionHud.ExternalHot = false;
    }
}

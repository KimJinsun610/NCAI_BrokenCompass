using NightDuty;
using UnityEngine;

/// <summary>
/// 조우 순간의 타격감(44차, 민: 「조우했을 때 나오는 사운드가 너무 임팩트가 없어. 놀라지도 않아」).
/// 원인은 셋이었다 — ① 대면 소리를 <c>AudioSource.PlayClipAtPoint</c>(최소 거리 1m)로 4~8m 앞에서 내 12~18dB 작아졌고
/// ② 엠비언트 스팅어(<c>stinger_*</c>)와 덕킹이 만들어져 있었지만 아무도 부르지 않았고 ③ 소리 전후의 대비(정적)가 없었다.
/// <list type="bullet">
/// <item><b>전조</b>: 엠비언트를 1.2초에 걸쳐 45%로 — 「조용해졌다」. 헛예고도 같다(6초 뒤 되돌림).</item>
/// <item><b>대면</b>: 갈래별로 — 덮치기(모형 급습·소년 머리 박기·복도 끝 형체·노란 얼굴·화장실 소녀·창밖 남자·CCTV 사람) = 엠비언트를 순간 10%로 끊고 <c>stinger_hit</c> + 몸 계기 심박 급등 ·
/// 스며들기(사람 나무·천장 다리·없던 문·앉은 소년) = 25%로 낮추고 <c>stinger_whisper</c> · 암전(과학실·화장실) = 거의 무음 + <c>stinger_breath</c> ·
/// 소리 조우(발소리·부르는 목소리) = 스팅어 없이 30%로 비켜 줘 그 소리만 또렷하게.</item>
/// <item><b>창 닫힘·결과·중단</b>: 2.5초에 걸쳐 되돌린다. 오래 안 오면 스스로 되돌린다(최대 12초).</item>
/// </list>
/// 판정과 무관하다. <see cref="DirectionStage"/>가 붙여 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncounterImpact : MonoBehaviour
{
    /// <summary>대면 갈래.</summary>
    public enum Kind
    {
        Sound,
        Hit,
        Creep,
        Dark,
    }

    private const float MaxDuckSeconds = 12f;
    private static EncounterImpact s_active;
    private float _restoreAt = -1f;

    /// <summary>지금 붙어 있는 것. 없으면 null.</summary>
    public static EncounterImpact Active
    {
        get { return s_active; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
        s_strongNight = null;
    }

    private void OnEnable()
    {
        s_active = this;
    }

    private void OnDisable()
    {
        if (s_active == this) s_active = null;
        Restore(0.5f);
    }

    /// <summary>
    /// 점프스케어 세기(51차, 민: 「조우 효과음(현악기)이 안 놀랍고 짜친다 — 점프스케어 효과음으로」). 소리는 연출 소리 표 <c>stinger.weak/mid/strong</c>
    /// (OpenGameArt Horror Hit Soundpack 1, CC0). 강은 밤당 한 번 — 두 번째부터는 중으로 낮춘다. 같은 클립이 잇달아 나지 않게 표가 무작위로 고른다.
    /// </summary>
    public enum Tier
    {
        None,
        Weak,
        Mid,
        Strong,
    }

    private static object s_strongNight;

    /// <summary>그 조우의 점프스케어 세기.</summary>
    public static Tier TierOf(string encounterId)
    {
        switch (encounterId)
        {
            case ProgramCatalog.ModelRush:
            case ProgramCatalog.CeilingLegs:   // 51차: 천장 다리 → 시체 낙하
                return Tier.Strong;
            case ProgramCatalog.HallEndFigure:
            case ProgramCatalog.YellowFace:
            case ProgramCatalog.ToiletGirl:
            case ProgramCatalog.SuitMan:
            case ProgramCatalog.PeopleTree:
            case ProgramCatalog.ScienceBlackout:
            case ProgramCatalog.ToiletBlackout:
                return Tier.Mid;
            case ProgramCatalog.BoySeated:
            case ProgramCatalog.BoyBang:   // 52차: 조용히 앉아 있다 — 놀람은 C2를 어겼을 때의 머리 박기(BoyHeadBang)
            case ProgramCatalog.PhantomDoor:
            case ProgramCatalog.CctvPerson:
                return Tier.Weak;
            default:
                return Tier.None;   // 발소리·목소리·창 두드림 — 그 소리 자체가 놀람
        }
    }

    /// <summary>점프스케어 소리를 2D로 낸다(강은 밤당 한 번, 넘치면 중). 연출 소리 표에 없으면 false.</summary>
    public static bool PlayTier(Tier tier, float volumeScale = 1f)
    {
        if (tier == Tier.None) return false;
        if (tier == Tier.Strong)
        {
            object night = NightRun.Inspections.Plan;
            if (ReferenceEquals(night, s_strongNight)) tier = Tier.Mid;
            else s_strongNight = night;
        }

        string key = tier == Tier.Strong ? "stinger.strong" : tier == Tier.Mid ? "stinger.mid" : "stinger.weak";
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return false;
        AmbiencePlayer amb = AmbiencePlayer.Active;
        if (amb != null && amb.PlayStingerClip(clip, volume * volumeScale)) return true;
        AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume * volumeScale);
        return true;
    }

    /// <summary>그 조우의 대면 갈래.</summary>
    public static Kind KindOf(string encounterId)
    {
        switch (encounterId)
        {
            case ProgramCatalog.ModelRush:
            case ProgramCatalog.HallEndFigure:
            case ProgramCatalog.YellowFace:
            case ProgramCatalog.ToiletGirl:
            case ProgramCatalog.SuitMan:
            case ProgramCatalog.CctvPerson:
            case ProgramCatalog.CeilingLegs:
                return Kind.Hit;
            case ProgramCatalog.PeopleTree:
            case ProgramCatalog.PhantomDoor:
            case ProgramCatalog.BoySeated:
            case ProgramCatalog.BoyBang:
                return Kind.Creep;
            case ProgramCatalog.ScienceBlackout:
            case ProgramCatalog.ToiletBlackout:
                return Kind.Dark;
            default:
                return Kind.Sound;
        }
    }

    /// <summary>전조(헛예고 포함) — 주변이 조용해진다.</summary>
    public void Foreshadow(bool isFalse)
    {
        AmbiencePlayer amb = AmbiencePlayer.Active;
        if (amb != null) amb.Duck(0.45f, 1.2f);
        _restoreAt = Time.time + (isFalse ? 6f : MaxDuckSeconds);
    }

    /// <summary>
    /// 60차(민: 「도서관 조우는 플레이어가 몹을 마주쳤을 때 효과음이 들리게」): 대면 순간이 아니라 몹이 시야에 들어온 순간 점프스케어를 내는 조우.
    /// <see cref="DirectionStage"/>가 대역에 <see cref="SeenStinger"/>를 붙여 <see cref="Hit"/>를 부른다.
    /// </summary>
    public static bool HitsWhenSeen(string encounterId)
    {
        return encounterId == ProgramCatalog.YellowFace || encounterId == ProgramCatalog.SuitMan;
    }

    /// <summary>70차: 대면 = 달려오기 시작, 점프스케어는 덮치는 순간(<see cref="MannequinRush"/>가 <see cref="Hit"/>를 부른다) — 모형 급습.</summary>
    public static bool HitsOnArrival(string encounterId)
    {
        return encounterId == ProgramCatalog.ModelRush;
    }

    /// <summary>덮치기 점프스케어 — 엠비언트를 순간 끊고 스팅어 + 심박 급등. 시체 낙하는 민이 준 전용 소리(<c>stinger.corpse</c>, 60차).</summary>
    public void Hit(string encounterId)
    {
        AmbiencePlayer amb = AmbiencePlayer.Active;
        if (amb != null) amb.Duck(0.1f, 0.06f);
        if (encounterId != ProgramCatalog.CeilingLegs || !PlayKey("stinger.corpse")) PlayTier(TierOf(encounterId));
        NightDutyMixer.Spotlight(2.5f);   // 67차(민: 「연출 사운드가 다른 효과음보다 더 강조되게」)
        BodyMeter.Startle();
        _restoreAt = Time.time + MaxDuckSeconds;
    }

    /// <summary>연출 소리 표의 그 키를 2D로 낸다. 없으면 false.</summary>
    public static bool PlayKey(string key, float volumeScale = 1f)
    {
        float volume;
        AudioClip clip = DirectionSoundTableSO.FindExact(key, out volume);
        if (clip == null) return false;
        AmbiencePlayer amb = AmbiencePlayer.Active;
        if (amb != null && amb.PlayStingerClip(clip, volume * volumeScale)) return true;
        AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume * volumeScale);
        return true;
    }

    /// <summary>대면 — 갈래별 스팅어·덕킹·몸 반응.</summary>
    public void Confront(string encounterId)
    {
        AmbiencePlayer amb = AmbiencePlayer.Active;
        Kind kind = KindOf(encounterId);
        switch (kind)
        {
            case Kind.Hit:
                if (HitsWhenSeen(encounterId) || HitsOnArrival(encounterId))
                {
                    if (amb != null) amb.Duck(0.3f, 0.4f);   // 60차: 조용해지기만 — 마주치는 순간에 Hit
                    break;
                }

                Hit(encounterId);
                break;
            case Kind.Creep:
                if (amb != null) amb.Duck(0.25f, 0.6f);
                PlayTier(TierOf(encounterId), 0.85f);
                break;
            case Kind.Dark:
                if (amb != null)
                {
                    amb.Duck(0.04f, 0.05f);
                    amb.PlayStinger("stinger_breath", 0.9f);
                }

                NightDutyMixer.Spotlight(1.8f);   // 67차

                PlayTier(TierOf(encounterId), 0.8f);
                BodyMeter.Startle();
                break;
            default:
                if (amb != null) amb.Duck(0.3f, 0.4f);
                break;
        }

        _restoreAt = Time.time + MaxDuckSeconds;
    }

    /// <summary>창이 닫히거나 끝났다 — 서서히 되돌린다.</summary>
    public void Release()
    {
        Restore(2.5f);
    }

    private void Restore(float seconds)
    {
        _restoreAt = -1f;
        AmbiencePlayer amb = AmbiencePlayer.Active;
        if (amb != null) amb.Duck(1f, seconds);
    }

    private void Update()
    {
        if (_restoreAt > 0f && Time.time >= _restoreAt) Restore(3f);
    }
}

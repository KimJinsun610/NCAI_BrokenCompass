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

    /// <summary>그 조우의 대면 갈래.</summary>
    public static Kind KindOf(string encounterId)
    {
        switch (encounterId)
        {
            case ProgramCatalog.ModelRush:
            case ProgramCatalog.BoyBang:
            case ProgramCatalog.HallEndFigure:
            case ProgramCatalog.YellowFace:
            case ProgramCatalog.ToiletGirl:
            case ProgramCatalog.SuitMan:
            case ProgramCatalog.CctvPerson:
                return Kind.Hit;
            case ProgramCatalog.PeopleTree:
            case ProgramCatalog.CeilingLegs:
            case ProgramCatalog.PhantomDoor:
            case ProgramCatalog.BoySeated:
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

    /// <summary>대면 — 갈래별 스팅어·덕킹·몸 반응.</summary>
    public void Confront(string encounterId)
    {
        AmbiencePlayer amb = AmbiencePlayer.Active;
        Kind kind = KindOf(encounterId);
        switch (kind)
        {
            case Kind.Hit:
                if (amb != null)
                {
                    amb.Duck(0.1f, 0.06f);
                    amb.PlayStinger("stinger_hit", 1f);
                }

                BodyMeter.Startle();
                break;
            case Kind.Creep:
                if (amb != null)
                {
                    amb.Duck(0.25f, 0.6f);
                    amb.PlayStinger("stinger_whisper", 0.85f);
                }

                break;
            case Kind.Dark:
                if (amb != null)
                {
                    amb.Duck(0.04f, 0.05f);
                    amb.PlayStinger("stinger_breath", 0.9f);
                }

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

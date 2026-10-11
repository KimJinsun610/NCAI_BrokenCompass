using System.Collections;
using NightDuty;
using UnityEngine;

/// <summary>
/// 79차 T1 「화장실에서 물이 다 내려가기 전에 나오십시오.」 — 물 내림 단서 때 실제로 한 변기가 내려간다.
/// <list type="bullet">
/// <item>화장실에서 플레이어와 가장 가까운 변기(<see cref="ToiletBowls"/>가 맑은 물을 얹은 것)를 고른다.</item>
/// <item>물 내림 애니메이션은 김진선님 <c>ToiletFlush</c>(핏물 소용돌이 프리팹 — 대역 <c>prop.toilet.flush</c>)를 그 변기에 겹쳐 쓴다.
/// 핏물은 T-1 이상과 헷갈리므로 이 인스턴스만 맑은 물 재질·옅은 물방울로 바꾼다(김진선님 에셋은 고치지 않는다). 소리도 그 프리팹이 낸다.</item>
/// <item>단서가 끝나면 겹친 것을 지우고 맑은 물을 되살린다.</item>
/// <item>역설 「변기 수압을 점검해주십시오.」가 온 날이면 그 변기에 「점검」 미끼(<see cref="ParadoxLure"/>)가 물이 다 내려갈 때까지 붙는다.</item>
/// </list>
/// </summary>
public static class ToiletFlushCue
{
    /// <summary>물 내림 대역 ID.</summary>
    public const string FlushId = "prop.toilet.flush";

    /// <summary>소용돌이 무늬 색(맑은 물).</summary>
    public static readonly Color SwirlColor = new Color(0.82f, 0.9f, 0.93f, 0.55f);

    /// <summary>튀는 물방울 색.</summary>
    public static readonly Color SplashColor = new Color(0.8f, 0.88f, 0.92f, 0.85f);

    /// <summary>
    /// 플레이어 가까운 변기를 내린다(<paramref name="seconds"/> = 단서 길이). 그 변기에서 소리를 냈으면 <paramref name="soundPlayed"/> = true.
    /// 돌려주는 값은 그 변기 위치(변기가 없으면 <paramref name="fallback"/>).
    /// </summary>
    public static Vector3 Begin(MonoBehaviour host, Vector3 player, float seconds, Vector3 fallback, out bool soundPlayed)
    {
        soundPlayed = false;
        ToiletBowls bowls = ToiletBowls.Active;
        Transform toilet = bowls != null ? bowls.Nearest(player) : null;
        if (toilet == null) return fallback;

        ParadoxLure.Attach(toilet.gameObject, "T1", seconds);
        Vector3 at = toilet.position + Vector3.up * 0.4f;
        GameObject prefab = Resources.Load<GameObject>("StandIns/" + FlushId);
        if (prefab == null || host == null) return at;

        Transform water = bowls.WaterOf(toilet);
        Renderer clear = water != null ? water.GetComponent<Renderer>() : null;
        GameObject fx = Object.Instantiate(prefab, toilet.position, toilet.rotation);
        fx.name = "변기 물 내림 · " + toilet.name;
        if (toilet.name.StartsWith("ToiletC")) fx.transform.position += Vector3.down * ToiletBowls.ToiletCDrop;
        Clean(fx, clear != null ? clear.sharedMaterial : null);
        if (water != null) water.gameObject.SetActive(false);
        host.StartCoroutine(RestoreAfter(fx, water, seconds));
        soundPlayed = true;
        return at;
    }

    /// <summary>김진선님 물 내림 인스턴스를 맑은 물로 — 안쪽 변기 몸통은 끄고(씬 변기와 겹치지 않게), 물·소용돌이·물방울 색을 바꾸고, 소리를 연출 버스로.</summary>
    private static void Clean(GameObject fx, Material clearWater)
    {
        foreach (Transform child in fx.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "ToiletA" && child.GetComponent<LODGroup>() != null) child.gameObject.SetActive(false);
            Renderer r = child.GetComponent<Renderer>();
            if (child.name == "Water" && r != null && clearWater != null) r.sharedMaterial = clearWater;
            if (child.name == "Swirl" && r != null)
            {
                Material m = new Material(r.sharedMaterial);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", SwirlColor);
                r.material = m;
            }
        }

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = SplashColor;
        }

        foreach (AudioSource a in fx.GetComponentsInChildren<AudioSource>(true))
        {
            a.minDistance = 3f;
            a.maxDistance = 30f;
            NightDutyMixer.Route(a, NightDutyMixer.Bus.Direction);
        }
    }

    private static IEnumerator RestoreAfter(GameObject fx, Transform water, float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(seconds, 6.5f));
        if (fx != null) Object.Destroy(fx);
        if (water != null) water.gameObject.SetActive(true);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <b>CCTV 화면에만 보이는 것.</b> 이 컴포넌트를 단 오브젝트는 플레이어의 눈으로는 보이지 않고,
/// 경비실 모니터로 볼 때만 나타난다. 「화면에는 있는데 가 보면 없다」를 오브젝트 하나로 만든다.
///
/// <para><b>레이어를 쓰지 않는다.</b> 레이어를 새로 만들면 <c>TagManager.asset</c>(프로젝트 설정)을 고쳐야 한다.
/// 대신 <see cref="CctvSystem"/>이 카메라마다 그리기 직전·직후에 렌더러를 켜고 끈다
/// (<c>RenderPipelineManager.beginCameraRendering</c>). 그 밖의 시간에는 늘 꺼져 있다.</para>
///
/// <para><b>연출이 켜고 끈다.</b> 이 컴포넌트를 켜 두면 보이고, <c>SetActive(false)</c>나 <c>enabled = false</c>면
/// CCTV에서도 안 보인다. 특정 채널에서만 보이게 하려면 <see cref="Channel"/>을 정한다.</para>
///
/// <para>그림자는 던지지 않게 한다 — 보이지 않는 것이 바닥에 그림자를 떨구면 플레이어 눈에 들킨다.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CctvOnlyVisible : MonoBehaviour
{
    private static readonly List<CctvOnlyVisible> s_all = new List<CctvOnlyVisible>();

    [Tooltip("이 채널(0부터)에서만 보인다. -1이면 모든 채널에서 보인다.")]
    [SerializeField] private int channel = -1;

    private Renderer[] _renderers = new Renderer[0];

    /// <summary>보일 채널(0부터). -1이면 전부.</summary>
    public int Channel
    {
        get { return channel; }
        set { channel = value; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_all.Clear();   // 도메인 리로드를 꺼도 지난 플레이의 목록이 남지 않게.
    }

    private void OnEnable()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderers[i].enabled = false;
        }

        if (!s_all.Contains(this))
        {
            s_all.Add(this);
        }
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        SetVisible(false);
    }

    /// <summary>자식에 렌더러가 새로 붙었으면 다시 모은다.</summary>
    public void RefreshRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        SetVisible(false);
    }

    /// <summary>카메라 하나가 그리기 직전. CCTV 카메라면 해당하는 것만 켠다.</summary>
    internal static void BeforeCamera(int cctvChannel)
    {
        for (int i = 0; i < s_all.Count; i++)
        {
            CctvOnlyVisible v = s_all[i];
            if (v == null)
            {
                continue;
            }

            v.SetVisible(cctvChannel >= 0 && (v.channel < 0 || v.channel == cctvChannel));
        }
    }

    /// <summary>카메라 하나가 다 그린 뒤. 전부 다시 끈다.</summary>
    internal static void AfterCamera()
    {
        for (int i = 0; i < s_all.Count; i++)
        {
            if (s_all[i] != null)
            {
                s_all[i].SetVisible(false);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].enabled != visible)
            {
                _renderers[i].enabled = visible;
            }
        }
    }
}

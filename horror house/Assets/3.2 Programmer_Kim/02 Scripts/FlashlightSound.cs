using UnityEngine;

/// <summary>
/// 손전등을 켜고 끌 때 딸깍 소리를 낸다. 플레이어(FPController) 루트에 붙인다.
/// <para>
/// 손전등을 실제로 켜고 끄는 것은 <see cref="FlashlightRelay"/>(판정 신호 발신기, 플레이 중 자동 설치)다.
/// 그쪽은 자동으로 생겨 인스펙터에 소리를 꽂아 둘 수 없으므로, 소리는 프리팹에 저장되는 이 컴포넌트가 맡는다.
/// 발신기를 고치지 않고 <see cref="FlashlightRelay.IsOn"/>이 바뀌는 순간만 지켜본다 —
/// 그래서 태블릿을 연 동안처럼 발신기가 토글을 막은 때에는 소리도 나지 않는다(켜지지 않았는데 소리만 나지 않게).
/// </para>
/// <para>발신기가 새로 생기거나 바뀐 첫 프레임은 현재 상태만 기억하고 소리를 내지 않는다(씬 시작 때 딸깍 소리 방지).</para>
/// </summary>
[DisallowMultipleComponent]
public class FlashlightSound : MonoBehaviour
{
    [Tooltip("켤 때 소리.")]
    [SerializeField] private AudioClip onClip;
    [Tooltip("끌 때 소리.")]
    [SerializeField] private AudioClip offClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [Tooltip("비우면 이 오브젝트에 2D AudioSource를 만들어 쓴다.")]
    [SerializeField] private AudioSource audioSource;

    private FlashlightRelay watched;
    private bool lastOn;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;   // 손에 든 물건 소리라 좌우로 갈리지 않게
        }
    }

    private void Update()
    {
        FlashlightRelay relay = FlashlightRelay.Active;
        if (relay == null) return;

        // 발신기가 처음 보이거나 바뀌었으면 상태만 맞춘다.
        if (relay != watched)
        {
            watched = relay;
            lastOn = relay.IsOn;
            return;
        }

        bool on = relay.IsOn;
        if (on == lastOn) return;

        lastOn = on;
        AudioClip clip = on ? onClip : offClip;
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }
}

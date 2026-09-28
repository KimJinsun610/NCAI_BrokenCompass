using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 울렁임의 단일 창구. 활성화된 <see cref="ScreenWobbleSource"/> 중 가장 큰 세기를 셰이더 전역값으로 올린다.
/// <para>
/// 그리는 쪽은 <c>NightDuty/ScreenWobble</c> 셰이더를 쓰는 URP Full Screen Pass Renderer Feature다.
/// 이 컴포넌트는 씬에 <b>하나만</b> 둔다(PlaySystems 또는 플레이어 카메라).
/// </para>
/// <para>
/// 셰이더 시간은 내장 <c>_Time</c>이 아니라 여기서 <c>Time.deltaTime</c>으로 누적한 값을 쓴다.
/// 그래야 일시정지(<see cref="GamePause"/>, timeScale 0)에서 물결이 함께 멈춘다.
/// </para>
/// </summary>
public class ScreenWobble : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("UV가 흔들리는 최대 폭. 0.01 = 화면 폭의 1%. 너무 크면 가장자리가 찢어져 보인다.")]
    [SerializeField, Range(0f, 0.05f)] private float amplitude = 0.012f;
    [Tooltip("물결의 촘촘함. 클수록 잔물결, 작을수록 크게 출렁인다.")]
    [SerializeField, Range(0.5f, 30f)] private float frequency = 9f;
    [Tooltip("물결이 흐르는 속도 배율.")]
    [SerializeField, Range(0f, 5f)] private float speed = 1f;
    [Tooltip("가장자리 색 번짐(색수차) 세기. 0이면 끈다.")]
    [SerializeField, Range(0f, 0.05f)] private float chroma = 0.008f;

    [Header("옵션")]
    [Tooltip("전체 배율. 멀미 옵션을 여기에 연결한다. 0이면 효과가 꺼진다.")]
    [Range(0f, 1f)] public float userScale = 1f;

    [Header("테스트")]
    [Tooltip("0보다 크면 소스와 상관없이 이 세기로 흔든다. 모양을 튜닝할 때만 쓴다.")]
    [SerializeField, Range(0f, 1f)] private float previewWeight;

    private static readonly List<ScreenWobbleSource> Sources = new List<ScreenWobbleSource>();

    private static readonly int WeightId = Shader.PropertyToID("_WobbleWeight");
    private static readonly int TimeId = Shader.PropertyToID("_WobbleTime");
    private static readonly int AmplitudeId = Shader.PropertyToID("_WobbleAmplitude");
    private static readonly int FrequencyId = Shader.PropertyToID("_WobbleFrequency");
    private static readonly int ChromaId = Shader.PropertyToID("_WobbleChroma");

    private float wobbleTime;

    /// <summary>지금 화면에 적용 중인 세기(0~1). 다른 연출이 참고할 때 읽는다.</summary>
    public static float CurrentWeight { get; private set; }

    public static void Register(ScreenWobbleSource source)
    {
        if (source != null && !Sources.Contains(source)) Sources.Add(source);
    }

    public static void Unregister(ScreenWobbleSource source)
    {
        Sources.Remove(source);
    }

    // 도메인 리로드를 끈 에디터에서 지난 플레이의 소스가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Sources.Clear();
        CurrentWeight = 0f;
    }

    private void Update()
    {
        float weight = previewWeight;
        for (int i = Sources.Count - 1; i >= 0; i--)
        {
            ScreenWobbleSource source = Sources[i];
            if (source == null)
            {
                Sources.RemoveAt(i);
                continue;
            }
            if (source.weight > weight) weight = source.weight;
        }

        weight = Mathf.Clamp01(weight) * userScale;
        CurrentWeight = weight;

        // 쉬는 동안에는 시간을 흘리지 않는다 — 값이 무한히 커져 float 정밀도가 떨어지는 것을 막는다.
        if (weight > 0f) wobbleTime += Time.deltaTime * speed;

        Shader.SetGlobalFloat(WeightId, weight);
        Shader.SetGlobalFloat(TimeId, wobbleTime);
        Shader.SetGlobalFloat(AmplitudeId, amplitude);
        Shader.SetGlobalFloat(FrequencyId, frequency);
        Shader.SetGlobalFloat(ChromaId, chroma);
    }

    // 씬 전환 · 플레이 종료 뒤에 전역값이 남아 다음 씬(또는 Scene 뷰)이 흔들리지 않게 한다.
    private void OnDisable()
    {
        CurrentWeight = 0f;
        Shader.SetGlobalFloat(WeightId, 0f);
    }
}

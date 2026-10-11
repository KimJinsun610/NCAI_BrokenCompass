using UnityEngine;

namespace NightDuty.SuccessEnding
{
    // 성공 엔딩 7~9번: 담당자 목소리가 수화기 너머가 아니라 "귓가 바로 옆"에서 들리게 하는 처리.
    // 전화 대역 필터(300~3400Hz)를 쓰지 않고, 저역을 조금 덜어내고 고역(숨소리·치찰음)을 살짝 올린 뒤
    // 가벼운 컴프레서로 가까이 붙은 마이크처럼 납작하게 만든다. 리버브 없음, 2D.
    // 권장 순서: AudioSource(2D) -> (선택) UncannyVoiceFX(아주 약한 비인간 레이어) -> CloseVoiceFX
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CloseVoiceFX : MonoBehaviour
    {
        [Header("EQ")]
        [Tooltip("이 주파수 아래를 2차 하이패스로 자른다(웅웅거림 제거).")]
        [Range(20f, 400f)] public float highPassHz = 110f;
        [Range(60f, 600f)] public float lowShelfHz = 220f;
        [Tooltip("저역 셸프(dB). 음수 = 저역을 덜어냄.")]
        [Range(-18f, 6f)] public float lowShelfDb = -5f;
        [Range(1500f, 9000f)] public float highShelfHz = 3800f;
        [Tooltip("고역 셸프(dB). 양수 = 숨소리·치찰음이 가까이 들림.")]
        [Range(-6f, 12f)] public float highShelfDb = 4f;

        [Header("Compressor")]
        [Range(-40f, 0f)] public float thresholdDb = -22f;
        [Range(1f, 10f)] public float ratio = 3f;
        [Range(0.5f, 50f)] public float attackMs = 5f;
        [Range(10f, 400f)] public float releaseMs = 90f;
        [Range(0f, 18f)] public float makeupDb = 5f;
        [Tooltip("출력 소프트 클리핑(디지털 클리핑 방지).")]
        public bool softClip = true;

        private const int MaxChannels = 8;
        private int sampleRate = 48000;
        // 3개 바이쿼드(하이패스, 로우셸프, 하이셸프) 계수: b0 b1 b2 a1 a2
        private volatile float[] coeffs;
        private readonly float[] z = new float[3 * 2 * MaxChannels];
        private float envelope;
        private volatile float pThreshold, pRatio, pAttack, pRelease, pMakeup;
        private volatile bool pSoftClip;
        private float lastHash = float.NaN;

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            Push(true);
        }

        private void OnValidate() { if (Application.isPlaying) Push(true); }
        private void Update() => Push(false);

        private void Push(bool force)
        {
            pThreshold = thresholdDb; pRatio = Mathf.Max(1f, ratio); pMakeup = Mathf.Pow(10f, makeupDb / 20f);
            pAttack = Mathf.Exp(-1f / (Mathf.Max(0.1f, attackMs) * .001f * sampleRate));
            pRelease = Mathf.Exp(-1f / (Mathf.Max(1f, releaseMs) * .001f * sampleRate));
            pSoftClip = softClip;
            float hash = highPassHz * 1.3f + lowShelfHz * 7.1f + lowShelfDb * 13.7f + highShelfHz * .37f + highShelfDb * 31.3f;
            if (!force && hash == lastHash) return;
            lastHash = hash;
            var c = new float[15];
            HighPass(c, 0, highPassHz, .7071f);
            Shelf(c, 5, lowShelfHz, lowShelfDb, false);
            Shelf(c, 10, highShelfHz, highShelfDb, true);
            coeffs = c; // 참조 교체(오디오 스레드는 다음 블록부터 새 계수를 씀)
        }

        // RBJ Audio EQ Cookbook
        private void HighPass(float[] c, int o, float f, float q)
        {
            float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, sampleRate * .45f) / sampleRate, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q);
            float a0 = 1f + al;
            c[o] = (1f + cs) / 2f / a0; c[o + 1] = -(1f + cs) / a0; c[o + 2] = (1f + cs) / 2f / a0;
            c[o + 3] = -2f * cs / a0; c[o + 4] = (1f - al) / a0;
        }

        private void Shelf(float[] c, int o, float f, float db, bool high)
        {
            float A = Mathf.Pow(10f, db / 40f);
            float w = 2f * Mathf.PI * Mathf.Clamp(f, 10f, sampleRate * .45f) / sampleRate, cs = Mathf.Cos(w), sn = Mathf.Sin(w);
            float al = sn / 2f * Mathf.Sqrt(2f), sq = 2f * Mathf.Sqrt(A) * al;
            float b0, b1, b2, a0, a1, a2;
            if (high)
            {
                b0 = A * ((A + 1) + (A - 1) * cs + sq); b1 = -2 * A * ((A - 1) + (A + 1) * cs); b2 = A * ((A + 1) + (A - 1) * cs - sq);
                a0 = (A + 1) - (A - 1) * cs + sq; a1 = 2 * ((A - 1) - (A + 1) * cs); a2 = (A + 1) - (A - 1) * cs - sq;
            }
            else
            {
                b0 = A * ((A + 1) - (A - 1) * cs + sq); b1 = 2 * A * ((A - 1) - (A + 1) * cs); b2 = A * ((A + 1) - (A - 1) * cs - sq);
                a0 = (A + 1) + (A - 1) * cs + sq; a1 = -2 * ((A - 1) + (A + 1) * cs); a2 = (A + 1) + (A - 1) * cs - sq;
            }
            c[o] = b0 / a0; c[o + 1] = b1 / a0; c[o + 2] = b2 / a0; c[o + 3] = a1 / a0; c[o + 4] = a2 / a0;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var c = coeffs;
            if (c == null || channels <= 0 || channels > MaxChannels) return;
            int frames = data.Length / channels;
            float thr = pThreshold, slope = 1f - 1f / pRatio, att = pAttack, rel = pRelease, makeup = pMakeup;
            bool clip = pSoftClip;
            for (int f = 0; f < frames; f++)
            {
                int offset = f * channels;
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float x = data[offset + ch];
                    for (int s = 0; s < 3; s++)
                    {
                        int o = s * 5, zi = (s * MaxChannels + ch) * 2;
                        // Transposed Direct Form II
                        float y = c[o] * x + z[zi];
                        z[zi] = c[o + 1] * x - c[o + 3] * y + z[zi + 1];
                        z[zi + 1] = c[o + 2] * x - c[o + 4] * y;
                        x = y;
                    }
                    data[offset + ch] = x;
                    float a = x < 0 ? -x : x;
                    if (a > peak) peak = a;
                }
                envelope = peak > envelope ? att * envelope + (1f - att) * peak : rel * envelope + (1f - rel) * peak;
                float levelDb = 20f * Mathf.Log10(envelope + 1e-9f);
                float gainDb = levelDb > thr ? -(levelDb - thr) * slope : 0f;
                float gain = Mathf.Pow(10f, gainDb / 20f) * makeup;
                for (int ch = 0; ch < channels; ch++)
                {
                    float y = data[offset + ch] * gain;
                    if (clip) y = y / (1f + Mathf.Abs(y) * .3f);
                    data[offset + ch] = y;
                }
            }
        }
    }
}

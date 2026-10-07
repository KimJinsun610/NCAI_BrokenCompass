using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightDuty.PrologueSample
{
    // Independent preview only: does not change the existing menu or SceneFlow configuration.
    public sealed class DispatchCallSample : MonoBehaviour
    {
        public const string ContractPath = "Assets/1. Design/01 Scene/ContractScene_hyun.unity";
        public static readonly string[] Lines = {
            "안녕하세요. 현장관리팀 파견 담당자입니다.",
            "지원하신 야간 근무 건으로 연락드렸습니다.",
            "이번 현장은 철거를 닷새 앞둔 폐교입니다.",
            "사전에 안내드린 대로, 철거 전 시설 점검이 필요한 장소입니다.",
            "철거 전까지 야간에 건물 상태를 점검하고 기록할 인원이 필요합니다.",
            "이전 근무자는 개인 사정으로 근무를 이어 가지 못하게 되어서요.",
            "자정부터 네 시까지 1층을 순찰하고, 지정된 대상을 점검해 태블릿으로 보고해 주시면 됩니다.",
            "근무 중에는 태블릿으로 점검표와 문자가 전달됩니다.",
            "계약서를 보내드리겠습니다.",
            "단순 점검 업무라 어려운 일은 없으실 겁니다.",
            "그럼 첫날 밤, 잘 부탁드립니다."
        };

        [SerializeField] private GameObject titlePanel, callPanel;
        [SerializeField] private TMP_Text stateText, subtitle, counter, actionLabel;
        [SerializeField] private UnityEngine.UI.Button playButton, actionButton, skipButton;
        [SerializeField] private CanvasGroup callGroup, fade;
        [SerializeField] private RectTransform[] bars;
        [SerializeField] private AudioSource voice, effects, ambience;
        [SerializeField] private AudioClip[] voiceClips;
        public string Stage { get; private set; } = "Title";
        public int CurrentLine { get; private set; } = -1;
        private AudioClip ring, pickup, endTone, message, roomTone;
        private readonly float[] samples = new float[128];

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            ring = Resources.Load<AudioClip>("PhoneAudio/Phone_ring_3");
            pickup = Resources.Load<AudioClip>("PhoneAudio/Call_connect_1");
            endTone = pickup;
            if (ring == null) Debug.LogError("Phone ring audio was not found at Resources/PhoneAudio/Phone_ring_3.", this);
            if (pickup == null) Debug.LogError("Call connect audio was not found at Resources/PhoneAudio/Call_connect_1.", this);
            message = Tone("Document notification", .38f, 880, 1320, .12f);
            roomTone = Tone("Quiet office and telephone line", 4, 60, 120, .009f);
            ambience.clip = roomTone;
            ambience.loop = true;
            titlePanel.SetActive(false);
            callPanel.SetActive(true);
            playButton.gameObject.SetActive(false);
            actionButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            fade.alpha = 0;
            fade.blocksRaycasts = false;
        }

        private void Start()
        {
            StartCoroutine(Call());
        }

        private IEnumerator Call()
        {
            Stage = "Ringing";
            yield return Fade(1, .4f);
            titlePanel.SetActive(false);
            callPanel.SetActive(true);
            callGroup.alpha = 1;
            stateText.text = "수신 전화";
            subtitle.text = "......";
            counter.text = "현장관리팀 · 파견 담당자";
            actionButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            yield return Fade(0, .6f);
            for (int i = 0; i < 2; i++)
            {
                if (ring != null) effects.PlayOneShot(ring);
                yield return new WaitForSecondsRealtime(ring != null ? ring.length + .15f : 1.9f);
            }
            if (pickup != null) effects.PlayOneShot(pickup);
            yield return new WaitForSecondsRealtime(.5f);
            ambience.Play();
            Stage = "Talking";
            stateText.text = "통화 연결됨";
            actionButton.gameObject.SetActive(false);
            for (int i = 0; i < Lines.Length; i++)
            {
                CurrentLine = i;
                subtitle.text = Lines[i];
                subtitle.maxVisibleCharacters = 0;
                counter.text = "현장관리팀 · 파견 담당자";
                voice.clip = i < voiceClips.Length ? voiceClips[i] : null;
                if (voice.clip != null) voice.Play();
                // Line timing follows the clip length (divided by pitch, since UncannyVoiceFX lowers it).
                float duration = voice.clip != null ? voice.clip.length / Mathf.Max(.1f, Mathf.Abs(voice.pitch)) : Mathf.Max(3, Lines[i].Length * .12f);
                float elapsed = 0;
                while (elapsed < duration + 1.1f || (voice.isPlaying && elapsed < duration + 3f))
                {
                    elapsed += Time.unscaledDeltaTime;
                    subtitle.maxVisibleCharacters = Mathf.CeilToInt(elapsed * 28);
                    yield return null;
                }
                voice.Stop();
                subtitle.maxVisibleCharacters = int.MaxValue;
                yield return new WaitForSecondsRealtime(.18f);
            }
            Stage = "Ending";
            actionButton.gameObject.SetActive(false);
            ambience.Stop();
            if (endTone != null) effects.PlayOneShot(endTone);
            stateText.text = "통화 종료";
            subtitle.text = "";
            yield return new WaitForSecondsRealtime(1.25f);
            ShowDocument();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return OpenContract();
        }

        private void ShowDocument()
        {
            Stage = "Document";
            effects.PlayOneShot(message);
            stateText.text = "문서가 도착했습니다";
            counter.text = "발신 · 현장관리팀";
            subtitle.maxVisibleCharacters = int.MaxValue;
            subtitle.text = "파견근무 계약서가 도착했습니다.";
            actionButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
        }

        private IEnumerator OpenContract()
        {
            Stage = "Loading";
            actionButton.interactable = false;
            yield return Fade(1, .65f);
            // Same in-memory new-run initialization as the existing main menu.
            GameSession.StartNewRun();
#if UNITY_EDITOR
            var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ContractPath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (!Application.CanStreamedLevelBeLoaded(ContractPath))
            {
                Stage = "Document";
                subtitle.text = "계약서 씬이 빌드 목록에 없습니다.\n이 샘플은 에디터에서 먼저 확인해 주세요.";
                yield return Fade(0, .3f);
                yield break;
            }
            var loading = SceneManager.LoadSceneAsync(ContractPath);
#endif
            while (loading != null && !loading.isDone) yield return null;
        }

        private IEnumerator Fade(float target, float seconds)
        {
            fade.blocksRaycasts = true;
            float start = fade.alpha;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            fade.alpha = target;
            fade.blocksRaycasts = target > .99f;
        }

        private void Update()
        {
            float level = 0;
            AudioSource waveSource = Stage == "Ringing" ? effects : voice;
            if (waveSource.isPlaying)
            {
                waveSource.GetOutputData(samples, 0);
                for (int i = 0; i < samples.Length; i++) level += samples[i] * samples[i];
                level = Mathf.Sqrt(level / samples.Length) * 18;
            }
            for (int i = 0; i < bars.Length; i++)
            {
                float pulse = level;
                bars[i].sizeDelta = new Vector2(3, 5 + Mathf.Min(1, pulse) * (12 + 28 * Mathf.Abs(Mathf.Sin(i * 1.7f + Time.unscaledTime * 7))));
            }
        }

        private static AudioClip Tone(string label, float seconds, float f1, float f2, float gain, bool pulse = false)
        {
            const int rate = 24000;
            float[] data = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(37);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Min(1, t * 80) * Mathf.Min(1, (seconds - t) * 60);
                float gate = pulse && t % .5f > .34f ? 0 : 1;
                data[i] = gain * envelope * gate * (.45f * Mathf.Sin(2 * Mathf.PI * f1 * t) + .45f * Mathf.Sin(2 * Mathf.PI * f2 * t) + .10f * ((float)random.NextDouble() * 2 - 1));
            }
            var clip = AudioClip.Create(label, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var clip in new[] { message, roomTone })
                if (clip != null) Destroy(clip);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using NightDuty.PrologueSample;

namespace NightDuty.SuccessEnding
{
    public enum EndingLineMode
    {
        Centered,   // 화면 가운데에 떴다가 사라짐 (프롤로그 방식)
        RandomStay  // 무작위 위치에 튀어나와 남음 (몰아치기 10~14)
    }

    // 음성을 어떤 소스(처리)로 재생할지
    public enum EndingVoiceRoute
    {
        Telephone,  // 담당자 통화 음성: 프롤로그와 같은 UncannyVoiceFX + 전화 대역 필터 (4~6, 16~17)
        CloseToEar, // 귓가 바로 옆: 전화 필터 없음, 2D, 저역↓ 고역↑ + 컴프레서, 리버브 없음 (7~9)
        Clean,      // 너무 깨끗한 목소리: 필터·글리치·더블링 전부 없음 (19)
        Crowd       // 몰아치기 목소리 풀 (10~14)
    }

    [Serializable]
    public class EndingLine
    {
        public string id = "";
        [TextArea(1, 3)] public string text = "";
        public EndingLineMode mode = EndingLineMode.Centered;
        [Tooltip("기획서의 시간(초).")]
        [Min(0)] public float duration = 3f;
        [Tooltip("음성이 기획 시간보다 길면 음성이 끝날 때까지 줄을 늘립니다. 몰아치기 10~14는 겹침이 의도이므로 끕니다.")]
        public bool extendToVoice = true;
        [Tooltip("동시에(또는 voiceStagger 간격으로) 재생할 음성. 여러 개면 겹쳐 들립니다.")]
        public AudioClip[] voices = new AudioClip[0];
        [Min(0)] public float voiceStagger = 0f;
        [Range(0, 1)] public float voiceVolume = 0.9f;
        [Tooltip("Telephone=전화 필터+UncannyVoiceFX, CloseToEar=귓가(필터 없음), Clean=완전 깨끗, Crowd=몰아치기 풀.")]
        public EndingVoiceRoute voiceRoute = EndingVoiceRoute.Telephone;
        [Tooltip("Centered: 줄이 끝나기 이 시간 전에 글자를 지웁니다(음성이 끝나기 전에는 지우지 않음).")]
        [Min(0)] public float hideBeforeEnd = 0.2f;

        [Header("RandomStay (무작위 위치)")]
        [Min(1)] public int copies = 1;
        [Tooltip("copies개를 이 시간 동안 나눠서 띄웁니다. 0이면 동시에.")]
        [Min(0)] public float spawnSpread = 0f;
        [Tooltip("화면 가장자리 쪽에서 튀어나오게 합니다(사방에서).")]
        public bool preferEdges = false;
        [Tooltip("화면을 수칙 글씨로 가득 채웁니다(14번).")]
        public bool flood = false;
        [Tooltip("이전 몰아치기 줄의 음성까지 한꺼번에 겹쳐 재생합니다(14번 '모든 목소리 겹침').")]
        public bool alsoPlayEarlierCrowdVoices = false;

        [Header("몰아치기 사운드 목표값 (이 줄이 끝날 때까지 서서히 도달)")]
        public bool driveBuildUpAudio = false;
        [Range(0, 1)] public float lineNoise = 0.35f;
        [Range(0, 1)] public float staticNoise = 0f;
        [Tooltip("심박 간격(초). 0이면 심박 없음.")]
        [Min(0)] public float heartbeatInterval = 0f;
        [Range(0, 1)] public float heartbeatVolume = 0.8f;
        [Tooltip("상승음(텐션) 크기. 0보다 커지는 첫 줄에서 상승음이 시작됩니다.")]
        [Range(0, 1)] public float tensionVolume = 0f;

        [Header("음성 깨짐 (17번: 깨끗하게 녹음 → 엔진에서 깨뜨림, Telephone 전용)")]
        [Tooltip("음성 클립 안에서 깨지기 시작하는 시각(초). 음수면 깨짐 없음. 17번은 '보낸' 시작 = 2.04초.")]
        public float breakAtVoiceTime = -1f;
        [Tooltip("음성 클립 안에서 모든 소리(음성·회선 잡음)를 끊는 시각(초). '없…' 도중.")]
        public float breakCutAtVoiceTime = -1f;
        [Tooltip("자막에서 흔들림·글자 깨짐이 시작되는 글자 위치(0부터). 음수면 처음부터.")]
        public int breakTextIndex = -1;
    }

    // 15번 끊김 프레임에 정점이 오도록 거꾸로 시작 시각을 맞추는 상승음 레이어.
    [Serializable]
    public class CutRiser
    {
        public string name = "";
        public AudioSource source;
        public AudioClip clip;
        [Tooltip("클립 안에서 가장 큰(정점) 지점(초). 이 지점이 15번 끊김 프레임에 옵니다.")]
        [Min(0)] public float peakTime = 3f;
        [Tooltip("시작할 때 → 끊김 프레임의 볼륨. 끝까지 커지기만 합니다.")]
        public Vector2 volume = new Vector2(.5f, 1f);
        public float pitch = 1f;
    }

    // 성공 엔딩: 검은 화면 + 전화 + 담당자 TTS + 수칙 몰아치기 + 정적 + 마지막 문장.
    // 기획 원본: 기획 자료/야간근무_성공엔딩.md
    public sealed class SuccessEndingSequence : MonoBehaviour
    {
        [Header("UI")]
        public RectTransform randomTextLayer;
        public TextMeshProUGUI centerText;
        public TextMeshProUGUI finalText;
        [Tooltip("자막(대사) 폰트: 프롤로그와 같은 Noto Serif KR 자막 폰트.")]
        public TMP_FontAsset subtitleFont;
        [Tooltip("수칙 글씨(몰아치기)와 23~25번 태블릿 문장에 쓰는 '수칙지' 폰트.")]
        public TMP_FontAsset font;
        [Tooltip("25번 번쩍임용 전체 화면 흰 이미지(알파 0으로 시작).")]
        public UnityEngine.UI.Graphic flashOverlay;
        public Color textColor = new Color(.84f, .84f, .84f, 1f);
        [Tooltip("프롤로그 자막과 같은 크기.")]
        public float centerFontSize = 34f;
        [Tooltip("프롤로그처럼 글자가 타자 치듯 나타나는 속도(초당 글자). 0이면 한 번에.")]
        public float typeCharsPerSecond = 28f;
        public Vector2 randomFontSize = new Vector2(30f, 54f);
        public Vector2 floodFontSize = new Vector2(24f, 66f);
        [Range(0, 30)] public float randomRotation = 5f;
        public float popScale = 1.35f;
        public float popTime = 0.08f;

        [Header("Audio Sources")]
        public AudioSource effects;
        public AudioSource callLine;
        public AudioSource lineStatic;
        public AudioSource heartbeat;
        public AudioSource tension;
        public AudioSource riser;
        public AudioSource hit;
        [Tooltip("통화 음성(4~6, 16~17): UncannyVoiceFX + 왜곡 + 코러스 + 하이패스 300 + 로우패스 3400 (프롤로그와 같은 설정).")]
        public AudioSource dispatcherVoice;
        [Tooltip("귓가 음성(7~9): 2D, 전화 필터 없음, CloseVoiceFX(저역↓ 고역↑ 컴프레서) + 아주 약한 UncannyVoiceFX.")]
        public AudioSource closeVoice;
        [Tooltip("깨끗한 음성(19): 필터·이펙트 없음.")]
        public AudioSource cleanVoice;
        public AudioSource[] crowdVoices = new AudioSource[0];
        [Tooltip("24번 태블릿 글리치 소리(앞부분만 재생하고 끊음).")]
        public AudioSource tabletGlitch;
        [Tooltip("26번 이명 / 블랙아웃 꼬리.")]
        public AudioSource tinnitus;
        public AudioSource blackout;

        [Header("Clips")]
        public AudioClip phoneRing;
        public AudioClip callConnect;
        [Tooltip("통화 끊김 소리. 비우면 callConnect를 씁니다(프롤로그와 같음).")]
        public AudioClip hangUp;
        public AudioClip callLineLoop;
        public AudioClip staticLoop;
        public AudioClip heartbeatOne;
        public AudioClip tensionClip;
        [Tooltip("텐션 클립 안에서 재생을 시작할 위치(초).")]
        public float tensionClipStart = 0.6f;
        [Tooltip("상승음이 시작될 때와 끊길 때의 재생 속도(음높이도 같이 올라감).")]
        public Vector2 tensionPitch = new Vector2(1f, 1.45f);
        public AudioClip riserClip;
        [Tooltip("riserClip 안에서 정점(끊기는 지점)의 시각. 이 지점이 15번 끊김 프레임에 오도록 시작 시각을 맞춥니다.")]
        public float riserPeakTime = 3.30f;
        [Range(0, 1)] public float riserVolume = 0.85f;
        [Tooltip("끊김 프레임에서의 상승음 볼륨(시작 → 끊김까지 계속 커짐).")]
        [Range(0, 1)] public float riserVolumeAtCut = 1f;
        [Tooltip("추가 상승음 레이어. 몰아치기가 길어도 끊김 직전까지 소리가 비지 않게 겹칩니다.")]
        public List<CutRiser> extraCutRisers = new List<CutRiser>();
        public AudioClip notification;
        public AudioClip climaxHit;
        [Tooltip("climaxHit 안에서 첫 타격이 있는 시각(초). v3 믹스의 얼굴 컷 = 10.067초.")]
        public float climaxHitStart = 10.067f;
        [Tooltip("히트를 쓸 최대 길이(초). v3는 13.367초에 복도 소리로 돌아가므로 그 전에 끝냅니다.")]
        public float climaxHitMaxLength = 3.25f;
        [Range(0, 1)] public float climaxHitVolume = 1f;
        [Tooltip("히트 위에 함께 얹을 추가 소리(선택).")]
        public AudioClip[] climaxHitExtraLayers = new AudioClip[0];

        [Header("Timeline 1~3: 전화")]
        public float openingSilence = 2f;
        public int ringCount = 2;
        public float ringBlock = 4f;
        public float connectBlock = 1f;
        [Range(0, 1)] public float callLineVolume = 0.35f;

        [Header("Timeline 4~6: 통화")]
        public List<EndingLine> callLines = new List<EndingLine>();

        [Header("Timeline 7~14: 몰아치기")]
        public List<EndingLine> buildUpLines = new List<EndingLine>();
        [Tooltip("14번에서 화면을 채울 수칙 문장들.")]
        [TextArea(1, 2)] public string[] floodTexts = new string[0];
        [Tooltip("buildUpLines 중 flood=true인 줄에서 한꺼번에 띄울 개수(기본은 지속 플러드를 써서 꺼 둠).")]
        public int floodCount = 160;

        [Header("Timeline 14+: 지속 플러드 (14번 뒤, 15번 끊김 직전까지)")]
        [Tooltip("14번 뒤에 이어지는 지속 플러드 길이(초). 0이면 없음.")]
        [Min(0)] public float floodDuration = 4.8f;
        [Tooltip("초당 글자 생성 수: 시작 → 끊김 직전. (60fps 기준 220/s ≈ 프레임당 3~4개)")]
        public Vector2 floodSpawnRate = new Vector2(10f, 220f);
        [Tooltip("생성 속도 가속 곡선(1=일정하게, 2 이상=뒤로 갈수록 몰아침).")]
        [Range(0.3f, 5f)] public float floodSpawnCurve = 2f;
        [Tooltip("안전 상한(이 이상은 만들지 않음).")]
        public int floodMaxTexts = 900;
        [Tooltip("플러드 끝 무렵 글자 크기 범위(시작 범위는 floodFontSize).")]
        public Vector2 floodFontSizeEnd = new Vector2(20f, 120f);
        [Tooltip("floodTexts에 몰아치기 7~14 줄의 문장도 합쳐서 씁니다.")]
        public bool floodIncludeBuildUpTexts = true;
        [Tooltip("초당 목소리 재생(재트리거) 수: 시작 → 끊김 직전.")]
        public Vector2 floodVoiceRate = new Vector2(8f, 22f);
        [Tooltip("플러드 시작 때 최소 동시 목소리 수. 이보다 적게 재생 중이면 바로 새 목소리를 겹칩니다(14번 직후 얇아지지 않게).")]
        public int floodMinActiveVoices = 12;
        [Tooltip("끊김 직전의 최소 동시 목소리 수(시작 값에서 점점 늘어남).")]
        public int floodMinActiveVoicesEnd = 20;
        [Tooltip("재트리거 목소리의 음높이 범위.")]
        public Vector2 floodVoicePitch = new Vector2(.9f, 1.1f);
        [Tooltip("재트리거 목소리 볼륨: 시작 → 끊김 직전.")]
        public Vector2 floodVoiceVolume = new Vector2(.5f, .8f);
        [Tooltip("클립 길이 대비 무작위 시작 위치 최대값(0~1).")]
        [Range(0, 0.9f)] public float floodVoiceMaxStartOffset = .35f;
        [Tooltip("담당자 7~9 목소리도 재트리거 목록에 넣습니다.")]
        public bool floodVoiceIncludeDispatcher = true;
        [Tooltip("추가로 재트리거할 목소리(선택).")]
        public AudioClip[] floodExtraVoices = new AudioClip[0];
        [Header("지속 플러드 사운드 목표값 (끊김 프레임에 도달)")]
        [Range(0, 1)] public float floodLineNoise = 1f;
        [Range(0, 1)] public float floodStaticNoise = .85f;
        [Min(0)] public float floodHeartbeatInterval = .14f;
        [Range(0, 1)] public float floodHeartbeatVolume = 1f;
        [Range(0, 1)] public float floodTensionVolume = 1f;

        [Header("Timeline 15: 정적")]
        public float cutSilence = 2f;

        [Header("Timeline 16~17")]
        [Range(0, 1)] public float returnCallLineVolume = 0.15f;
        public float returnCallLineFadeIn = 0.4f;
        public List<EndingLine> afterLines = new List<EndingLine>();
        [Header("17번 깨짐 세기")]
        [Tooltip("깨짐 끝 무렵 음높이(끌려 내려감).")]
        [Range(0.4f, 1f)] public float breakPitchEnd = 0.68f;
        [Range(0, 1)] public float breakDistortion = 0.93f;
        [Tooltip("스터터·끊김 글리치 사이 간격(초) 범위.")]
        public Vector2 breakGlitchInterval = new Vector2(0.09f, 0.22f);
        public Vector2 breakGlitchDuration = new Vector2(0.08f, 0.2f);
        [Range(15f, 120f)] public float breakStutterSliceMs = 65f;
        [Tooltip("깨짐 동안 회선 잡음이 치솟는 목표값.")]
        [Range(0, 1)] public float breakLineNoise = 0.8f;
        [Range(0, 1)] public float breakStaticNoise = 0.55f;
        [Tooltip("자막 흔들림(px): 시작 → 끊김 직전.")]
        public Vector2 breakShake = new Vector2(2f, 11f);
        [Tooltip("자막 글자 깨짐 확률: 시작 → 끊김 직전.")]
        public Vector2 breakCorruptChance = new Vector2(0.12f, 0.55f);
        [Tooltip("깨진 글자 대신 보일 문자들(자막 폰트 아틀라스에 들어 있어야 함).")]
        public string breakGlyphs = "▒▓░█ㅂㄴㅈㅇ낸보뵈냄";

        [Header("Timeline 18~22")]
        [Tooltip("18. 회선 소리까지 완전 무음.")]
        public float preCleanSilence = 0.8f;
        [Tooltip("19. '고생하셨습니다.' (cleanVoice, 회선 소리 없음)")]
        public List<EndingLine> cleanLines = new List<EndingLine>();
        public float hangUpBlock = 0.5f;
        public float postHangUpSilence = 1f;
        public float notificationBlock = 1f;

        [Header("Timeline 23~25: 태블릿 문장 (수칙지 폰트)")]
        public string tabletCursorText = "인수인계";
        public string cursorGlyph = "_";
        public float cursorBlinkInterval = 0.25f;
        public float cursorDuration = 1f;
        [TextArea(1, 2)] public string tabletText = "인수인계가 진행됩니다.";
        public float tabletTextDuration = 1.5f;
        public float finalFontSize = 72f;
        public AudioClip tabletGlitchClip;
        [Tooltip("글리치 소리 앞부분만 쓸 길이(초). SFX_TABLET_Glitch는 0.7초까지가 첫 덩어리.")]
        public float tabletGlitchLength = 0.72f;
        [Range(0, 1)] public float tabletGlitchVolume = 0.8f;
        [Tooltip("25. 번쩍임 + 흔들림 + 사라짐 + 히트 블록.")]
        public float climaxBlock = 1.5f;
        public Color flashTextColor = Color.white;
        [Range(0, 1)] public float flashOverlayAlpha = 0.45f;
        public float flashOverlayDecay = 0.16f;
        public float shakeAmplitude = 34f;
        [Tooltip("이 시간 동안 세게 흔들린 뒤 글자가 사라진다.")]
        public float shakeDuration = 0.45f;
        public AudioClip youDiedClip;
        [Range(0, 1)] public float youDiedVolume = 0.9f;

        [Header("Timeline 26: 꼬리")]
        public float tailDuration = 3f;
        [Tooltip("26 시작에서 히트·YouDied 잔향을 줄이는 시간(초).")]
        public float hitFadeOut = 1f;
        public AudioClip tinnitusClip;
        [Range(0, 1)] public float tinnitusVolume = 0.6f;
        [Tooltip("이명이 0까지 줄어드는 시간(초).")]
        public float tinnitusFade = 2.4f;
        public AudioClip blackoutClip;
        [Range(0, 1)] public float blackoutVolume = 0.35f;
        public float blackoutDelay = 0.5f;

        [Header("Timeline 22~27: 사망 암시 사운드 (런타임 소스 사용, 씬 수정 없음)")]
        [Tooltip("켜면 22번 진동과 24~27번을 아래 사망 사운드 설계로 재생합니다. 끄면 예전 방식(위 Timeline 23~26 값)으로 재생합니다.")]
        public bool useDeathSoundDesign = true;
        [Tooltip("비우면 Resources/SuccessEndingDeath/에서 자동으로 불러옵니다.")]
        public AudioClip deathNotificationClip;
        public AudioClip deathHeartbeatClip;
        public AudioClip deathBreathClip;
        public AudioClip deathBodyFallClip;
        public AudioClip deathFlashlightClip;
        [Tooltip("22. 진동 알림 전용 2D 소스 볼륨.")]
        [Range(0, 1)] public float deathNotificationVolume = 1f;
        [Tooltip("24. '인수인계가 진행됩니다.'가 뜬 뒤 클라이맥스까지(심장·호흡이 커지고 빨라지는 시간).")]
        public float deathBuildUpDuration = 2.6f;
        public Vector2 deathHeartbeatVolume = new Vector2(.35f, 1f);
        public Vector2 deathHeartbeatPitch = new Vector2(1f, 1.06f);
        public Vector2 deathBreathVolume = new Vector2(.15f, .95f);
        public Vector2 deathBreathPitch = new Vector2(1f, 1.25f);
        [Tooltip("호흡 클립 시작 위치(초). 클라이맥스가 숨 들이쉬는 도중에 오도록 맞춘 값.")]
        public float deathBreathStart = .1f;
        [Tooltip("24번 동안 밑에 깔리는 글리치 볼륨.")]
        [Range(0, 1)] public float deathGlitchVolume = .45f;
        [Tooltip("25. 번쩍임 뒤 쓰러짐·태블릿 낙하가 들리기까지(초).")]
        public float deathFoleyDelay = .3f;
        [Range(0, 1)] public float deathBodyFallVolume = 1f;
        [Tooltip("몸 쓰러짐 소리 시작 뒤 손전등 낙하가 시작되기까지(초).")]
        public float deathFlashlightDelay = .95f;
        [Range(0, 1)] public float deathFlashlightVolume = .8f;
        [Tooltip("손전등 소리 안에서 구르기가 시작/끝나는 시각(초). 이 구간 동안 좌→우로 이동.")]
        public Vector2 deathRollTime = new Vector2(.75f, 1.9f);
        [Tooltip("손전등 팬: 떨어진 위치 → 굴러가 멈춘 위치.")]
        public Vector2 deathRollPan = new Vector2(-.3f, .65f);
        [Tooltip("쓰러짐 소리가 시작되면 히트·YouDied를 이 볼륨 비율로 낮춘다(폴리가 들리게).")]
        [Range(0, 1)] public float deathHitDuck = .45f;
        [Tooltip("히트·YouDied가 0이 되는 시각(클라이맥스 기준 초).")]
        public float deathHitEnd = 2.6f;
        [Range(0, 1)] public float deathTinnitusVolume = .5f;
        [Tooltip("클라이맥스 뒤 이명만 남는 구간이 시작되는 시각(초). 이 시각부터 deathTinnitusFade 동안 줄어든다.")]
        public float deathTinnitusAloneAt = 3.2f;
        public float deathTinnitusFade = 2f;
        [Tooltip("이명이 끝난 뒤 거의 무음 구간(초).")]
        public float deathSilence = 2.5f;
        [Tooltip("먼 곳에서 울리는 전화벨(1~3번과 같은 phoneRing).")]
        public int deathRingCount = 2;
        [Range(0, 1)] public float deathRingVolume = .3f;
        public float deathRingLowPass = 1000f;
        [Tooltip("마지막 벨이 울리기 시작한 뒤 소리를 끊는 시각(초).")]
        public float deathRingCutAfter = 1.2f;
        [Tooltip("벨이 끊긴 뒤 무음(초) → onEndingFinished.")]
        public float deathEndSilence = 1f;

        [Header("Timeline 20~: 무시 엔딩 꼬리 (퇴근) — 「고생하셨습니다.」 뒤는 TTS 없이 글자와 소리만")]
        [Tooltip("켜면 「고생하셨습니다.」 뒤를 통화 끊김·태블릿·사망 사운드 대신 퇴근 꼬리(철거 문장 → 퇴근 확인 1명→2명 → 바깥 소리·발소리)로 재생합니다.")]
        public bool useDepartureTail = true;
        [Tooltip("「고생하셨습니다.」(마지막 TTS)가 끝난 뒤 철거 문장이 뜨기까지의 무음(초).")]
        public float departureLeadSilence = 1.8f;
        [TextArea(1, 2)] public string departureText = "철거는 예정대로 진행되었습니다.";
        public float departureTextDuration = 2.8f;
        [Tooltip("철거 문장이 사라진 뒤 퇴근 확인이 뜨기까지(초).")]
        public float departureGap = 0.6f;
        public string headcountPrefix = "퇴근 확인: ";
        public string headcountBefore = "1명";
        [Tooltip("1명이 흔들리고 깨지다가 이 글자로 바뀝니다(「저희는 규칙을… 보낸 적이 없…」의 깨짐 방식).")]
        public string headcountAfter = "2명";
        public float headcountBeforeHold = 1.5f;
        [Tooltip("1명 → 2명으로 바뀌는 깨짐 연출 길이(초).")]
        public float headcountGlitchDuration = 1.0f;
        [Tooltip("깨짐 동안 깔리는 글리치 소리 볼륨(tabletGlitchClip 사용). 0이면 소리 없음.")]
        [Range(0, 1)] public float headcountGlitchVolume = 0.45f;
        public float headcountAfterHold = 1.8f;
        [Tooltip("검은 화면에서 들리는 바깥 소리(바람 등).")]
        public AudioClip outsideClip;
        [Range(0, 1)] public float outsideVolume = 0.5f;
        [Tooltip("바깥 소리가 먼저 들리고 첫 발소리가 나기까지(초).")]
        public float outsideLead = 1.2f;
        public AudioClip footstepClip;
        [Header("발소리 두 사람 (같은 보폭으로 걷는 둘: 한 사람이 먼저, 다른 사람이 한 박 늦게)")]
        [Tooltip("첫 번째 사람의 발소리 시각(초).")]
        public float[] stepTimes = new float[] { 0f, 0.55f, 1.1f, 1.65f };
        [Range(0, 1)] public float stepVolume = 0.8f;
        public float stepPitch = 1f;
        [Range(-1, 1)] public float stepPan = -0.35f;
        [Tooltip("두 번째 사람(따라오는 쪽)의 발소리 시각(초). 첫 사람보다 한 박 늦게, 같은 간격으로.")]
        public float[] followTimes = new float[] { 0.27f, 0.82f, 1.37f, 1.92f };
        [Range(0, 1)] public float followVolume = 0.75f;
        [Tooltip("따라오는 사람은 발소리가 더 낮고 무겁게.")]
        public float followPitch = 0.78f;
        [Range(-1, 1)] public float followPan = 0.4f;
        [Tooltip("마지막 발소리의 볼륨 배율. 첫 발소리는 1(그대로)에서 시작해 마지막으로 갈수록 서서히 이 값까지 작아집니다(멀어지는 느낌). 1이면 줄어들지 않음.")]
        [Range(0f, 1f)] public float stepFadeEnd = 0.08f;
        [Tooltip("마지막 발소리 뒤 모든 소리를 끊기까지(초).")]
        public float afterLastStepCut = 0.35f;
        [Tooltip("소리가 끊긴 뒤 완전한 암전(초) → onEndingFinished.")]
        public float departureEndBlack = 1.5f;

        [Header("End (27: 크레딧)")]
        public bool playOnStart = true;
        [Tooltip("엔딩이 끝나면 호출됩니다. 크레딧 씬 로드 등을 여기에 연결하세요.")]
        public UnityEvent onEndingFinished = new UnityEvent();

        [Header("Debug")]
        [Tooltip("0이면 매번 다른 무작위 배치.")]
        public int randomSeed = 0;
        [Tooltip("에디터 확인용: 이 Stage에 들어가고 debugBreakDelay초 뒤 에디터를 일시정지합니다. 비우면 동작 안 함.")]
        public string debugBreakAtStage = "";
        public float debugBreakDelay = 0f;

        public string Stage { get; private set; } = "Idle";
        public bool Finished { get; private set; }
        public int SpawnedTextCount => spawned.Count;
        public float CutTimeFromStart { get; private set; }
        // 검증용 기록: 끊김 직전 프레임 / 끊김 다음 프레임
        public int PreCutActiveVoices { get; private set; } = -1;
        public int PreCutActiveSources { get; private set; } = -1;
        public int PreCutTexts { get; private set; } = -1;
        public int PostCutActiveSources { get; private set; } = -1;
        public int PostCutTexts { get; private set; } = -1;
        public int FloodMinActiveVoices { get; private set; } = -1;
        public int FloodVoiceTriggers { get; private set; }
        public bool TextCountEverDecreased { get; private set; }
        public float PreCutRiserVolume { get; private set; } = -1f;

        private float clock, startTime, stageStart;
        // 시퀀스 시계: 프레임당 최대 maxFrameStep초만 진행(에디터 일시정지·끊김 뒤에 순서가 건너뛰지 않게).
        private float seqTime;
        [Tooltip("한 프레임에 시퀀스 시계가 진행할 수 있는 최대 시간(초).")]
        public float maxFrameStep = 0.25f;
        private bool debugBroken;
        private System.Random rng;
        private readonly List<RectTransform> spawned = new List<RectTransform>();
        private readonly List<(RectTransform rect, float time)> popping = new List<(RectTransform, float)>();
        private readonly List<Coroutine> spawners = new List<Coroutine>();
        private int crowdIndex;
        private float typeStart; private bool typing;

        // build-up audio state
        private bool buildUpActive;
        private float rampStart, rampEnd;
        private float noiseFrom, noiseTo, staticFrom, staticTo, hbVolFrom, hbVolTo, tensionFrom, tensionTo, hbIntFrom, hbIntTo;
        private float nextBeat = -1f;
        private bool tensionStarted, riserStarted;
        private float tensionStartTime, riserStartTime, cutTime;
        private float hitFadeStartTime = -1f, hitBaseVolume;
        private float callFadeStart = -1f, callFadeTarget;
        private UncannyVoiceFX dispatcherFX, closeFX;
        private AudioDistortionFilter dispatcherDistortion;
        private float fxBasePitch = 1f, fxBaseDistortion, fxGlitchMin, fxGlitchMax, fxStutterMs;
        private bool fxGlitches;
        private Vector2 centerBasePos, finalBasePos;
        private bool cursorActive; private float cursorStart;
        private float tabletGlitchStopAt = -1f, flashStart = -1f, shakeEnd = -1f, tailStart = -1f;
        private bool blackoutStarted;
        // 사망 사운드 런타임 소스 (Awake에서 생성, 씬에는 저장되지 않음)
        private AudioSource dNotify, dHeart, dBreath, dBody, dFlash, dRing, dWind, dStep, dFollow;
        private bool deathBuildActive; private float deathBuildStart;
        private float deathClimaxAt = -1f, deathFlashStart = -1f, deathTinnitusStart = -1f;
        private bool deathBodyStarted, deathFlashStarted;
        private int notifyCheckFrames = -1;
        public bool NotificationWasPlaying { get; private set; }
        public float NotificationVolumeAtPlay { get; private set; } = -1f;
        public string NotificationClipName { get; private set; } = "";
        public string DeathLog { get; private set; } = "";
        public int BreakGlitchCount { get; private set; }
        public float BreakCutTimeInLine { get; private set; } = -1f;

        private void Awake()
        {
            Cursor.visible = false;
            rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            // 자막 = Noto Serif KR 자막 폰트, 수칙/태블릿 = 수칙지 폰트 (서로 덮어쓰지 않음)
            if (subtitleFont != null && centerText != null) centerText.font = subtitleFont;
            if (font != null && finalText != null) finalText.font = font;
            if (centerText != null) { centerText.text = ""; centerText.fontSize = centerFontSize; centerText.color = textColor; centerBasePos = centerText.rectTransform.anchoredPosition; }
            if (finalText != null) { finalText.text = ""; finalText.fontSize = finalFontSize; finalText.color = textColor; finalBasePos = finalText.rectTransform.anchoredPosition; }
            if (flashOverlay != null) { var c = flashOverlay.color; c.a = 0f; flashOverlay.color = c; flashOverlay.raycastTarget = false; }
            if (dispatcherVoice != null) { dispatcherFX = dispatcherVoice.GetComponent<UncannyVoiceFX>(); dispatcherDistortion = dispatcherVoice.GetComponent<AudioDistortionFilter>(); }
            if (closeVoice != null) closeFX = closeVoice.GetComponent<UncannyVoiceFX>();
            if (dispatcherFX != null)
            {
                fxBasePitch = dispatcherFX.basePitch; fxBaseDistortion = dispatcherFX.baseDistortion; fxGlitches = dispatcherFX.glitchesEnabled;
                fxGlitchMin = dispatcherFX.glitchDurationMin; fxGlitchMax = dispatcherFX.glitchDurationMax; fxStutterMs = dispatcherFX.stutterSliceMs;
            }
            foreach (var s in AllSources()) if (s != null) { s.playOnAwake = false; s.Stop(); }
            SetupDeathAudio();
        }

        private AudioClip DeathClip(AudioClip assigned, string name)
        {
            var c = assigned != null ? assigned : Resources.Load<AudioClip>("SuccessEndingDeath/" + name);
            if (c != null && c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            return c;
        }

        private AudioSource DeathSource(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = false; src.spatialBlend = 0f; src.volume = 1f; src.pitch = 1f; src.panStereo = 0f;
            src.priority = 0; src.dopplerLevel = 0f; src.bypassReverbZones = true;
            return src;
        }

        private void SetupDeathAudio()
        {
            deathNotificationClip = DeathClip(deathNotificationClip, "SFX_DEATH_TabletVibrate_Audible");
            deathHeartbeatClip = DeathClip(deathHeartbeatClip, "SFX_DEATH_HeartbeatAccel");
            deathBreathClip = DeathClip(deathBreathClip, "SFX_BODY_RoughBreath_02");
            deathBodyFallClip = DeathClip(deathBodyFallClip, "CAP-COM-14_3");
            deathFlashlightClip = DeathClip(deathFlashlightClip, "SFX_DEATH_FlashlightDropRoll");
            if (phoneRing != null && phoneRing.loadState != AudioDataLoadState.Loaded) phoneRing.LoadAudioData();
            if (notification != null && notification.loadState != AudioDataLoadState.Loaded) notification.LoadAudioData();
            var root = new GameObject("DeathAudio (runtime)").transform;
            root.SetParent(transform, false);
            dNotify = DeathSource(root, "D_Notification");
            dHeart = DeathSource(root, "D_Heartbeat");
            dBreath = DeathSource(root, "D_Breath");
            dBody = DeathSource(root, "D_BodyFall");
            dFlash = DeathSource(root, "D_FlashlightRoll");
            dRing = DeathSource(root, "D_DistantRing");
            dWind = DeathSource(root, "D_OutsideWind");
            dStep = DeathSource(root, "D_Footstep");
            dFollow = DeathSource(root, "D_FootstepFollow");
            var lp = dRing.gameObject.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = deathRingLowPass; lp.lowpassResonanceQ = 1f;
            var rv = dRing.gameObject.AddComponent<AudioReverbFilter>();
            rv.reverbPreset = AudioReverbPreset.Hallway;
        }

        private void DLog(string msg)
        {
            string line = "[" + (seqTime - startTime).ToString("0.00") + "s] " + msg;
            DeathLog += line + "\n";
            Debug.Log("[SuccessEnding] " + line, this);
        }

        private IEnumerable<AudioSource> DeathSources()
        {
            yield return dNotify; yield return dHeart; yield return dBreath; yield return dBody; yield return dFlash; yield return dRing;
            yield return dWind; yield return dStep; yield return dFollow;
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        private IEnumerable<AudioSource> AllSources()
        {
            yield return effects; yield return callLine; yield return lineStatic; yield return heartbeat;
            yield return tension; yield return riser; yield return hit; yield return dispatcherVoice;
            yield return closeVoice; yield return cleanVoice; yield return tabletGlitch; yield return tinnitus; yield return blackout;
            if (crowdVoices != null) foreach (var s in crowdVoices) yield return s;
            if (extraCutRisers != null) foreach (var r in extraCutRisers) if (r != null) yield return r.source;
        }

        private void SetStage(string stage)
        {
            Stage = stage;
            stageStart = seqTime;
            debugBroken = false;
        }

        private IEnumerator WaitCursor(float seconds)
        {
            clock += Mathf.Max(0, seconds);
            while (seqTime < clock) yield return null;
        }

        // 에디터에서 확인할 때 원하는 단계부터 바로 재생합니다(재생 중에도 가능). 게임 진행에는 쓰지 않습니다.
        public enum EndingJumpPoint { Start = 0, Call = 1, BuildUp = 2, After = 3, Clean = 4, Tail = 5 }
        private int jumpLevel;

        public void PlayFrom(EndingJumpPoint point)
        {
            StopAllCoroutines();
            ResetForJump();
            jumpLevel = (int)point;
            StartCoroutine(Run());
        }

        // 하드 컷과 같은 정리를 하되(소리 정지, 글자 지움) 컷 직후 처리(cutPending)는 일으키지 않는다.
        private void ResetForJump()
        {
            buildUpActive = false;
            foreach (var c in spawners) if (c != null) StopCoroutine(c);
            spawners.Clear();
            foreach (var s in AllSources()) if (s != null) s.Stop();
            foreach (var ds in DeathSources()) if (ds != null) ds.Stop();
            if (randomTextLayer != null)
                for (int i = randomTextLayer.childCount - 1; i >= 0; i--)
                {
                    var child = randomTextLayer.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            spawned.Clear(); popping.Clear();
            if (centerText != null) { centerText.text = ""; centerText.rectTransform.anchoredPosition = centerBasePos; }
            if (finalText != null) { finalText.text = ""; finalText.rectTransform.anchoredPosition = finalBasePos; finalText.rectTransform.localScale = Vector3.one; }
            if (flashOverlay != null) { var c = flashOverlay.color; c.a = 0f; flashOverlay.color = c; }
            typing = false; cursorActive = false; deathBuildActive = false;
            nextBeat = -1f; callFadeStart = -1f; tabletGlitchStopAt = -1f;
            tailStart = -1f; flashStart = -1f; shakeEnd = -1f;
            StopCallAudio();
        }

        private IEnumerator Run()
        {
            int jump = jumpLevel; jumpLevel = 0;
            Finished = false;
            startTime = clock = seqTime;
            deathBuildActive = false; deathClimaxAt = -1f; deathFlashStart = -1f; deathTinnitusStart = -1f; notifyCheckFrames = -1;
            NotificationWasPlaying = false; DeathLog = "";
            foreach (var ds in DeathSources()) if (ds != null) ds.Stop();

            if (jump == 0)
            {
                // 1. 완전 무음
                SetStage("1_Silence");
                yield return WaitCursor(openingSilence);

                // 2. 전화벨
                SetStage("2_Ringing");
                float ringLen = phoneRing != null ? phoneRing.length : 0f;
                float interval = Mathf.Max(ringBlock / Mathf.Max(1, ringCount), ringLen);
                for (int i = 0; i < ringCount; i++)
                {
                    if (phoneRing != null) effects.PlayOneShot(phoneRing);
                    bool last = i == ringCount - 1;
                    yield return WaitCursor(last ? Mathf.Max(ringBlock - interval * (ringCount - 1), ringLen) : interval);
                }

                // 3. 연결음 → 회선 소리
                SetStage("3_Connect");
                if (callConnect != null) effects.PlayOneShot(callConnect);
                float connectLen = Mathf.Min(callConnect != null ? callConnect.length : 0f, connectBlock);
                yield return WaitCursor(connectLen);
                StartCallLine(callLineVolume, 0f);
                yield return WaitCursor(connectBlock - connectLen);
            }
            else if (jump <= 2)
            {
                // 에디터 이동: 연결음 이후 회선 소리만 켠 상태에서 시작
                StartCallLine(callLineVolume, 0f);
            }

            // 4~6. 통화
            if (jump <= 1)
                foreach (var line in callLines) { SetStage("Call_" + line.id); yield return PlayLine(line); }

            if (jump <= 2)
            {
                // 7~14. 몰아치기
                BeginBuildUp();
                for (int i = 0; i < buildUpLines.Count; i++)
                {
                    var line = buildUpLines[i];
                    SetStage("BuildUp_" + line.id + (line.flood ? "_Flood" : ""));
                    yield return PlayLine(line, i);
                }
                if (floodDuration > 0f)
                {
                    SetStage("BuildUp_Flood");
                    yield return SustainFlood();
                }

                // 15. 하드 컷: 같은 프레임에 글자와 소리를 전부 끊는다
                HardCut();
                SetStage("15_Cut");
                yield return WaitCursor(cutSilence);
            }

            if (jump <= 3)
            {
                // 16~17. 회선 소리가 작게 돌아온다 (17은 '보낸'부터 깨지고 '없…'에서 전부 끊김)
                StartCallLine(returnCallLineVolume, returnCallLineFadeIn);
                foreach (var line in afterLines) { SetStage("After_" + line.id); yield return PlayLine(line); }
            }

            if (jump <= 4)
            {
                // 18. 회선 소리까지 완전 무음
                SetStage("18_Silence");
                StopCallAudio();
                yield return WaitCursor(preCleanSilence);

                // 19. '고생하셨습니다.' 같은 목소리지만 회선 잡음·필터 없이 너무 깨끗하게
                foreach (var line in cleanLines) { SetStage("Clean_" + line.id); yield return PlayLine(line); }
            }

            // 20~. 무시 엔딩 꼬리: TTS는 「고생하셨습니다.」까지, 이후는 글자와 소리만
            if (useDepartureTail)
            {
                yield return DepartureTail();
                SetStage("27_Credits");
                Finished = true;
                Debug.Log("[SuccessEnding] 무시 엔딩 종료 — onEndingFinished를 호출합니다.", this);
                onEndingFinished?.Invoke();
                yield break;
            }

            // 20. 통화 끊김 (Call_connect_1 재사용)
            SetStage("20_HangUp");
            StopCallAudio();
            var hang = hangUp != null ? hangUp : callConnect;
            if (hang != null) effects.PlayOneShot(hang);
            yield return WaitCursor(hangUpBlock);

            // 21. 무음
            SetStage("21_Silence");
            yield return WaitCursor(postHangUpSilence);

            // 22. 태블릿 진동 알림 1회 (전용 2D 소스, 볼륨 1, 저역 진동에 배음을 더한 사본)
            SetStage("22_Notification");
            if (useDeathSoundDesign && dNotify != null && (deathNotificationClip != null || notification != null))
            {
                var nc = deathNotificationClip != null ? deathNotificationClip : notification;
                dNotify.clip = nc; dNotify.volume = deathNotificationVolume; dNotify.pitch = 1f; dNotify.time = 0f;
                dNotify.Play();
                NotificationClipName = nc.name; NotificationVolumeAtPlay = dNotify.volume; notifyCheckFrames = 2;
                DLog("22 진동 알림 재생: " + nc.name + " vol " + dNotify.volume.ToString("0.00"));
            }
            else if (notification != null) effects.PlayOneShot(notification);
            yield return WaitCursor(notificationBlock);

            // 23. '인수인계_' 커서 깜빡임 (무음)
            SetStage("23_Cursor");
            if (finalText != null) { finalText.fontSize = finalFontSize; finalText.color = textColor; finalText.maxVisibleCharacters = 99999; }
            cursorStart = seqTime; cursorActive = true;
            UpdateCursor(seqTime);
            yield return WaitCursor(cursorDuration);

            if (useDeathSoundDesign)
            {
                yield return DeathEnding();
            }
            else
            {
                // 24. '인수인계가 진행됩니다.' 한 번에 채워짐 + 글리치 소리 앞부분
                SetStage("24_TabletText");
                cursorActive = false;
                if (finalText != null) finalText.text = tabletText;
                if (tabletGlitch != null && tabletGlitchClip != null)
                {
                    tabletGlitch.clip = tabletGlitchClip; tabletGlitch.loop = false; tabletGlitch.volume = tabletGlitchVolume; tabletGlitch.time = 0f;
                    tabletGlitch.Play();
                    tabletGlitchStopAt = seqTime + Mathf.Max(0.05f, tabletGlitchLength);
                }
                yield return WaitCursor(tabletTextDuration);

                // 25. 글자 번쩍 → 세게 흔들림 → 사라짐 + 클라이맥스 히트 + YouDied (첫 타격 = 번쩍임 프레임)
                SetStage("25_Climax");
                if (tabletGlitch != null) tabletGlitch.Stop();
                tabletGlitchStopAt = -1f;
                if (finalText != null) { finalText.color = flashTextColor; finalText.rectTransform.localScale = Vector3.one * 1.08f; }
                if (flashOverlay != null) { var c = flashOverlay.color; c.a = flashOverlayAlpha; flashOverlay.color = c; }
                flashStart = seqTime; shakeEnd = seqTime + shakeDuration;
                PlayHit();
                if (hit != null && youDiedClip != null) hit.PlayOneShot(youDiedClip, youDiedVolume);
                yield return WaitCursor(climaxBlock);

                // 26. 이명 꼬리 → 아주 작게 블랙아웃 꼬리 → 무음
                SetStage("26_Tail");
                if (finalText != null) { finalText.text = ""; finalText.rectTransform.anchoredPosition = finalBasePos; finalText.rectTransform.localScale = Vector3.one; }
                hitFadeStartTime = seqTime;
                tailStart = seqTime;
                if (tinnitus != null && tinnitusClip != null) { tinnitus.clip = tinnitusClip; tinnitus.loop = false; tinnitus.volume = tinnitusVolume; tinnitus.Play(); }
                blackoutStarted = false;
                yield return WaitCursor(tailDuration);
                tailStart = -1f;
                foreach (var s in AllSources()) if (s != null) s.Stop();
                hitStartedAt = -1f; hitFadeStartTime = -1f;
            }

            // 27. 끝 → 크레딧 훅
            SetStage("27_Credits");
            Finished = true;
            Debug.Log("[SuccessEnding] 성공 엔딩 종료 — onEndingFinished(크레딧)을 호출합니다.", this);
            onEndingFinished?.Invoke();
        }

        // 무시 엔딩 꼬리: 철거 문장 → 퇴근 확인 1명 → 2명(소리 없이 정정) → 검은 화면 + 바깥 소리 + 발소리(한 쌍, 한 박 늦게 하나) → 끊김
        private IEnumerator DepartureTail()
        {
            StopCallAudio();

            SetStage("19b_Gap");
            yield return WaitCursor(departureLeadSilence);

            SetStage("20_Departure");
            ShowCentered(departureText);
            yield return WaitCursor(departureTextDuration);
            if (centerText != null) centerText.text = "";
            typing = false;
            yield return WaitCursor(departureGap);

            SetStage("21_Headcount");
            ShowCentered(headcountPrefix + headcountBefore);
            yield return WaitCursor(headcountBeforeHold);
            SetStage("21b_HeadcountGlitch");
            yield return HeadcountGlitch();
            yield return WaitCursor(headcountAfterHold);
            if (centerText != null) centerText.text = "";
            typing = false;

            SetStage("22_OutsideSteps");
            if (dWind != null && outsideClip != null)
            { dWind.clip = outsideClip; dWind.volume = outsideVolume; dWind.loop = false; dWind.time = 0f; dWind.Play(); }
            yield return WaitCursor(outsideLead);

            // 두 사람의 발소리: 먼저 걷는 쪽(왼쪽, 보통 높이)과 한 박 늦게 따라오는 쪽(오른쪽, 낮고 무겁게)을 시각순으로 섞어 낸다
            var steps = new List<(float time, bool follow)>();
            if (stepTimes != null) foreach (var t in stepTimes) steps.Add((t, false));
            if (followTimes != null) foreach (var t in followTimes) steps.Add((t, true));
            steps.Sort((a, b) => a.time.CompareTo(b.time));
            float span = steps.Count > 0 ? steps[steps.Count - 1].time : 0f;
            float last = 0f;
            foreach (var st in steps)
            {
                yield return WaitCursor(Mathf.Max(0f, st.time - last));
                last = st.time;
                var src = st.follow ? dFollow : dStep;
                if (src != null && footstepClip != null)
                {
                    // 끝으로 갈수록 서서히 작아진다(멀어지는 느낌): 첫 발소리 1배 → 마지막 발소리 stepFadeEnd배
                    float fade = span > 0f ? Mathf.Lerp(1f, stepFadeEnd, st.time / span) : 1f;
                    src.pitch = st.follow ? followPitch : stepPitch;
                    src.panStereo = st.follow ? followPan : stepPan;
                    src.PlayOneShot(footstepClip, (st.follow ? followVolume : stepVolume) * fade);
                }
            }
            yield return WaitCursor(afterLastStepCut);

            SetStage("23_DepartureCut");
            foreach (var s in DeathSources()) if (s != null) s.Stop();
            yield return WaitCursor(departureEndBlack);
        }

        // 퇴근 확인 1명 → 2명: 「저희는 규칙을… 보낸 적이 없…」처럼 자막이 흔들리고 글자가 깨지다가 바뀐다.
        // 앞 절반은 1명이, 뒤 절반은 2명이 깨지며, 마지막에 2명으로 정착한다.
        private IEnumerator HeadcountGlitch()
        {
            float dur = Mathf.Max(0.1f, headcountGlitchDuration);
            float start = seqTime, end = clock + dur;
            clock = end;
            if (headcountGlitchVolume > 0f && tabletGlitch != null && tabletGlitchClip != null)
            {
                tabletGlitch.clip = tabletGlitchClip; tabletGlitch.loop = false; tabletGlitch.volume = headcountGlitchVolume; tabletGlitch.time = 0f;
                tabletGlitch.Play();
                tabletGlitchStopAt = seqTime + Mathf.Min(dur, Mathf.Max(0.05f, tabletGlitchLength));
            }
            var sb = new System.Text.StringBuilder();
            float nextCorrupt = 0f;
            while (seqTime < end)
            {
                float q = Mathf.Clamp01((seqTime - start) / dur);
                if (centerText != null)
                {
                    float amp = Mathf.Lerp(breakShake.x, breakShake.y, q);
                    centerText.rectTransform.anchoredPosition = centerBasePos + new Vector2(Range(-amp, amp), Range(-amp, amp) * .6f);
                    if (seqTime >= nextCorrupt)
                    {
                        nextCorrupt = seqTime + 0.06f;
                        string body = q < 0.5f ? headcountBefore : headcountAfter;
                        float chance = q < 0.5f ? Mathf.Lerp(0.15f, 0.7f, q * 2f) : Mathf.Lerp(0.7f, 0.1f, (q - 0.5f) * 2f);
                        sb.Length = 0;
                        sb.Append(headcountPrefix);
                        foreach (char ch0 in body)
                        {
                            char ch = ch0;
                            if (!char.IsWhiteSpace(ch) && !string.IsNullOrEmpty(breakGlyphs) && rng.NextDouble() < chance)
                                ch = breakGlyphs[rng.Next(breakGlyphs.Length)];
                            sb.Append(ch);
                        }
                        centerText.maxVisibleCharacters = 99999;
                        typing = false;
                        centerText.text = sb.ToString();
                    }
                }
                yield return null;
            }
            if (tabletGlitch != null) { tabletGlitch.Stop(); tabletGlitchStopAt = -1f; }
            if (centerText != null)
            {
                centerText.rectTransform.anchoredPosition = centerBasePos;
                centerText.text = headcountPrefix + headcountAfter;
                centerText.maxVisibleCharacters = 99999;
            }
            typing = false;
        }

        // 24~26 사망 암시 사운드 (죽음은 소리로만, 화면은 검은 화면/글자만)
        private IEnumerator DeathEnding()
        {
            // 24. '인수인계가 진행됩니다.' + 심장박동·거친 숨이 2D로 가까이 커지며 빨라짐 + 밑에 글리치
            SetStage("24_TabletText");
            cursorActive = false;
            if (finalText != null) finalText.text = tabletText;
            if (tabletGlitch != null && tabletGlitchClip != null)
            {
                tabletGlitch.clip = tabletGlitchClip; tabletGlitch.loop = false; tabletGlitch.volume = deathGlitchVolume; tabletGlitch.time = 0f;
                tabletGlitch.Play();
            }
            tabletGlitchStopAt = -1f;
            if (dHeart != null && deathHeartbeatClip != null)
            { dHeart.clip = deathHeartbeatClip; dHeart.volume = deathHeartbeatVolume.x; dHeart.pitch = deathHeartbeatPitch.x; dHeart.time = 0f; dHeart.Play(); }
            if (dBreath != null && deathBreathClip != null)
            {
                dBreath.clip = deathBreathClip; dBreath.volume = deathBreathVolume.x; dBreath.pitch = deathBreathPitch.x;
                dBreath.time = Mathf.Clamp(deathBreathStart, 0f, deathBreathClip.length - 0.01f); dBreath.Play();
            }
            deathBuildStart = seqTime; deathBuildActive = true;
            DLog("24 심장+호흡 시작 (heart " + (deathHeartbeatClip != null ? deathHeartbeatClip.name : "없음") + ", breath " + (deathBreathClip != null ? deathBreathClip.name : "없음") + ")");
            yield return WaitCursor(deathBuildUpDuration);

            // 25. 번쩍임·흔들림 프레임 = v3 히트 + YouDied, 숨은 들이쉬던 중에 끊기고 심장은 뚝 멈춤
            SetStage("25_Climax");
            deathBuildActive = false;
            float breathPos = dBreath != null ? dBreath.time : -1f;
            if (dBreath != null) dBreath.Stop();
            if (dHeart != null) dHeart.Stop();
            if (tabletGlitch != null) tabletGlitch.Stop();
            if (finalText != null) { finalText.color = flashTextColor; finalText.rectTransform.localScale = Vector3.one * 1.08f; }
            if (flashOverlay != null) { var c = flashOverlay.color; c.a = flashOverlayAlpha; flashOverlay.color = c; }
            flashStart = seqTime; shakeEnd = seqTime + shakeDuration;
            PlayHit();
            if (hit != null && youDiedClip != null) hit.PlayOneShot(youDiedClip, youDiedVolume);
            if (tinnitus != null && tinnitusClip != null) { tinnitus.clip = tinnitusClip; tinnitus.loop = false; tinnitus.volume = deathTinnitusVolume; tinnitus.Play(); }
            deathClimaxAt = seqTime; deathBodyStarted = false; deathFlashStarted = false; deathTinnitusStart = -1f;
            DLog("25 클라이맥스: 히트+YouDied, 호흡 컷(클립 " + breathPos.ToString("0.00") + "s 지점), 심장 정지");
            yield return WaitCursor(deathFoleyDelay);

            // 25-1. 검은 화면, 0.3초 뒤: 몸이 쓰러지는 쿵 + 태블릿이 바닥에 떨어져 덜그럭 → 손전등이 떨어져 굴러가다 멈춤
            SetStage("25_Collapse");
            if (dBody != null && deathBodyFallClip != null)
            { dBody.clip = deathBodyFallClip; dBody.volume = deathBodyFallVolume; dBody.panStereo = 0f; dBody.time = 0f; dBody.Play(); deathBodyStarted = true; }
            DLog("25-1 쓰러짐+태블릿 낙하 (" + (deathBodyFallClip != null ? deathBodyFallClip.name : "없음") + ")");
            yield return WaitCursor(deathFlashlightDelay);
            if (dFlash != null && deathFlashlightClip != null)
            { dFlash.clip = deathFlashlightClip; dFlash.volume = deathFlashlightVolume; dFlash.panStereo = deathRollPan.x; dFlash.time = 0f; dFlash.Play(); deathFlashStarted = true; deathFlashStart = seqTime; }
            DLog("25-2 손전등 낙하·구르기 (" + (deathFlashlightClip != null ? deathFlashlightClip.name : "없음") + ")");
            float waitAlone = deathTinnitusAloneAt - (seqTime - deathClimaxAt);
            yield return WaitCursor(Mathf.Max(0f, waitAlone));

            // 26. 이명만 남았다가 사라짐
            SetStage("26_Tinnitus");
            if (finalText != null) { finalText.text = ""; finalText.rectTransform.anchoredPosition = finalBasePos; finalText.rectTransform.localScale = Vector3.one; }
            deathTinnitusStart = seqTime;
            DLog("26 이명만 남음 → " + deathTinnitusFade.ToString("0.0") + "초 동안 사라짐");
            yield return WaitCursor(deathTinnitusFade);
            if (tinnitus != null) tinnitus.Stop();
            deathTinnitusStart = -1f;

            // 26-1. 거의 무음
            SetStage("26_Silence");
            foreach (var s in AllSources()) if (s != null) s.Stop();
            hitStartedAt = -1f; hitFadeStartTime = -1f; deathClimaxAt = -1f;
            if (dBody != null) dBody.Stop();
            if (dFlash != null) dFlash.Stop();
            deathFlashStart = -1f;
            DLog("26-1 무음");
            yield return WaitCursor(deathSilence);

            // 26-2. 멀리서 먹먹하게 같은 전화벨 2번 (다음 근무자를 부르는 소리) → 뚝 끊김
            SetStage("26_DistantRing");
            float ringLen = phoneRing != null ? phoneRing.length : 0f;
            float interval = Mathf.Max(ringBlock / Mathf.Max(1, ringCount), ringLen);
            int rings = Mathf.Max(1, deathRingCount);
            for (int i = 0; i < rings; i++)
            {
                if (phoneRing != null && dRing != null) { dRing.Stop(); dRing.clip = phoneRing; dRing.volume = deathRingVolume; dRing.time = 0f; dRing.Play(); }
                DLog("26-2 먼 전화벨 " + (i + 1) + "회 (" + (phoneRing != null ? phoneRing.name : "없음") + ", LPF " + deathRingLowPass.ToString("0") + "Hz, vol " + deathRingVolume.ToString("0.00") + ")");
                yield return WaitCursor(i == rings - 1 ? Mathf.Min(deathRingCutAfter, interval) : interval);
            }
            if (dRing != null) dRing.Stop();
            DLog("26-3 벨 끊김 → 무음");
            yield return WaitCursor(deathEndSilence);
            foreach (var s in DeathSources()) if (s != null) s.Stop();
        }

        private void UpdateDeathAudio(float now)
        {
            if (notifyCheckFrames >= 0)
            {
                if (dNotify != null && dNotify.isPlaying) NotificationWasPlaying = true;
                if (notifyCheckFrames-- == 0)
                    DLog("22 진동 확인: isPlaying=" + (dNotify != null && dNotify.isPlaying) + " vol=" + (dNotify != null ? dNotify.volume.ToString("0.00") : "-") +
                         " clip=" + NotificationClipName + " loadState=" + (dNotify != null && dNotify.clip != null ? dNotify.clip.loadState.ToString() : "-"));
            }
            if (deathBuildActive)
            {
                float k = deathBuildUpDuration > 0f ? Mathf.Clamp01((now - deathBuildStart) / deathBuildUpDuration) : 1f;
                float e = k * k * (3f - 2f * k);
                if (dHeart != null) { dHeart.volume = Mathf.Lerp(deathHeartbeatVolume.x, deathHeartbeatVolume.y, e); dHeart.pitch = Mathf.Lerp(deathHeartbeatPitch.x, deathHeartbeatPitch.y, k); }
                if (dBreath != null) { dBreath.volume = Mathf.Lerp(deathBreathVolume.x, deathBreathVolume.y, e); dBreath.pitch = Mathf.Lerp(deathBreathPitch.x, deathBreathPitch.y, k); }
            }
            if (deathClimaxAt >= 0f)
            {
                float t = now - deathClimaxAt;
                if (hit != null && hitStartedAt >= 0f)
                {
                    float duckStart = deathFoleyDelay, duckEnd = deathFoleyDelay + .25f;
                    float v = t < duckStart ? 1f
                            : t < duckEnd ? Mathf.Lerp(1f, deathHitDuck, (t - duckStart) / .25f)
                            : Mathf.Lerp(deathHitDuck, 0f, Mathf.Clamp01((t - duckEnd) / Mathf.Max(.05f, deathHitEnd - duckEnd)));
                    hit.volume = hitBaseVolume * v;
                    if (t >= deathHitEnd) { hit.Stop(); hitStartedAt = -1f; }
                }
            }
            if (deathFlashStart >= 0f && dFlash != null && dFlash.isPlaying)
            {
                float ft = now - deathFlashStart;
                float rp = deathRollTime.y > deathRollTime.x ? Mathf.Clamp01((ft - deathRollTime.x) / (deathRollTime.y - deathRollTime.x)) : 0f;
                dFlash.panStereo = Mathf.Lerp(deathRollPan.x, deathRollPan.y, 1f - (1f - rp) * (1f - rp));
            }
            if (deathTinnitusStart >= 0f && tinnitus != null && tinnitus.isPlaying)
            {
                float p = deathTinnitusFade > 0f ? Mathf.Clamp01((now - deathTinnitusStart) / deathTinnitusFade) : 1f;
                tinnitus.volume = deathTinnitusVolume * (1f - p) * (1f - p);
            }
        }

        private void StopCallAudio()
        {
            callFadeStart = -1f;
            if (callLine != null) callLine.Stop();
            if (lineStatic != null) lineStatic.Stop();
            if (dispatcherVoice != null) dispatcherVoice.Stop();
            if (closeVoice != null) closeVoice.Stop();
        }

        private void UpdateCursor(float now)
        {
            if (finalText == null) return;
            bool on = cursorBlinkInterval <= 0f || Mathf.FloorToInt((now - cursorStart) / cursorBlinkInterval) % 2 == 0;
            // 커서가 꺼져도 글자 위치가 흔들리지 않게 투명한 커서를 남긴다
            finalText.text = tabletCursorText + (on ? cursorGlyph : "<alpha=#00>" + cursorGlyph);
        }

        // 담당자 소스는 UncannyVoiceFX가 pitch를 낮춰(0.95) 재생이 조금 느려지므로 그만큼 길게 잡는다.
        public float RoutePitch(EndingVoiceRoute route)
        {
            if (route == EndingVoiceRoute.Telephone && dispatcherFX != null) return Mathf.Max(0.1f, Mathf.Abs(dispatcherFX.EffectivePitch));
            if (route == EndingVoiceRoute.Telephone && dispatcherVoice != null) return Mathf.Max(0.1f, Mathf.Abs(dispatcherVoice.pitch));
            if (route == EndingVoiceRoute.CloseToEar && closeFX != null) return Mathf.Max(0.1f, Mathf.Abs(closeFX.EffectivePitch));
            return 1f;
        }

        public float VoiceEnd(EndingLine line)
        {
            float end = 0f;
            if (line.voices == null) return 0f;
            float pitch = RoutePitch(line.voiceRoute);
            for (int i = 0; i < line.voices.Length; i++)
                if (line.voices[i] != null) end = Mathf.Max(end, i * line.voiceStagger + line.voices[i].length / pitch);
            return end;
        }

        public float EffectiveDuration(EndingLine line)
        {
            float d = line.duration;
            if (line.extendToVoice) { float v = VoiceEnd(line); if (v > 0) d = Mathf.Max(d, v + 0.1f); }
            return d;
        }

        private IEnumerator PlayLine(EndingLine line, int buildUpIndex = -1)
        {
            float dur = EffectiveDuration(line);
            var voiceSrc = PlayVoices(line.voices, line.voiceStagger, line.voiceVolume, line.voiceRoute);
            if (line.alsoPlayEarlierCrowdVoices && buildUpIndex > 0)
                for (int i = 0; i < buildUpIndex; i++)
                    if (buildUpLines[i].mode == EndingLineMode.RandomStay)
                        PlayVoices(buildUpLines[i].voices, 0.03f, buildUpLines[i].voiceVolume * .8f, EndingVoiceRoute.Crowd);
            if (line.driveBuildUpAudio) SetBuildUpTargets(line, dur);

            if (line.mode == EndingLineMode.Centered && line.breakAtVoiceTime >= 0f && voiceSrc != null)
            {
                ShowCentered(line.text);
                yield return BreakLine(line, voiceSrc, dur);
            }
            else if (line.mode == EndingLineMode.Centered)
            {
                ShowCentered(line.text);
                float voiceEnd = VoiceEnd(line);
                float visible = Mathf.Clamp(Mathf.Max(dur - line.hideBeforeEnd, voiceEnd), 0f, dur);
                yield return WaitCursor(visible);
                if (centerText != null) centerText.text = "";
                typing = false;
                yield return WaitCursor(dur - visible);
            }
            else
            {
                spawners.Add(StartCoroutine(SpawnCopies(line.text, line.copies, line.spawnSpread, line.preferEdges, randomFontSize, 1f)));
                if (line.flood && floodTexts != null && floodTexts.Length > 0)
                    spawners.Add(StartCoroutine(SpawnFlood(Mathf.Max(0.05f, dur - 0.05f))));
                yield return WaitCursor(dur);
            }
        }

        private void ShowCentered(string text)
        {
            if (centerText == null) return;
            centerText.fontSize = centerFontSize;
            centerText.color = textColor;
            centerText.text = text;
            if (typeCharsPerSecond > 0f) { centerText.maxVisibleCharacters = 0; typeStart = seqTime; typing = true; }
            else { centerText.maxVisibleCharacters = 99999; typing = false; }
        }

        // 마지막으로 재생한 소스를 돌려준다(17번 깨짐 처리에서 씀).
        private AudioSource PlayVoices(AudioClip[] clips, float stagger, float volume, EndingVoiceRoute route)
        {
            AudioSource last = null;
            if (clips == null) return null;
            AudioSource single = null;
            if (clips.Length == 1)
            {
                if (route == EndingVoiceRoute.Telephone) single = dispatcherVoice;
                else if (route == EndingVoiceRoute.CloseToEar) single = closeVoice;
                else if (route == EndingVoiceRoute.Clean) single = cleanVoice;
            }
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                AudioSource src = single;
                if (src == null)
                {
                    if (crowdVoices == null || crowdVoices.Length == 0) { if (dispatcherVoice != null) dispatcherVoice.PlayOneShot(clip, volume); continue; }
                    src = crowdVoices[crowdIndex++ % crowdVoices.Length];
                }
                src.Stop();
                src.clip = clip;
                src.loop = false;
                src.volume = volume;
                src.pitch = src == single ? RoutePitch(route) : 1f;
                src.time = 0f;
                if (i * stagger > 0f) src.PlayDelayed(i * stagger); else src.Play();
                last = src;
            }
            return last;
        }

        // 17번: '보낸'부터 음성이 깨진다 — 지글거림↑, 음높이가 끌려 내려가고, 같은 음절 반복(스터터)·끊김,
        // 회선 잡음이 치솟다가 '없…'에서 전부 끊김. 자막도 흔들리고 일부 글자가 깨진다.
        private IEnumerator BreakLine(EndingLine line, AudioSource src, float dur)
        {
            float lineStart = seqTime, lineEnd = clock + dur;
            clock = lineEnd;
            float breakAt = line.breakAtVoiceTime;
            float cutAt = line.breakCutAtVoiceTime > breakAt ? line.breakCutAtVoiceTime : (src.clip != null ? src.clip.length : breakAt + 1f);
            float callFrom = callLine != null ? (callFadeStart >= 0f ? callFadeTarget : callLine.volume) : 0f;
            string full = line.text ?? "";
            int textFrom = Mathf.Clamp(line.breakTextIndex, 0, full.Length);
            bool broken = false, cut = false;
            float nextGlitch = 0f, nextCorrupt = 0f, spikeUntil = -1f;
            var sb = new System.Text.StringBuilder(full.Length + 8);
            while (seqTime < lineEnd)
            {
                float now = seqTime;
                float vt = src.isPlaying ? src.time : (broken ? cutAt : 0f);
                if (!cut && broken && (vt >= cutAt || !src.isPlaying)) cut = true;
                if (!cut && !broken && src.isPlaying && vt >= breakAt)
                {
                    broken = true;
                    BreakBegin();
                    if (lineStatic != null && staticLoop != null) { lineStatic.clip = staticLoop; lineStatic.loop = true; lineStatic.volume = 0f; lineStatic.Play(); }
                    callFadeStart = -1f;
                    nextGlitch = now;
                }
                if (cut)
                {
                    // '없…'에서 음성·회선 잡음·자막을 같은 프레임에 끊는다
                    StopCallAudio();
                    BreakEnd();
                    if (centerText != null) { centerText.text = ""; centerText.rectTransform.anchoredPosition = centerBasePos; }
                    typing = false;
                    BreakCutTimeInLine = now - lineStart;
                    while (seqTime < lineEnd) yield return null;
                    yield break;
                }
                if (broken)
                {
                    float q = Mathf.Clamp01((vt - breakAt) / Mathf.Max(0.05f, cutAt - breakAt));
                    if (dispatcherFX != null)
                    {
                        dispatcherFX.basePitch = Mathf.Lerp(fxBasePitch, breakPitchEnd, Mathf.Pow(q, 1.2f));
                        dispatcherFX.baseDistortion = Mathf.Lerp(fxBaseDistortion, breakDistortion, Mathf.Sqrt(q));
                    }
                    src.pitch = dispatcherFX != null ? dispatcherFX.EffectivePitch : Mathf.Lerp(1f, breakPitchEnd, q);
                    // 지글거림을 계속 올린다(DistortionSpike 글리치 중에는 그 값을 그대로 둠)
                    if (dispatcherDistortion != null && now >= spikeUntil)
                        dispatcherDistortion.distortionLevel = dispatcherFX != null ? dispatcherFX.EffectiveDistortion : Mathf.Lerp(fxBaseDistortion, breakDistortion, Mathf.Sqrt(q));
                    if (dispatcherFX != null && now >= nextGlitch)
                    {
                        double r = rng.NextDouble();
                        var type = r < .55 ? UncannyVoiceFX.GlitchType.Stutter : r < .8 ? UncannyVoiceFX.GlitchType.Dropout : UncannyVoiceFX.GlitchType.DistortionSpike;
                        dispatcherFX.TriggerGlitch(type);
                        if (type == UncannyVoiceFX.GlitchType.DistortionSpike) spikeUntil = now + breakGlitchDuration.y;
                        BreakGlitchCount++;
                        nextGlitch = now + Range(breakGlitchInterval.x, breakGlitchInterval.y) * Mathf.Lerp(1.2f, .7f, q);
                    }
                    if (callLine != null) callLine.volume = Mathf.Lerp(callFrom, breakLineNoise, q);
                    if (lineStatic != null) lineStatic.volume = Mathf.Lerp(0f, breakStaticNoise, q * q);
                    if (centerText != null)
                    {
                        float amp = Mathf.Lerp(breakShake.x, breakShake.y, q);
                        centerText.rectTransform.anchoredPosition = centerBasePos + new Vector2(Range(-amp, amp), Range(-amp, amp) * .6f);
                        if (now >= nextCorrupt)
                        {
                            nextCorrupt = now + 0.06f;
                            float chance = Mathf.Lerp(breakCorruptChance.x, breakCorruptChance.y, q);
                            sb.Length = 0;
                            sb.Append(full, 0, textFrom);
                            for (int i = textFrom; i < full.Length; i++)
                            {
                                char ch = full[i];
                                if (!char.IsWhiteSpace(ch) && ch != '…' && !string.IsNullOrEmpty(breakGlyphs) && rng.NextDouble() < chance)
                                    ch = breakGlyphs[rng.Next(breakGlyphs.Length)];
                                sb.Append(ch);
                            }
                            centerText.text = sb.ToString();
                        }
                    }
                }
                yield return null;
            }
            // 안전장치: 끊김 시각에 못 미쳐도 줄이 끝나면 끊는다
            StopCallAudio();
            BreakEnd();
            if (centerText != null) { centerText.text = ""; centerText.rectTransform.anchoredPosition = centerBasePos; }
            typing = false;
        }

        private void BreakBegin()
        {
            if (dispatcherFX == null) return;
            dispatcherFX.glitchesEnabled = false;
            dispatcherFX.glitchDurationMin = breakGlitchDuration.x;
            dispatcherFX.glitchDurationMax = Mathf.Max(breakGlitchDuration.x, breakGlitchDuration.y);
            dispatcherFX.stutterSliceMs = breakStutterSliceMs;
        }

        private void BreakEnd()
        {
            if (dispatcherFX == null) return;
            dispatcherFX.basePitch = fxBasePitch; dispatcherFX.baseDistortion = fxBaseDistortion; dispatcherFX.glitchesEnabled = fxGlitches;
            dispatcherFX.glitchDurationMin = fxGlitchMin; dispatcherFX.glitchDurationMax = fxGlitchMax; dispatcherFX.stutterSliceMs = fxStutterMs;
            if (dispatcherVoice != null) dispatcherVoice.pitch = dispatcherFX.EffectivePitch;
            if (dispatcherDistortion != null) dispatcherDistortion.distortionLevel = dispatcherFX.EffectiveDistortion;
        }

        private Vector2 RandomPosition(bool edges, Vector2 halfText)
        {
            var size = randomTextLayer.rect.size;
            float hw = Mathf.Max(10f, size.x * .5f - halfText.x), hh = Mathf.Max(10f, size.y * .5f - halfText.y);
            float x = Range(-hw, hw), y = Range(-hh, hh);
            if (edges)
            {
                // 가장자리 띠(바깥 35%)에서 튀어나오게 한다
                if (rng.NextDouble() < .5) x = Mathf.Sign(Range(-1f, 1f)) * Range(hw * .65f, hw);
                else y = Mathf.Sign(Range(-1f, 1f)) * Range(hh * .65f, hh);
            }
            return new Vector2(x, y);
        }

        private float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private RectTransform SpawnText(string text, Vector2 sizeRange, bool edges, float alpha)
        {
            var go = new GameObject("Rule_" + spawned.Count, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(randomTextLayer, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = Range(sizeRange.x, sizeRange.y);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            var c = textColor; c.a = alpha; tmp.color = c;
            tmp.text = text;
            Vector2 pref = tmp.GetPreferredValues(text);
            rect.sizeDelta = pref + new Vector2(8, 4);
            rect.anchoredPosition = RandomPosition(edges, pref * .5f);
            rect.localRotation = Quaternion.Euler(0, 0, Range(-randomRotation, randomRotation));
            rect.localScale = Vector3.one * popScale;
            spawned.Add(rect);
            popping.Add((rect, seqTime));
            return rect;
        }

        private IEnumerator SpawnCopies(string text, int copies, float spread, bool edges, Vector2 sizeRange, float alpha)
        {
            float t0 = seqTime;
            for (int i = 0; i < copies; i++)
            {
                float due = copies > 1 ? t0 + spread * i / (copies - 1) : t0;
                while (seqTime < due) yield return null;
                SpawnText(text, sizeRange, edges, alpha);
            }
        }

        private IEnumerator SpawnFlood(float seconds)
        {
            float t0 = seqTime;
            int done = 0;
            while (done < floodCount)
            {
                float p = Mathf.Clamp01((seqTime - t0) / seconds);
                // 뒤로 갈수록 더 빨리 쏟아진다
                int target = Mathf.CeilToInt(floodCount * Mathf.Pow(p, .6f));
                for (; done < target && done < floodCount; done++)
                {
                    string text = floodTexts[rng.Next(floodTexts.Length)];
                    SpawnText(text, floodFontSize, rng.NextDouble() < .4, Range(.55f, 1f));
                }
                yield return null;
            }
        }

        // 14번 뒤 지속 플러드: 글자는 점점 빨리(초당 몇 개 → 프레임당 여러 개), 목소리는 계속 겹쳐 재트리거.
        // 끊김 직전 프레임까지 줄어드는 것이 없게 한다.
        private IEnumerator SustainFlood()
        {
            float dur = Mathf.Max(0.05f, floodDuration);
            var target = new EndingLine
            {
                lineNoise = floodLineNoise, staticNoise = floodStaticNoise, heartbeatInterval = floodHeartbeatInterval,
                heartbeatVolume = floodHeartbeatVolume, tensionVolume = floodTensionVolume, driveBuildUpAudio = true,
            };
            SetBuildUpTargets(target, dur);

            var texts = new List<string>();
            if (floodTexts != null) foreach (var t in floodTexts) if (!string.IsNullOrEmpty(t) && !texts.Contains(t)) texts.Add(t);
            if (floodIncludeBuildUpTexts) foreach (var l in buildUpLines) if (!string.IsNullOrEmpty(l.text) && !texts.Contains(l.text)) texts.Add(l.text);
            var voices = new List<AudioClip>();
            foreach (var l in buildUpLines)
            {
                if (l.voices == null) continue;
                if (l.mode == EndingLineMode.Centered && !floodVoiceIncludeDispatcher) continue;
                foreach (var c in l.voices) if (c != null) voices.Add(c);
            }
            if (floodExtraVoices != null) foreach (var c in floodExtraVoices) if (c != null) voices.Add(c);

            float t0 = clock, end = clock + dur;
            clock = end;
            float last = seqTime, textAcc = 0f, voiceAcc = 0f;
            FloodMinActiveVoices = int.MaxValue;
            while (seqTime < end)
            {
                float now = seqTime, dt = Mathf.Max(0f, now - last); last = now;
                float p = Mathf.Clamp01((now - t0) / dur);
                float curve = Mathf.Pow(p, floodSpawnCurve);

                if (texts.Count > 0)
                {
                    textAcc += Mathf.Lerp(floodSpawnRate.x, floodSpawnRate.y, curve) * dt;
                    var size = new Vector2(Mathf.Lerp(floodFontSize.x, floodFontSizeEnd.x, p), Mathf.Lerp(floodFontSize.y, floodFontSizeEnd.y, p));
                    while (textAcc >= 1f && spawned.Count < floodMaxTexts)
                    {
                        textAcc -= 1f;
                        SpawnText(texts[rng.Next(texts.Count)], size, rng.NextDouble() < .35, Range(Mathf.Lerp(.55f, .75f, p), 1f));
                    }
                }

                if (voices.Count > 0)
                {
                    voiceAcc += Mathf.Lerp(floodVoiceRate.x, floodVoiceRate.y, curve) * dt;
                    int active = ActiveVoiceCount();
                    int minActive = Mathf.RoundToInt(Mathf.Lerp(floodMinActiveVoices, floodMinActiveVoicesEnd, p));
                    while (voiceAcc >= 1f || active < minActive)
                    {
                        if (voiceAcc >= 1f) voiceAcc -= 1f;
                        TriggerFloodVoice(voices[rng.Next(voices.Count)], p);
                        active++;
                        if (active > (crowdVoices != null ? crowdVoices.Length : 0) + 1) break;
                    }
                }
                yield return null;
            }
        }

        private void TriggerFloodVoice(AudioClip clip, float progress)
        {
            if (crowdVoices == null || crowdVoices.Length == 0 || clip == null) return;
            // 쉬고 있는 소스 우선, 없으면 가장 오래 재생된 소스를 빼앗는다
            AudioSource src = null; float best = -1f;
            foreach (var s in crowdVoices)
            {
                if (s == null) continue;
                if (!s.isPlaying) { src = s; break; }
                float played = s.clip != null ? s.time / Mathf.Max(0.01f, s.clip.length) : 1f;
                if (played > best) { best = played; src = s; }
            }
            if (src == null) return;
            src.Stop();
            src.clip = clip;
            src.loop = false;
            src.pitch = Range(floodVoicePitch.x, floodVoicePitch.y);
            src.volume = Mathf.Lerp(floodVoiceVolume.x, floodVoiceVolume.y, progress) * Range(.85f, 1f);
            src.time = Mathf.Clamp(Range(0f, floodVoiceMaxStartOffset) * clip.length, 0f, Mathf.Max(0f, clip.length - 0.05f));
            src.Play();
            FloodVoiceTriggers++;
        }

        public int ActiveVoiceCount()
        {
            int n = 0;
            if (dispatcherVoice != null && dispatcherVoice.isPlaying) n++;
            if (closeVoice != null && closeVoice.isPlaying) n++;
            if (cleanVoice != null && cleanVoice.isPlaying) n++;
            if (crowdVoices != null) foreach (var s in crowdVoices) if (s != null && s.isPlaying) n++;
            return n;
        }

        public int ActiveSourceCount()
        {
            int n = 0;
            foreach (var s in AllSources()) if (s != null && s.isPlaying) n++;
            return n;
        }

        private void StartCallLine(float volume, float fadeIn)
        {
            if (callLine == null || callLineLoop == null) return;
            callLine.clip = callLineLoop;
            callLine.loop = true;
            callLine.pitch = 1f;
            if (fadeIn > 0f) { callLine.volume = 0f; callFadeStart = seqTime; callFadeTarget = volume; }
            else { callLine.volume = volume; callFadeStart = -1f; }
            callLine.Play();
        }

        private void BeginBuildUp()
        {
            buildUpActive = true;
            noiseTo = callLine != null ? callLine.volume : callLineVolume;
            staticTo = 0f; hbVolTo = 0f; tensionTo = 0f; hbIntTo = 0f;
            tensionStarted = riserStarted = false;
            nextBeat = -1f;
            if (lineStatic != null && staticLoop != null) { lineStatic.clip = staticLoop; lineStatic.loop = true; lineStatic.volume = 0f; lineStatic.Play(); }
            // 끊김 시각을 미리 계산해 상승음의 정점을 그 프레임에 맞춘다
            float total = 0f, untilTension = -1f;
            foreach (var l in buildUpLines)
            {
                if (untilTension < 0f && l.driveBuildUpAudio && l.tensionVolume > 0f) untilTension = total;
                total += EffectiveDuration(l);
            }
            total += Mathf.Max(0f, floodDuration);
            cutTime = clock + total;
            CutTimeFromStart = cutTime - startTime;
            float tensionAt = untilTension >= 0f ? clock + untilTension : cutTime;
            tensionStartTime = tensionAt;
            riserStartTime = Mathf.Max(tensionAt, cutTime - riserPeakTime);
            extraStarted = new bool[extraCutRisers != null ? extraCutRisers.Count : 0];
            PreCutActiveVoices = PreCutActiveSources = PreCutTexts = PostCutActiveSources = PostCutTexts = -1;
            FloodMinActiveVoices = -1; FloodVoiceTriggers = 0; TextCountEverDecreased = false; lastTextCount = 0;
            cutPending = false;
        }
        private bool[] extraStarted = new bool[0];
        private int lastTextCount;
        private bool cutPending;

        private void SetBuildUpTargets(EndingLine line, float dur)
        {
            float now = seqTime;
            // 현재 값에서 시작
            float k = Ramp(now);
            noiseFrom = Mathf.Lerp(noiseFrom, noiseTo, k); staticFrom = Mathf.Lerp(staticFrom, staticTo, k);
            hbVolFrom = Mathf.Lerp(hbVolFrom, hbVolTo, k); tensionFrom = Mathf.Lerp(tensionFrom, tensionTo, k);
            hbIntFrom = hbIntTo > 0f ? Mathf.Lerp(hbIntFrom > 0f ? hbIntFrom : hbIntTo, hbIntTo, k) : line.heartbeatInterval;
            noiseTo = line.lineNoise; staticTo = line.staticNoise; hbVolTo = line.heartbeatVolume; tensionTo = line.tensionVolume; hbIntTo = line.heartbeatInterval;
            if (hbIntFrom <= 0f) hbIntFrom = hbIntTo;
            rampStart = now; rampEnd = now + Mathf.Max(0.05f, dur);
            if (line.heartbeatInterval > 0f && nextBeat < 0f) nextBeat = now;
        }

        private float Ramp(float now) => rampEnd > rampStart ? Mathf.Clamp01((now - rampStart) / (rampEnd - rampStart)) : 1f;

        private void HardCut()
        {
            buildUpActive = false;
            cutPending = true;
            foreach (var c in spawners) if (c != null) StopCoroutine(c);
            spawners.Clear();
            foreach (var s in AllSources()) if (s != null) s.Stop();
            if (randomTextLayer != null)
            {
                for (int i = randomTextLayer.childCount - 1; i >= 0; i--)
                {
                    var child = randomTextLayer.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }
            spawned.Clear(); popping.Clear();
            if (centerText != null) centerText.text = "";
            typing = false;
            nextBeat = -1f; callFadeStart = -1f;
        }

        private void PlayHit()
        {
            if (hit == null) return;
            hitBaseVolume = climaxHitVolume;
            hit.volume = climaxHitVolume;
            if (climaxHit != null)
            {
                hit.clip = climaxHit;
                hit.loop = false;
                int start = Mathf.Clamp(Mathf.RoundToInt(climaxHitStart * climaxHit.frequency), 0, climaxHit.samples - 1);
                hit.timeSamples = start;
                hit.Play();
                hit.timeSamples = start;
            }
            if (climaxHitExtraLayers != null) foreach (var c in climaxHitExtraLayers) if (c != null) hit.PlayOneShot(c);
            hitFadeStartTime = -1f;
            hitStartedAt = seqTime;
        }
        private float hitStartedAt = -1f;

        private void Update()
        {
            seqTime += Mathf.Min(Time.unscaledDeltaTime, maxFrameStep);
            float now = seqTime;

            if (typing && centerText != null)
                centerText.maxVisibleCharacters = Mathf.CeilToInt((now - typeStart) * typeCharsPerSecond);

            for (int i = popping.Count - 1; i >= 0; i--)
            {
                var (rect, t) = popping[i];
                if (rect == null) { popping.RemoveAt(i); continue; }
                float p = popTime > 0f ? Mathf.Clamp01((now - t) / popTime) : 1f;
                rect.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, p);
                if (p >= 1f) popping.RemoveAt(i);
            }

            if (callFadeStart >= 0f && callLine != null)
            {
                float p = returnCallLineFadeIn > 0f ? Mathf.Clamp01((now - callFadeStart) / returnCallLineFadeIn) : 1f;
                callLine.volume = callFadeTarget * p;
                if (p >= 1f) callFadeStart = -1f;
            }

            if (buildUpActive)
            {
                float k = Ramp(now);
                if (callLine != null) callLine.volume = Mathf.Lerp(noiseFrom, noiseTo, k);
                if (lineStatic != null) lineStatic.volume = Mathf.Lerp(staticFrom, staticTo, k);
                if (heartbeat != null) heartbeat.volume = Mathf.Lerp(hbVolFrom, hbVolTo, k);
                float beatInterval = Mathf.Lerp(hbIntFrom, hbIntTo, k);
                if (heartbeatOne != null && heartbeat != null && hbIntTo > 0f && nextBeat >= 0f && now >= nextBeat)
                {
                    heartbeat.PlayOneShot(heartbeatOne);
                    nextBeat = now + Mathf.Max(0.12f, beatInterval);
                }
                if (!tensionStarted && now >= tensionStartTime && tension != null && tensionClip != null)
                {
                    tensionStarted = true;
                    tension.clip = tensionClip; tension.loop = false;
                    tension.time = Mathf.Clamp(tensionClipStart, 0f, tensionClip.length - 0.01f);
                    tension.Play();
                }
                if (tension != null && tensionStarted)
                {
                    tension.volume = Mathf.Lerp(tensionFrom, tensionTo, k);
                    float tp = cutTime > tensionStartTime ? Mathf.Clamp01((now - tensionStartTime) / (cutTime - tensionStartTime)) : 1f;
                    tension.pitch = Mathf.Lerp(tensionPitch.x, tensionPitch.y, tp);
                }
                if (!riserStarted && now >= riserStartTime && riser != null && riserClip != null)
                {
                    riserStarted = true;
                    riser.clip = riserClip; riser.loop = false; riser.volume = riserVolume; riser.pitch = 1f;
                    // 늦게 시작한 만큼 앞을 건너뛰어 정점이 끊김 프레임에 오게 한다
                    riser.time = Mathf.Clamp(now - riserStartTime, 0f, riserClip.length - 0.01f);
                    riser.Play();
                }
                if (riser != null && riserStarted)
                {
                    float rp = cutTime > riserStartTime ? Mathf.Clamp01((now - riserStartTime) / (cutTime - riserStartTime)) : 1f;
                    riser.volume = Mathf.Lerp(riserVolume, riserVolumeAtCut, rp);
                }
                if (extraCutRisers != null)
                {
                    for (int i = 0; i < extraCutRisers.Count && i < extraStarted.Length; i++)
                    {
                        var r = extraCutRisers[i];
                        if (r == null || r.source == null || r.clip == null) continue;
                        float pitch = Mathf.Max(0.1f, r.pitch);
                        float start = cutTime - r.peakTime / pitch;
                        if (!extraStarted[i] && now >= start)
                        {
                            extraStarted[i] = true;
                            r.source.clip = r.clip; r.source.loop = false; r.source.pitch = pitch;
                            r.source.time = Mathf.Clamp((now - start) * pitch, 0f, r.clip.length - 0.01f);
                            r.source.Play();
                        }
                        if (extraStarted[i])
                        {
                            float rp = cutTime > start ? Mathf.Clamp01((now - start) / (cutTime - start)) : 1f;
                            r.source.volume = Mathf.Lerp(r.volume.x, r.volume.y, rp);
                        }
                    }
                }

                // 검증 기록 (코루틴보다 먼저 도는 Update라서, 끊김 프레임의 이 값 = 끊김 직전 상태)
                PreCutActiveVoices = ActiveVoiceCount();
                PreCutActiveSources = ActiveSourceCount();
                PreCutTexts = spawned.Count;
                PreCutRiserVolume = riser != null ? riser.volume : -1f;
                if (spawned.Count < lastTextCount) TextCountEverDecreased = true;
                lastTextCount = spawned.Count;
                if (Stage == "BuildUp_Flood" && FloodMinActiveVoices >= 0 && now - stageStart > 0.05f)
                    FloodMinActiveVoices = Mathf.Min(FloodMinActiveVoices, PreCutActiveVoices);
            }
            else if (cutPending)
            {
                cutPending = false;
                PostCutActiveSources = ActiveSourceCount();
                PostCutTexts = randomTextLayer != null ? randomTextLayer.childCount : spawned.Count;
                Debug.Log("[SuccessEnding] 끊김 검증: 직전 프레임 목소리 " + PreCutActiveVoices + "개 / 소스 " + PreCutActiveSources +
                          "개 / 글자 " + PreCutTexts + "개 → 끊김 뒤 소스 " + PostCutActiveSources + "개 / 글자 " + PostCutTexts +
                          "개. 플러드 중 최소 목소리 " + FloodMinActiveVoices + ", 재트리거 " + FloodVoiceTriggers +
                          "회, 글자 수 감소 " + (TextCountEverDecreased ? "있음" : "없음") + ", 컷 시각 " + CutTimeFromStart.ToString("0.00") + "s", this);
            }

            if (cursorActive) UpdateCursor(now);
            if (tabletGlitchStopAt >= 0f && tabletGlitch != null)
            {
                // 글리치 소리는 앞부분만: 끝 50ms 동안 줄였다가 끊는다
                float left = tabletGlitchStopAt - now;
                tabletGlitch.volume = tabletGlitchVolume * Mathf.Clamp01(left / 0.05f);
                if (left <= 0f) { tabletGlitch.Stop(); tabletGlitchStopAt = -1f; }
            }
            if (flashStart >= 0f)
            {
                float t = now - flashStart;
                if (flashOverlay != null)
                {
                    var c = flashOverlay.color;
                    c.a = flashOverlayDecay > 0f ? flashOverlayAlpha * Mathf.Clamp01(1f - t / flashOverlayDecay) : 0f;
                    flashOverlay.color = c;
                }
                if (finalText != null)
                {
                    if (now < shakeEnd)
                    {
                        float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, shakeEnd - flashStart)) * .5f;
                        finalText.rectTransform.anchoredPosition = finalBasePos + new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * shakeAmplitude * k;
                        finalText.color = Color.Lerp(flashTextColor, textColor, Mathf.Clamp01(t / 0.25f));
                    }
                    else if (!string.IsNullOrEmpty(finalText.text))
                    {
                        finalText.text = "";
                        finalText.rectTransform.anchoredPosition = finalBasePos;
                        finalText.rectTransform.localScale = Vector3.one;
                    }
                }
                if (now >= shakeEnd && (flashOverlay == null || flashOverlay.color.a <= 0f)) flashStart = -1f;
            }
            if (tailStart >= 0f)
            {
                float t = now - tailStart;
                if (tinnitus != null && tinnitus.isPlaying)
                    tinnitus.volume = tinnitusVolume * (tinnitusFade > 0f ? Mathf.Clamp01(1f - t / tinnitusFade) : 0f);
                if (!blackoutStarted && t >= blackoutDelay && blackout != null && blackoutClip != null)
                {
                    blackoutStarted = true;
                    blackout.clip = blackoutClip; blackout.loop = false; blackout.volume = blackoutVolume; blackout.Play();
                }
                if (blackoutStarted && blackout != null)
                {
                    float span = Mathf.Max(0.05f, tailDuration - blackoutDelay);
                    blackout.volume = blackoutVolume * Mathf.Clamp01(1f - (t - blackoutDelay) / span);
                }
            }

            UpdateDeathAudio(now);

            if (hit != null && hitStartedAt >= 0f && climaxHitMaxLength > 0f)
            {
                float playedFor = now - hitStartedAt;
                if (hitFadeStartTime >= 0f)
                {
                    float fadeLen = Mathf.Max(0.01f, Mathf.Min(hitFadeOut, climaxHitMaxLength - (hitFadeStartTime - hitStartedAt)));
                    float p = Mathf.Clamp01((now - hitFadeStartTime) / fadeLen);
                    hit.volume = hitBaseVolume * (1f - p);
                    if (p >= 1f) { hit.Stop(); hitStartedAt = -1f; }
                }
                if (hitStartedAt >= 0f && playedFor >= climaxHitMaxLength) { hit.Stop(); hitStartedAt = -1f; }
            }

#if UNITY_EDITOR
            if (!debugBroken && !string.IsNullOrEmpty(debugBreakAtStage) && Stage == debugBreakAtStage && now - stageStart >= debugBreakDelay)
            {
                debugBroken = true;
                Debug.Log("[SuccessEnding] debug break at " + Stage + " +" + (now - stageStart).ToString("0.00") + "s, texts=" + spawned.Count, this);
                Debug.Break();
            }
#endif
        }
    }
}


using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightDuty
{
    /// <summary>회차 안에서 조우 장면 하나가 놓인 상태.</summary>
    public enum EncounterState
    {
        /// <summary>아직 그 회차에서 깔린 적이 없다(또는 어젯밤 못 보고 물러나 대기 중이다).</summary>
        Idle = 0,

        /// <summary>오늘 깔려 있고 아직 관찰되지 않았다. 접근 단계와 퇴실 게이트가 여기서만 움직인다.</summary>
        Active = 1,

        /// <summary>가림 없이 1초 관찰됐다. <b>아직 끝난 것이 아니다</b> — 그 방문의 퇴실이 남았다.</summary>
        Observed = 2,

        /// <summary>관찰 + 퇴실을 모두 마쳤다. 그 회차에서 소비돼 다시 깔리지 않는다.</summary>
        Done = 3
    }

    /// <summary>
    /// 조우 8장면을 회차에 배분하고, 하룻밤 동안 모형을 단계적으로 플레이어 쪽으로 옮긴다
    /// (2026-09-21 재설계안 8절 「조우 8장면 전소진」·9절 「인체모형 필연 조우 3단계」).
    /// <list type="bullet">
    /// <item><b>배분은 고정이다.</b> 1일 S-A · 2일 C-A · 3일 T-B+H-A · 4일 C-B+T-A · 5일 H-B+S-B.
    /// 무작위로 고르면 회차마다 두세 장면이 남는데, 8장면은 한 장씩이 전부 다른 연출이라
    /// 안 나온 장면은 그 회차에서 영영 못 본 콘텐츠가 된다. <b>전소진이 목적이라 추첨을 쓰지 않는다.</b></item>
    /// <item><b>발견형 3장(H-B · C-B · T-B)을 3·4·5일에 심는다.</b> 발견형은 두 선택 모두 델타 0이라,
    /// 잘하는 플레이어가 <b>수치 손해 없이 겪는 유일한 공포</b>다. 앞쪽에 몰면 뒤쪽 사흘이
    /// 「어기면 손해」뿐인 밤이 되고, 잘하는 플레이어의 긴장이 0으로 내려간다.</item>
    /// <item><b>하루 최대 3장면</b>(이월분 포함). 그 이상은 공포가 아니라 소란이다 — 모형을 세 번 넘게 마주치면
    /// 「또 있네」가 되고, 시야 밖에서만 움직인다는 규칙 자체가 눈치채인다.</item>
    /// <item><b>추격 AI 없음 · 강제 카메라 회전 없음 · 체류 즉사 없음.</b> 필연 조우 3단계는 이 셋을
    /// 지킨 채로 「안 보려는 플레이어도 결국 만나게」 만드는 장치다. 이 금기를 어기는 순간
    /// 이 게임은 다른 장르가 된다(CLAUDE.md §2.7).</item>
    /// </list>
    /// <para>
    /// <b>위치·시야는 이 클래스가 모른다.</b> 시야 판정은 <see cref="IsVisible"/>, 실제 배치는
    /// <see cref="PlaceModel"/>로 씬에 넘긴다. <see cref="ParadoxDirector"/>가 좌표를 모르는 것과 같은 이유이고,
    /// 이 경계를 무너뜨리면 레벨을 손볼 때마다 판정 코어를 같이 고쳐야 한다.
    /// </para>
    /// <para>
    /// <b>회차 내내 살아 있어야 한다.</b> 어느 장면을 봤는지 · 어느 G를 보냈는지 · 무엇이 이월됐는지를 들고 있다.
    /// 새 회차는 <see cref="BeginRun"/>으로 연다.
    /// </para>
    /// </summary>
    public sealed class EncounterDirector
    {
        /// <summary>복도 A. 1단계 접근만 있는 평범한 장면.</summary>
        public const string SceneHA = "scene.ha";

        /// <summary>복도 B. <b>발견형</b>이자 <b>퇴실 게이트</b> 장면.</summary>
        public const string SceneHB = "scene.hb";

        /// <summary>교실 1-1 A.</summary>
        public const string SceneCA = "scene.ca";

        /// <summary>교실 1-1 B. <b>발견형</b>.</summary>
        public const string SceneCB = "scene.cb";

        /// <summary>
        /// 과학실 A. 1일차 고정이며 최초 조우다.
        /// 카드 S1(<c>science.model.sa.face</c>)이 여는 장면과 같은 모형을 쓴다.
        /// </summary>
        public const string SceneSA = "scene.sa";

        /// <summary>
        /// 과학실 B. <b>퇴실 게이트</b> 장면. 카드 S5의 트리거 ID가 이 문자열이다
        /// (2026-09-21 실측 기준선 — 바꾸면 S5가 영영 시작되지 않는다).
        /// </summary>
        public const string SceneSB = "scene.sb";

        /// <summary>
        /// 화장실 A. <b>퇴실 게이트</b> 장면. 카드 T3의 트리거 ID가 이 문자열이다
        /// (2026-09-21 실측 기준선 — 바꾸면 T3가 영영 시작되지 않는다).
        /// </summary>
        public const string SceneTA = "scene.ta";

        /// <summary>화장실 B. <b>발견형</b>.</summary>
        public const string SceneTB = "scene.tb";

        /// <summary>조우 장면 수. 회차 5일에 정확히 이 수만큼 깔린다(전소진).</summary>
        public const int SceneCount = 8;

        /// <summary>
        /// 하루 최대 장면 수(이월분 포함). 배분표 자체는 하루 2장면까지라, 이 상한은
        /// <b>이월이 겹쳤을 때만</b> 걸린다. 넘친 장면은 소비되지 않고 다음 날로 다시 넘어간다.
        /// </summary>
        public const int MaxScenesPerNight = 3;

        /// <summary>
        /// 하루 안에서 모형이 자리를 옮기는 최대 횟수. 3인 이유는 단계마다 <b>뜻이 다르기</b> 때문이다 —
        /// 1회는 「몰라볼 수도 있는 시야 주변부」, 2회는 「지나가면 시야 가장자리에 반드시 걸림」,
        /// 3회는 「안 보려면 고개를 돌려야 하는 거리」. 네 번째 단계는 문 앞을 막는 것 말고 남는 자리가 없고,
        /// 그건 출구 봉쇄라 금기다.
        /// </summary>
        public const int ApproachStepCount = 3;

        /// <summary>회차 마지막 일차. 배분표가 이 수만큼의 칸을 갖는다.</summary>
        public const int LastDay = 5;

        /// <summary>
        /// 회차 배분을 다시 뽑아 보는 최대 횟수. 제약을 만족하는 배분이 넉넉히 많아 보통 한두 번에 걸린다.
        /// <see cref="DayDirector"/>의 <c>MaxAttempts</c>와 같은 뜻의 상수다.
        /// </summary>
        private const int MaxPlanAttempts = 64;

        /// <summary>1일차에 고정 배정하는 장면(최초 조우). 유도 문자가 없는 유일한 장면이다.</summary>
        public const string FirstDaySceneId = SceneSA;

        /// <summary>
        /// 유도 문자 1통을 여는 <b>서로 다른</b> 일반 점검 수. 「모형을 아직 관찰하지 않은 상태에서
        /// 서로 다른 일반 점검 2곳을 완료하면 1통」(기획서 10-3).
        /// 점검 ID는 5개뿐이라 하룻밤에 최대 2통이고, 그래서 하루 2장면인 3·4·5일에 꼭 맞는다.
        /// </summary>
        public const int InspectionsPerMessage = 2;

        /// <summary>재방문 문자 ID. 장면에 속하지 않는 단 하나의 문자다.</summary>
        public const string NoticeMessageId = "N1";

        /// <summary>재방문 문자를 보내는 일차. <b>2일차 고정</b>이다.</summary>
        public const int NoticeDay = 2;

        /// <summary>재방문 문자의 회차 발송 한도.</summary>
        public const int NoticeLimitPerRun = 1;

        /// <summary>
        /// 장면 ID 8개(정본 순서). 배분이 아니라 <b>목록</b>이다 — 어느 날 나오는지는 <see cref="PlanFor"/>가 안다.
        /// </summary>
        public static readonly string[] AllSceneIds =
        {
            SceneHA, SceneHB, SceneCA, SceneCB, SceneSA, SceneSB, SceneTA, SceneTB
        };

        /// <summary>
        /// 퇴실 게이트를 붙이는 장면 셋(2026-09-21 결정).
        /// <para>
        /// <b>왜 셋뿐인가.</b> 여덟 장면 전부에 붙이면 「안 보고 나가면 문 앞에 선다」가 규칙으로 읽히고,
        /// 규칙으로 읽히는 순간 공포가 아니라 조작법이 된다. 셋이면 플레이어는 그것을 법칙으로 세우지 못한다.
        /// 회차 후반(S-B·T-A·H-B는 4·5일 배분)에 몰려 있는 것도 의도다 — 앞쪽에서 학습하면 늦게 심은 뜻이 없다.
        /// </para>
        /// </summary>
        public static readonly string[] ExitGateSceneIds = { SceneSB, SceneTA, SceneHB };

        /// <summary>
        /// 발견형 장면 셋. <b>두 선택 모두 델타 0</b>이며 새 판정을 만들지 않는다(CLAUDE.md §2.7).
        /// </summary>
        public static readonly string[] DiscoverySceneIds = { SceneHB, SceneCB, SceneTB };

        /// <summary>
        /// 일차별 <b>장면 수</b>(0번 칸 = 1일차). 합이 <see cref="SceneCount"/>라 회차 5일에 8장면이 전소진된다.
        /// <para>
        /// 2026-09-22까지는 어느 날 어느 장면이 나오는지도 이 표가 고정했다. 시나리오 기획서 v6 §4가
        /// 「날짜별 공간을 고정하지 않는다」로 뒤집어, 이제 <b>장면 수만</b> 고정이고 배분은 회차마다
        /// <see cref="BuildRunPlan"/>이 다시 뽑는다. 되살리지 말 것: 「1일 S-A · 2일 C-A · 3일 T-B+H-A ·
        /// 4일 C-B+T-A · 5일 H-B+S-B 고정」.
        /// </para>
        /// </summary>
        private static readonly int[] SlotsPerDay = { 1, 1, 2, 2, 2 };

        /// <summary>
        /// 장면이 나갈 수 있는 <b>가장 이른 일차</b>.
        /// <para>
        /// <b>남은 제약은 발견형 셋뿐이다.</b> 두 선택 모두 델타 0이라 잘하는 플레이어가 수치 손해 없이
        /// 겪는 유일한 공포이고, 앞쪽으로 당기면 후반이 빈다(<see cref="DiscoverySceneIds"/>).
        /// 3장을 3·4·5일에 한 장씩 심는다.
        /// </para>
        /// <para>
        /// <b>S-B 3일차 · T-A 4일차 제약은 2026-09-22 자격 재설계로 사라졌다.</b> S5·T3의 배치 자격이
        /// 각각 Band1·Band2였을 때는 일차 하한이 그 값에 닿는 날부터만 장면을 깔 수 있었다 —
        /// 더 이르면 <b>장면은 있는데 카드가 없는 밤</b>이 됐다. 두 카드를 Band0으로 내려 그 매듭을 풀었다.
        /// 되살리지 말 것: 「S-B는 3일차부터」 · 「T-A는 4일차부터」.
        /// </para>
        /// </summary>
        private static int EarliestDayOf(string sceneId)
        {
            return IsDiscovery(sceneId) ? 3 : 2;
        }

        private readonly EncounterTableSO _table;
        private readonly System.Random _rng;

        // 회차 상태 — 표 인덱스와 1:1. 표가 생성 시점에 고정되므로 Dictionary 대신 배열을 쓴다(DayDirector와 같은 이유).
        private readonly EncounterState[] _state;
        private readonly int[] _placedStep;    // 실제로 놓은 접근 단계(0 = 아직 안 놓음).
        private readonly int[] _wantStep;      // 놓고 싶은 접근 단계. 시야에 걸리면 placed보다 앞서 있다.
        private readonly bool[] _placedGate;   // 퇴실 게이트를 실제로 놓았는지(하룻밤 1회).
        private readonly bool[] _wantGate;     // 게이트를 놓고 싶은지(시야 대기 포함).
        private readonly bool[] _messageSent;  // 그 장면의 G를 회차에서 보냈는지.

        // 회차용 배분(하루 안의 순서만 섞은 것). 인덱스 0 = 1일차.
        private readonly List<string>[] _runPlan = new List<string>[LastDay];

        private readonly List<string> _todayScenes = new List<string>();
        private readonly List<string> _carriedOver = new List<string>();

        /// <summary>
        /// 주인 장면이 이미 소비됐는데 아직 못 보낸 G의 장면 ID 큐(이월분).
        /// <b>취소가 아니라 이월인 이유</b>는 <see cref="EndNight"/> 주석에 적었다.
        /// </summary>
        private readonly List<string> _carriedMessages = new List<string>();

        private readonly HashSet<SpaceId> _inspectedToday = new HashSet<SpaceId>();
        private readonly StringBuilder _report = new StringBuilder();

        private int _day;
        private bool _runStarted;
        private bool _nightOpen;
        private bool _laidToday;          // 그날 첫 일반 점검을 마쳐 장면을 깔았는지.
        private int _creditsIssued;       // 오늘 발행한 문자 크레딧 수.
        private int _creditsSpent;        // 오늘 실제로 보낸 문자 수(G만. N1은 크레딧을 쓰지 않는다).
        private int _noticeSentCount;     // 회차 N1 발송 횟수.
        private bool _visibleWarned;      // 시야 델리게이트 미주입 경고를 한 번만 남기기 위한 표시.
        private string _lastReport = string.Empty;

        /// <summary>
        /// 회차용 조우 연출기를 만든다.
        /// </summary>
        /// <param name="table">조우 8장면 표. null이면 경고를 남기고 아무것도 깔지 않는다(밤은 그대로 진행된다).</param>
        /// <param name="rng">난수. null이면 시드 없는 인스턴스를 만든다.
        /// <b>배분 자체는 난수를 쓰지 않는다</b> — 하루 안의 순서만 섞는다. 테스트는 시드를 고정한 것을 넣는다.</param>
        public EncounterDirector(EncounterTableSO table, System.Random rng = null)
        {
            _table = table;
            _rng = rng ?? new System.Random();

            if (_table == null)
            {
                Debug.LogWarning("[EncounterDirector] 조우 표가 없습니다. 장면을 하나도 깔지 않습니다. " +
                                 "Resources/" + EncounterTableSO.ResourcePath + " 에셋을 확인하십시오.");
            }

            int n = _table != null ? _table.Scenes.Count : 0;
            _state = new EncounterState[n];
            _placedStep = new int[n];
            _wantStep = new int[n];
            _placedGate = new bool[n];
            _wantGate = new bool[n];
            _messageSent = new bool[n];
        }

        /// <summary>
        /// 장면 ID → <b>그 장면의 모형이 지금 플레이어 시야 안인가</b>. 씬 쪽이 채운다.
        /// <para>
        /// <b>모형은 시야 밖에 있을 때만 위치가 바뀐다</b>(CLAUDE.md §2.7). 이 게임에 추격 AI가 없는 이상,
        /// 「보고 있는 동안 움직이지 않는다」가 모형이 무서운 이유의 전부다. 한 번이라도 눈앞에서 움직이면
        /// 플레이어는 그것을 연출로 분류하고 그 뒤로는 무서워하지 않는다.
        /// </para>
        /// <para>
        /// <b>주입이 없으면 「항상 안 보임」으로 본다.</b> 반대로(항상 보임) 두면 모형이 영영 움직이지 않아
        /// 조우가 통째로 사라지는데, 그건 버그가 아니라 <b>빈 게임</b>으로 보인다 — 알아채기가 훨씬 어렵다.
        /// 대신 경고를 한 번 남긴다.
        /// </para>
        /// </summary>
        public Func<string, bool> IsVisible { get; set; }

        /// <summary>
        /// (장면 ID, 대상 ID) → 그 지점에 모형을 놓아라. 씬 쪽이 채운다.
        /// <para>
        /// 공개 API 목록에는 없지만 <b>없으면 이 클래스가 아무 일도 하지 않는다</b> —
        /// 판정 코어가 좌표를 모르는 이상, 결정한 배치를 밖으로 내보낼 통로가 하나는 있어야 한다.
        /// 대상 ID는 씬의 <see cref="JudgeTarget"/> ID이고 실제 위치·회전은 씬이 안다.
        /// </para>
        /// <para>같은 지점에 두 번 부르지 않는다. 호출 시점은 <b>반드시 시야 밖</b>이다.</para>
        /// </summary>
        public Action<string, string> PlaceModel { get; set; }

        /// <summary>
        /// 현재 게임 시각(0:00 기준 분). 문자에 찍는다. null이면 -1로 기록한다
        /// (<see cref="ParadoxMessage.Minute"/>의 「모르면 -1」 약속).
        /// </summary>
        public Func<int> ClockMinutes { get; set; }

        /// <summary>
        /// 오늘 역설 문자를 이미 보냈는가. <b>N1은 역설을 보낸 날에는 발송하지 않는다</b>(기획서 10-4).
        /// <para>
        /// <see cref="ParadoxDirector"/>를 직접 참조하지 않고 델리게이트로 받는 이유는, 두 연출기가 서로를
        /// 모르는 편이 테스트와 순서 문제 양쪽에서 낫기 때문이다(<c>NightRun</c>이 <c>Paradox.Today.Count &gt; 0</c>을 넘겨 주면 된다).
        /// </para>
        /// <para>
        /// <b>판정은 보내는 순간의 한 점이다.</b> N1은 그날 첫 일반 점검 직후에 나가므로, 그 뒤에 역설이 나가면
        /// 같은 밤에 둘 다 존재할 수 있다. 「역설을 먼저 보냈으면 N1을 안 보낸다」까지가 규칙이고
        /// 「N1을 보냈으면 역설을 막는다」는 규칙이 아니다 — 역설은 짝 카드가 시작되는 시점에 매여 있어
        /// 뒤로 미룰 수 없다.
        /// </para>
        /// </summary>
        public Func<bool> ParadoxSentToday { get; set; }

        /// <summary>오늘 깔린 장면 ID(깔린 순서). 배정 순서이자 유도 문자 우선순위다.</summary>
        public IReadOnlyList<string> TodayScenes
        {
            get { return _todayScenes; }
        }

        /// <summary>못 보고 다음 날로 넘어간 장면 ID.</summary>
        public IReadOnlyList<string> CarriedOver
        {
            get { return _carriedOver; }
        }

        /// <summary>
        /// 디버그용 한 줄 요약. 「오늘 왜 이 장면이 깔렸고 지금 어디까지 왔는지」 —
        /// 일차 · 오늘 장면과 상태 · 접근 단계 · 게이트 · 이월 · 문자 크레딧이 들어 있다.
        /// 조우가 이상해 보일 때 가장 먼저 볼 문자열이다.
        /// </summary>
        public string LastReport
        {
            get { return _lastReport; }
        }

        /// <summary>현재 일차(1부터). 회차 시작 전에는 0.</summary>
        public int Day
        {
            get { return _day; }
        }

        /// <summary>회차 N1 발송 횟수.</summary>
        public int NoticeSentCount
        {
            get { return _noticeSentCount; }
        }

        /// <summary>
        /// 새 회차를 연다. 배분을 확정하고 장면 상태·문자 이력·이월을 전부 비운다.
        /// <para>
        /// <b>난수는 초기화하지 않는다</b> — 시드를 다시 심는 것은 호출자의 몫이다
        /// (<see cref="DayDirector.Reset"/>과 같은 규칙).
        /// </para>
        /// </summary>
        public void BeginRun()
        {
            _runStarted = true;
            _nightOpen = false;
            _day = 0;
            _laidToday = false;
            _creditsIssued = 0;
            _creditsSpent = 0;
            _noticeSentCount = 0;

            for (int i = 0; i < _state.Length; i++)
            {
                _state[i] = EncounterState.Idle;
                _placedStep[i] = 0;
                _wantStep[i] = 0;
                _placedGate[i] = false;
                _wantGate[i] = false;
                _messageSent[i] = false;
            }

            _todayScenes.Clear();
            _carriedOver.Clear();
            _carriedMessages.Clear();
            _inspectedToday.Clear();

            BuildRunPlan();

            _lastReport = "회차 시작: 8장면 배분 확정(1일 " + Join(_runPlan[0]) + " · 2일 " + Join(_runPlan[1]) +
                          " · 3일 " + Join(_runPlan[2]) + " · 4일 " + Join(_runPlan[3]) + " · 5일 " + Join(_runPlan[4]) + ")";
        }

        /// <summary>
        /// 회차 배분을 뽑는다. 8장면이 5일에 정확히 한 번씩 들어가되 <b>어느 날 어느 공간인지는 회차마다 다르다</b>
        /// (시나리오 기획서 v6 §4).
        /// <para>지키는 것 넷 — 이 중 하나라도 깨지면 설계가 무너진다.</para>
        /// <list type="number">
        /// <item><b>1일차는 S-A 하나.</b> 최초 조우이고, 기획서 §3-3이 「이전 근무자의 마지막 확인 장소는 과학실」로
        /// 못박았다. 여기가 흔들리면 프롤로그 전체가 흔들린다.</item>
        /// <item><b>발견형 셋은 3·4·5일에 한 장씩.</b> 잘하는 플레이어가 수치 손해 없이 겪는 유일한 공포라
        /// 후반에 고루 남아야 한다.</item>
        /// <item><b>장면마다 가장 이른 일차</b>(<see cref="EarliestDayOf"/>). 카드 자격이 여기 매달려 있다.</item>
        /// <item><b>하루 장면 수</b>(<see cref="SlotsPerDay"/>). 합이 8이라 전소진이 성립한다.</item>
        /// </list>
        /// <para>
        /// 뽑기는 <b>거절 표본</b>이다 — 섞어서 넣어 보고 제약이 깨지면 다시 섞는다. 제약을 만족하는 배분이
        /// 넉넉히 많아 몇 번이면 걸리고, <see cref="MaxPlanAttempts"/>번 모두 실패하면 결정적 배분으로 물러난다
        /// (<see cref="FallbackPlan"/>). 밤을 못 여는 것보다 예측 가능한 밤이 낫다.
        /// </para>
        /// </summary>
        private void BuildRunPlan()
        {
            for (int attempt = 0; attempt < MaxPlanAttempts; attempt++)
            {
                if (TryBuildRunPlan())
                {
                    return;
                }
            }

            FallbackPlan();
        }

        /// <summary>한 번 뽑아 본다. 제약을 못 지키면 false.</summary>
        private bool TryBuildRunPlan()
        {
            for (int d = 0; d < _runPlan.Length; d++)
            {
                _runPlan[d] = new List<string>(SlotsPerDay[d]);
            }

            // 1일차는 고정이다. 나머지 일곱 장을 섞어 빈 칸에 차례로 넣는다.
            _runPlan[0].Add(FirstDaySceneId);

            List<string> rest = new List<string>(SceneCount - 1);
            for (int i = 0; i < AllSceneIds.Length; i++)
            {
                if (AllSceneIds[i] != FirstDaySceneId)
                {
                    rest.Add(AllSceneIds[i]);
                }
            }

            Shuffle(rest);

            // 발견형을 먼저 3·4·5일에 한 장씩 앉힌다. 나중에 앉히면 자리가 남지 않아 거절률이 크게 오른다.
            List<int> discoveryDays = new List<int> { 3, 4, 5 };
            Shuffle(discoveryDays);

            int next = 0;
            for (int i = 0; i < rest.Count; i++)
            {
                if (!IsDiscovery(rest[i]))
                {
                    continue;
                }

                int day = discoveryDays[next];
                next++;

                if (day < EarliestDayOf(rest[i]) || _runPlan[day - 1].Count >= SlotsPerDay[day - 1])
                {
                    return false;
                }

                _runPlan[day - 1].Add(rest[i]);
            }

            // 남은 장면을 빈 칸에 넣는다. 가장 이른 일차를 못 지키면 그 자리에서 실패로 본다.
            for (int i = 0; i < rest.Count; i++)
            {
                if (IsDiscovery(rest[i]))
                {
                    continue;
                }

                if (!PlaceInFirstFreeDay(rest[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>그 장면을 넣을 수 있는 빈 칸 중 하나를 골라 넣는다.</summary>
        private bool PlaceInFirstFreeDay(string sceneId)
        {
            int earliest = EarliestDayOf(sceneId);

            List<int> free = new List<int>(_runPlan.Length);
            for (int day = earliest; day <= LastDay; day++)
            {
                if (_runPlan[day - 1].Count < SlotsPerDay[day - 1])
                {
                    free.Add(day);
                }
            }

            if (free.Count == 0)
            {
                return false;
            }

            int pick = free[_rng.Next(free.Count)];
            _runPlan[pick - 1].Add(sceneId);
            return true;
        }

        /// <summary>
        /// 뽑기가 모두 실패했을 때 쓰는 결정적 배분. 2026-09-22 이전의 고정표와 같은 구성이라
        /// 모든 제약을 확실히 지킨다. <b>정상 경로에서는 쓰이지 않는다</b> — 여기로 떨어지면 제약이
        /// 서로 모순됐다는 뜻이므로 보고 문구에 남긴다.
        /// </summary>
        private void FallbackPlan()
        {
            _runPlan[0] = new List<string> { SceneSA };
            _runPlan[1] = new List<string> { SceneCA };
            _runPlan[2] = new List<string> { SceneTB, SceneHA };
            _runPlan[3] = new List<string> { SceneCB, SceneTA };
            _runPlan[4] = new List<string> { SceneHB, SceneSB };
        }

        /// <summary>
        /// 하룻밤을 연다. 그날 후보(배분표)와 이월분을 합쳐 <see cref="TodayScenes"/>를 정한다.
        /// <para>
        /// <b>여기서는 아무것도 준비하지 않는다.</b> 모형이 실제로 놓이는 것은 그날 첫 일반 점검
        /// (<see cref="SignalKind.InspectionCompleted"/>)을 마친 뒤다 —
        /// 밤 시작 순간에 놓으면 플레이어가 아직 어느 공간에 있는지도 모르는 상태라
        /// 「시야 밖에서만」을 지켰는지 확인할 방법이 없다.
        /// </para>
        /// <para>
        /// 이월분을 배분표보다 <b>앞</b>에 넣는다. 어제 못 본 장면이 오늘 더 가까이 오는 것이 3단계의 뜻이고,
        /// 상한(<see cref="MaxScenesPerNight"/>)에 걸려 잘릴 때 잘려야 하는 쪽은 오늘 처음 나오는 장면이다.
        /// </para>
        /// </summary>
        /// <param name="day">일차(1부터).</param>
        public void BeginNight(int day)
        {
            if (!_runStarted)
            {
                // 씬에서 바로 BeginNight부터 시작해도 터지지 않게 한 안전망(NightRun.EnsureRun과 같은 뜻).
                BeginRun();
            }

            if (_nightOpen)
            {
                Debug.LogWarning("[EncounterDirector] 이전 밤이 닫히지 않은 채 새 밤을 시작합니다. 이전 밤의 미관찰 장면을 먼저 이월합니다.");
                EndNight();
            }

            if (day < 1)
            {
                Debug.LogWarning("[EncounterDirector] 일차 " + day + "는 1보다 작습니다. 1일차로 취급합니다.");
                day = 1;
            }

            _day = day;
            _nightOpen = true;
            _laidToday = false;
            _creditsIssued = 0;
            _creditsSpent = 0;
            _inspectedToday.Clear();
            _todayScenes.Clear();

            // ① 그날 배분이 먼저다. 5일차를 넘기면 마지막 칸이 아니라 빈 목록이다 — 8장면이 이미 다 나갔기 때문이다.
            //
            // **이월보다 앞에 둔다.** 하루 배분은 최대 2장이고 상한은 3장이라, 이 순서면 그날 몫은
            // 언제나 자리를 얻는다. 반대로 두면 이월이 상한을 채워 그날 배분이 밀리고, 밀린 장면은
            // 다음 날 또 이월에 밀려 **회차가 끝날 때까지 한 번도 안 깔린다**.
            // 2026-09-22 실측: 아무것도 관찰하지 않는 플레이어에게 5일차 두 장면이 영영 나오지 않았다.
            // 「8장면 전소진」이 이 설계의 목적이므로 치명적이다. 되살리지 말 것: 「이월분 먼저」.
            //
            // 접근 단계가 이 순서에 걸려 있지 않다는 점이 중요하다 — <see cref="_placedStep"/>은 회차 내내
            // 누적되므로, 이월된 장면은 몇 번째로 들어오든 어제보다 가까운 자리에서 다시 시작한다.
            IReadOnlyList<string> plan = PlanFor(day);
            for (int i = 0; i < plan.Count; i++)
            {
                if (_todayScenes.Count < MaxScenesPerNight)
                {
                    Admit(plan[i]);
                }
                else
                {
                    // 상한에 걸려 오늘 못 들어간 장면은 **반드시 이월 목록에 넣는다.**
                    // 배분은 일차로만 조회되므로(PlanFor), 여기서 놓치면 다시 볼 기회가 영영 없다.
                    CarryOver(plan[i]);
                }
            }

            // ② 남은 자리에 이월분.
            for (int i = 0; i < _carriedOver.Count && _todayScenes.Count < MaxScenesPerNight; i++)
            {
                Admit(_carriedOver[i]);
            }

            // 오늘 들어간 것은 이월 목록에서 뺀다. 남은 것은 상한에 걸려 다시 넘어가는 장면이다.
            for (int i = _carriedOver.Count - 1; i >= 0; i--)
            {
                if (_todayScenes.Contains(_carriedOver[i]))
                {
                    _carriedOver.RemoveAt(i);
                }
            }

            BuildReport("밤 시작");
        }

        /// <summary>
        /// 판정 신호를 관찰한다. <b>부작용으로 상태가 전이된다</b>(장면 확정 · 모형 이동 · 문자 발송).
        /// <para>
        /// 읽는 신호는 셋이다.
        /// <list type="bullet">
        /// <item><see cref="SignalKind.InspectionCompleted"/> — 첫 한 건이 그날 장면을 확정하고,
        /// 서로 다른 <see cref="InspectionsPerMessage"/>곳마다 유도 문자 1통이 열린다.</item>
        /// <item><see cref="SignalKind.ModelObserved"/> — <c>TargetId</c>가 장면 ID다. 관찰 1초 완료.</item>
        /// <item><see cref="SignalKind.SpaceExited"/> — 퇴실. 관찰까지 끝났으면 소비, 아니면 접근 단계를 한 칸 올리고
        /// 게이트 장면이면 게이트를 세운다.</item>
        /// </list>
        /// 그 밖의 신호는 무시하지만 <b>버리지는 않는다</b> — 시야에 걸려 대기 중인 배치가 있으면
        /// 호출 끝에서 다시 시도하므로, <see cref="SignalKind.Tick"/>까지 그대로 넘겨 주면 대기가 빨리 풀린다.
        /// </para>
        /// </summary>
        public void Observe(in JudgeSignal signal)
        {
            if (!_nightOpen)
            {
                return;
            }

            switch (signal.Kind)
            {
                case SignalKind.InspectionCompleted:
                    OnInspectionCompleted(signal.Space);
                    break;

                case SignalKind.ModelObserved:
                    OnModelObserved(signal.TargetId);
                    break;

                case SignalKind.SpaceExited:
                    OnSpaceExited(signal.Space);
                    break;
            }

            // 대기 중인 배치를 매번 다시 시도한다. 「시야 안이면 대기했다가 나중에」의 그 「나중에」가 여기다.
            PumpPlacements();
        }

        /// <summary>
        /// 밤을 닫는다. 관찰·퇴실을 마치지 못한 장면은 <b>소비되지 않고 다음 날로 넘어간다</b>(3단계).
        /// <list type="bullet">
        /// <item>관찰만 하고 퇴실하지 않은 장면(<see cref="EncounterState.Observed"/>)도 <b>이월이다.</b>
        /// 「관찰 + 그 방문의 퇴실」이 한 묶음이라 반만 끝난 것은 안 끝난 것이다.</item>
        /// <item>이미 본 장면의 <b>미발송 유도 문자는 취소가 아니라 이월</b>한다(2026-09-21 재설계).
        /// 취소로 두면 자연 발견이 잦은 플레이어일수록 G를 덜 받게 되는데, G 7통은 장면 8개에 1:1로 묶여 있어
        /// 한 통이 취소될 때마다 그 회차의 문자 콘텐츠가 통째로 한 통씩 사라진다. 이월하면 7통이 전부 소진된다.
        /// <b>단, 이월된 G는 주인 장면이 이미 소비된 뒤라 사실상 재방문 안내로 읽힌다</b> — 문구를 그렇게 쓸 것.</item>
        /// </list>
        /// </summary>
        public void EndNight()
        {
            if (!_nightOpen)
            {
                return;
            }

            int carried = 0;

            for (int i = 0; i < _todayScenes.Count; i++)
            {
                string sceneId = _todayScenes[i];
                int index = IndexOf(sceneId);
                if (index < 0)
                {
                    continue;
                }

                if (_state[index] == EncounterState.Done)
                {
                    // 소비된 장면인데 G를 아직 못 보냈으면 문자만 따로 넘긴다.
                    QueueCarriedMessage(sceneId, index);
                    continue;
                }

                // 미관찰(또는 관찰만 하고 퇴실 전) — 장면 자체가 넘어간다. G는 장면을 따라가므로 큐에 넣지 않는다.
                //
                // **접근 단계(_placedStep)는 지우지 않는다.** 이월의 뜻이 「어제 못 본 장면이 오늘 더 가까이 온다」라서,
                // 매일 밤 1단계로 되돌리면 모형이 영원히 시야 주변부에만 머물고 3단계가 죽은 코드가 된다.
                // 게이트만 내린다 — 게이트는 「나가려는 순간」이라는 하룻밤짜리 맥락에 매여 있다.
                _state[index] = EncounterState.Idle;
                _placedGate[index] = false;
                _wantGate[index] = false;

                if (!_carriedOver.Contains(sceneId))
                {
                    _carriedOver.Add(sceneId);
                }

                carried++;
            }

            _nightOpen = false;
            BuildReport("밤 종료(이월 " + carried + ")");
        }

        /// <summary>
        /// 그 장면의 공간. 표에 없으면 <see cref="SpaceId.None"/>.
        /// <see cref="DayDirector"/>가 「그날 조우 공간」을 물을 때 쓴다(조우 공간 카드 1장 강제).
        /// </summary>
        public SpaceId SpaceOf(string sceneId)
        {
            EncounterTableSO.Scene scene = _table != null ? _table.Find(sceneId) : null;
            return scene != null ? scene.Space : SpaceId.None;
        }

        /// <summary>
        /// 오늘 깔려 있고 아직 그 회차에서 소비되지 않았는지(관찰만 하고 퇴실 전인 장면도 포함).
        /// <para>
        /// 「오늘 이 공간에 모형이 있는가」를 묻는 용도다. 덱 제약(S2 활성 중 S-B 금지)과
        /// 연출 쪽 분기가 이것을 읽는다.
        /// </para>
        /// </summary>
        public bool IsActive(string sceneId)
        {
            int index = IndexOf(sceneId);
            if (index < 0 || !_todayScenes.Contains(sceneId))
            {
                return false;
            }

            return _state[index] == EncounterState.Active || _state[index] == EncounterState.Observed;
        }

        /// <summary>그 장면의 현재 상태. 표에 없으면 <see cref="EncounterState.Idle"/>.</summary>
        public EncounterState StateOf(string sceneId)
        {
            int index = IndexOf(sceneId);
            return index >= 0 ? _state[index] : EncounterState.Idle;
        }

        /// <summary>
        /// 그 장면의 모형이 실제로 놓인 접근 단계(0 = 아직 안 놓음, 1~<see cref="ApproachStepCount"/>).
        /// 시야에 걸려 대기 중이면 <b>놓고 싶은 단계보다 작다.</b>
        /// </summary>
        public int StepOf(string sceneId)
        {
            int index = IndexOf(sceneId);
            return index >= 0 ? _placedStep[index] : 0;
        }

        /// <summary>그 장면의 퇴실 게이트가 실제로 세워졌는지.</summary>
        public bool IsExitGatePlaced(string sceneId)
        {
            int index = IndexOf(sceneId);
            return index >= 0 && _placedGate[index];
        }

        /// <summary>그 장면의 유도 문자를 회차에서 이미 보냈는지.</summary>
        public bool WasMessageSent(string sceneId)
        {
            int index = IndexOf(sceneId);
            return index >= 0 && _messageSent[index];
        }

        /// <summary>
        /// <b>이번 회차의</b> 일차별 배분. 회차마다 다르다(시나리오 기획서 v6 §4).
        /// 일차가 표를 넘어가면 빈 목록이다 — 8장면이 이미 전부 나갔기 때문이다.
        /// <para>
        /// <see cref="BeginRun"/> 전에는 비어 있다. 2026-09-22까지 있던 <c>static PlanFor</c>는
        /// 고정표를 돌려주던 것이라 이제 뜻이 없어 지웠다 — 부르던 자리는 이 인스턴스 메서드로 옮긴다.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> PlanFor(int day)
        {
            if (day < 1 || day > _runPlan.Length || _runPlan[day - 1] == null)
            {
                return EncounterTableSO.EmptyIds;
            }

            return _runPlan[day - 1];
        }

        /// <summary>그 일차에 깔리는 장면 수(이월 제외). 배분과 달리 회차가 바뀌어도 같다.</summary>
        public static int SlotsOf(int day)
        {
            if (day < 1 || day > SlotsPerDay.Length)
            {
                return 0;
            }

            return SlotsPerDay[day - 1];
        }

        /// <summary>조우 장면 ID인지.</summary>
        public static bool IsSceneId(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return false;
            }

            for (int i = 0; i < AllSceneIds.Length; i++)
            {
                if (AllSceneIds[i] == sceneId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>퇴실 게이트를 붙이는 장면인지(<see cref="ExitGateSceneIds"/> 셋).</summary>
        public static bool UsesExitGate(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return false;
            }

            for (int i = 0; i < ExitGateSceneIds.Length; i++)
            {
                if (ExitGateSceneIds[i] == sceneId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>발견형 장면인지(<see cref="DiscoverySceneIds"/> 셋). 두 선택 모두 델타 0이다.</summary>
        public static bool IsDiscovery(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return false;
            }

            for (int i = 0; i < DiscoverySceneIds.Length; i++)
            {
                if (DiscoverySceneIds[i] == sceneId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>로그·검증 메시지용 퇴실 게이트 장면 목록.</summary>
        public static string ExitGateSceneList()
        {
            return Join(ExitGateSceneIds);
        }

        /// <summary>로그·검증 메시지용 발견형 장면 목록.</summary>
        public static string DiscoverySceneList()
        {
            return Join(DiscoverySceneIds);
        }

        /// <summary>
        /// 그날 첫 일반 점검이 장면을 확정하고, 서로 다른 점검 <see cref="InspectionsPerMessage"/>곳마다 문자 1통이 열린다.
        /// <para>
        /// <b>같은 공간을 두 번 점검해도 한 곳이다.</b> 아니면 한 방에서 들락거리는 것만으로 7통이 다 나간다.
        /// </para>
        /// </summary>
        private void OnInspectionCompleted(SpaceId space)
        {
            if (space == SpaceId.None)
            {
                return;
            }

            bool isNewPlace = _inspectedToday.Add(space);

            if (!_laidToday)
            {
                LayToday();
            }

            if (isNewPlace)
            {
                int earned = _inspectedToday.Count / InspectionsPerMessage;
                if (earned > _creditsIssued)
                {
                    _creditsIssued = earned;
                }
            }

            TrySendMessages();
        }

        /// <summary>
        /// 그날 장면을 실제로 깐다(첫 일반 점검 직후). 접근 1단계를 「놓고 싶다」로 걸어 두고,
        /// 실제 배치는 <see cref="PumpPlacements"/>가 시야를 보고 한다.
        /// <para>N1은 여기서 판단한다 — 2일차이고, 회차 한도가 남았고, 오늘 역설을 아직 안 보냈을 때만.</para>
        /// </summary>
        private void LayToday()
        {
            _laidToday = true;

            for (int i = 0; i < _todayScenes.Count; i++)
            {
                int index = IndexOf(_todayScenes[i]);
                if (index >= 0 && _state[index] == EncounterState.Active)
                {
                    _wantStep[index] = 1;
                }
            }

            TrySendNotice();
            PumpPlacements();
            BuildReport("장면 확정");
        }

        /// <summary>
        /// 관찰 1초 완료. <b>아직 끝난 것이 아니다</b> — 퇴실이 남았다.
        /// 오늘 깔리지 않은 장면의 신호는 무시한다(전시형 모형을 본 것 등).
        /// </summary>
        private void OnModelObserved(string sceneId)
        {
            int index = IndexOf(sceneId);
            if (index < 0 || !_todayScenes.Contains(sceneId))
            {
                return;
            }

            if (_state[index] != EncounterState.Active)
            {
                return;
            }

            _state[index] = EncounterState.Observed;

            // 본 장면은 더 다가가지 않는다. 대기 중이던 배치도 전부 접는다.
            _wantStep[index] = _placedStep[index];
            _wantGate[index] = false;

            BuildReport(sceneId + " 관찰(퇴실 대기)");
        }

        /// <summary>
        /// 퇴실. 그 공간에 걸린 오늘 장면마다 다음 중 하나가 일어난다.
        /// <list type="bullet">
        /// <item><b>관찰까지 끝난 장면</b> → 소비. 「관찰 + 그 방문의 퇴실」이 완성됐다.</item>
        /// <item><b>못 본 장면</b> → 접근 단계를 한 칸 올린다. 방문이 헛되이 끝났다는 뜻이고,
        /// 다음 방문에는 더 정면인 지점에서 기다린다. 올리는 것은 <b>의도</b>일 뿐이고
        /// 실제 이동은 시야 밖일 때 일어난다.</item>
        /// <item><b>못 본 게이트 장면</b> → 게이트를 세운다(하룻밤 1회). <b>출구를 막지 않는다</b> —
        /// 그 자리를 등져야 나갈 뿐이고 모형 금지 반경은 그대로다.</item>
        /// </list>
        /// </summary>
        private void OnSpaceExited(SpaceId space)
        {
            if (space == SpaceId.None)
            {
                return;
            }

            for (int i = 0; i < _todayScenes.Count; i++)
            {
                string sceneId = _todayScenes[i];
                int index = IndexOf(sceneId);
                if (index < 0 || SpaceOf(sceneId) != space)
                {
                    continue;
                }

                if (_state[index] == EncounterState.Observed)
                {
                    _state[index] = EncounterState.Done;
                    BuildReport(sceneId + " 완료(관찰+퇴실)");
                    continue;
                }

                if (_state[index] != EncounterState.Active)
                {
                    continue;
                }

                if (_wantStep[index] < ApproachStepCount)
                {
                    _wantStep[index] = _wantStep[index] + 1;
                }

                if (UsesExitGate(sceneId) && !_placedGate[index])
                {
                    _wantGate[index] = true;
                }
            }
        }

        /// <summary>
        /// 대기 중인 배치를 시야를 보고 실행한다. <b>시야 안이면 아무것도 하지 않고 다음 호출을 기다린다.</b>
        /// <para>
        /// 게이트가 접근 단계보다 앞선다 — 게이트는 「지금 나가는 이 순간」에 뜻이 있고,
        /// 접근 단계는 「다음 방문」을 위한 것이라 하루 안에서 미뤄도 잃는 것이 없다.
        /// </para>
        /// </summary>
        private void PumpPlacements()
        {
            for (int i = 0; i < _todayScenes.Count; i++)
            {
                string sceneId = _todayScenes[i];
                int index = IndexOf(sceneId);
                if (index < 0 || _state[index] != EncounterState.Active)
                {
                    continue;
                }

                EncounterTableSO.Scene scene = _table != null ? _table.Find(sceneId) : null;
                if (scene == null)
                {
                    continue;
                }

                bool wantsGate = _wantGate[index] && !_placedGate[index];
                bool wantsStep = _wantStep[index] > _placedStep[index];

                if (!wantsGate && !wantsStep)
                {
                    continue;
                }

                if (IsVisibleSafe(sceneId))
                {
                    // 보고 있는 동안에는 절대 움직이지 않는다. 다음 신호에서 다시 본다.
                    continue;
                }

                if (wantsGate)
                {
                    if (Place(sceneId, scene.ExitGateTargetId, "퇴실 게이트"))
                    {
                        _placedGate[index] = true;
                        BuildReport(sceneId + " 퇴실 게이트");
                    }
                    else
                    {
                        // 지점이 없으면 영영 못 세운다. 매 신호마다 재시도해 로그를 도배하지 않도록 뜻을 접는다.
                        _wantGate[index] = false;
                    }

                    continue;
                }

                int step = _placedStep[index] + 1;
                if (Place(sceneId, scene.ApproachOf(step), "접근 " + step + "단계"))
                {
                    _placedStep[index] = step;
                    BuildReport(sceneId + " 접근 " + step + "단계");
                }
                else
                {
                    // 그 단계 지점이 비어 있다. 더 올려도 같은 일이 반복되므로 거기서 멈춘다.
                    _wantStep[index] = _placedStep[index];
                }
            }
        }

        /// <summary>모형을 실제로 놓는다. 지점이나 출력 통로가 없으면 경고를 남기고 false.</summary>
        private bool Place(string sceneId, string targetId, string what)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning("[EncounterDirector] " + sceneId + "의 " + what + " 지점이 표에 없습니다. 이 배치는 건너뜁니다.");
                return false;
            }

            if (PlaceModel == null)
            {
                Debug.LogWarning("[EncounterDirector] PlaceModel이 주입되지 않아 " + sceneId + "의 " + what + "를 실행할 수 없습니다. " +
                                 "씬 쪽에서 (장면 ID, 대상 ID)를 받아 모형을 옮기는 통로를 연결하십시오.");
                return false;
            }

            try
            {
                PlaceModel(sceneId, targetId);
            }
            catch (Exception e)
            {
                // 씬 쪽이 터져도 조우 상태 기계는 계속 돈다(EventBus의 개별 try/catch와 같은 이유).
                Debug.LogException(e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 지금 그 장면의 모형이 시야 안인가. 주입이 없으면 「안 보임」으로 보고 경고를 한 번 남긴다.
        /// 델리게이트가 터지면 <b>보인다</b>고 본다 — 판단할 수 없을 때 안전한 쪽은 움직이지 않는 쪽이다.
        /// </summary>
        private bool IsVisibleSafe(string sceneId)
        {
            if (IsVisible == null)
            {
                if (!_visibleWarned)
                {
                    _visibleWarned = true;
                    Debug.LogWarning("[EncounterDirector] IsVisible이 주입되지 않았습니다. 「항상 안 보임」으로 보고 진행합니다 — " +
                                     "플레이어가 보는 앞에서 모형이 움직일 수 있습니다. 씬 쪽에서 시야 판정을 연결하십시오.");
                }

                return false;
            }

            try
            {
                return IsVisible(sceneId);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return true;
            }
        }

        /// <summary>
        /// 열린 크레딧만큼 유도 문자를 보낸다. 우선순위는 <b>오늘 깔린 미관찰 장면 → 이월된 문자</b>다.
        /// <para>
        /// 오늘 것이 먼저인 이유: G는 「지금 가 보라」는 안내다. 이월분을 먼저 보내면 오늘 실제로 깔려 있는
        /// 장면을 놔두고 이미 끝난 장면으로 부르게 된다.
        /// </para>
        /// </summary>
        private void TrySendMessages()
        {
            while (_creditsSpent < _creditsIssued)
            {
                string sceneId = PickTodayMessage();
                bool fromCarry = false;

                if (string.IsNullOrEmpty(sceneId) && _carriedMessages.Count > 0)
                {
                    sceneId = _carriedMessages[0];
                    fromCarry = true;
                }

                if (string.IsNullOrEmpty(sceneId))
                {
                    // 보낼 것이 없으면 크레딧을 그대로 둔다. 뒤에 장면이 깔리면 그때 쓴다.
                    return;
                }

                if (!SendSceneMessage(sceneId))
                {
                    // 문자 데이터가 없는 장면이다. 다시 고르면 같은 것이 나오므로 목록에서 빼고 크레딧은 남긴다.
                    int index = IndexOf(sceneId);
                    if (index >= 0)
                    {
                        _messageSent[index] = true;
                    }

                    if (fromCarry)
                    {
                        _carriedMessages.RemoveAt(0);
                    }

                    continue;
                }

                if (fromCarry)
                {
                    _carriedMessages.RemoveAt(0);
                }

                _creditsSpent++;
            }
        }

        /// <summary>오늘 깔린 장면 중 아직 관찰되지 않았고 유도 문자를 안 보낸 첫 장면. 없으면 빈 문자열.</summary>
        private string PickTodayMessage()
        {
            for (int i = 0; i < _todayScenes.Count; i++)
            {
                string sceneId = _todayScenes[i];
                int index = IndexOf(sceneId);
                if (index < 0 || _messageSent[index])
                {
                    continue;
                }

                if (_state[index] != EncounterState.Active)
                {
                    continue;
                }

                if (sceneId == FirstDaySceneId)
                {
                    // S-A에는 유도 문자가 없다. 1일차 고정이라 안내할 필요가 없기 때문이다.
                    continue;
                }

                return sceneId;
            }

            return string.Empty;
        }

        /// <summary>장면의 유도 문자를 보낸다. 표에 문자 데이터가 없으면 false.</summary>
        private bool SendSceneMessage(string sceneId)
        {
            int index = IndexOf(sceneId);
            EncounterTableSO.Scene scene = _table != null ? _table.Find(sceneId) : null;
            if (index < 0 || scene == null || string.IsNullOrEmpty(scene.MessageId))
            {
                return false;
            }

            _messageSent[index] = true;
            Send(scene.MessageId, scene.Space, scene.MessageText);
            BuildReport("문자 " + scene.MessageId);
            return true;
        }

        /// <summary>
        /// 재방문 문자(N1). <b>2일차 고정 · 회차 1회 · 역설을 보낸 날에는 미발송.</b>
        /// <para>
        /// 2일차인 이유: 1일차는 S-A 하나로 「모형이라는 것이 있다」를 가르치는 밤이라 다른 문자를 섞지 않는다.
        /// 3일차부터는 하루 2장면이라 G 두 통이 나가므로 태블릿이 이미 바쁘다. 빈 자리는 2일차뿐이다.
        /// </para>
        /// </summary>
        private void TrySendNotice()
        {
            if (_day != NoticeDay || _noticeSentCount >= NoticeLimitPerRun || _table == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_table.NoticeMessageId) || string.IsNullOrEmpty(_table.NoticeText))
            {
                return;
            }

            if (ParadoxSentTodaySafe())
            {
                return;
            }

            _noticeSentCount++;
            Send(_table.NoticeMessageId, _table.NoticeSpace, _table.NoticeText);
            BuildReport("문자 " + _table.NoticeMessageId);
        }

        /// <summary>오늘 역설 문자가 이미 나갔는지. 주입이 없으면 「안 나갔다」로 본다(N1을 막지 않는다).</summary>
        private bool ParadoxSentTodaySafe()
        {
            if (ParadoxSentToday == null)
            {
                return false;
            }

            try
            {
                return ParadoxSentToday();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// 문자를 태블릿으로 보낸다. <b>새 이벤트를 만들지 않고</b> <see cref="EventBus.MessageSent"/>를 그대로 쓴다 —
        /// 태블릿 메시지 탭은 문자가 G인지 P인지 알 필요가 없고, 통로를 둘로 나누면 구독자가 둘 다 달아야 한다.
        /// <para>
        /// 짝 수칙이 없으므로 <see cref="ParadoxMessage.CardId"/>는 <b>비운다</b>. 여기에 장면 ID를 넣으면
        /// 결산의 3구분 표기(<see cref="DutyLogEntry"/>)가 카드 ID로 착각할 자리가 생긴다.
        /// </para>
        /// </summary>
        private void Send(string messageId, SpaceId space, string text)
        {
            EventBus.RaiseMessageSent(new ParadoxMessage(messageId, string.Empty, space, text, CurrentMinute()));
        }

        private int CurrentMinute()
        {
            if (ClockMinutes == null)
            {
                return -1;
            }

            try
            {
                return ClockMinutes();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return -1;
            }
        }

        /// <summary>
        /// 오늘 목록에 장면을 넣는다. 이미 소비됐거나 표에 없으면 넣지 않는다.
        /// </summary>
        private void Admit(string sceneId)
        {
            int index = IndexOf(sceneId);
            if (index < 0)
            {
                Debug.LogWarning("[EncounterDirector] 장면 " + sceneId + "이(가) 조우 표에 없습니다. 오늘 배정에서 뺍니다.");
                return;
            }

            if (_state[index] == EncounterState.Done || _todayScenes.Contains(sceneId))
            {
                return;
            }

            _state[index] = EncounterState.Active;

            // 접근 단계는 **회차 내내 누적**한다. 이월된 장면이 어제와 같은 자리에서 다시 시작하면
            // 「어제 못 본 장면이 오늘 더 가까이 온다」는 3단계의 뜻이 사라진다.
            // _placedStep은 그대로 두고, 아직 안 놓은 다음 단계만 다시 걸어 둔다.
            _wantStep[index] = _placedStep[index];

            // 게이트는 하룻밤 단위다. 어제 세운 게이트가 오늘까지 서 있으면 「나가려는 순간」이라는 맥락이 없다.
            _placedGate[index] = false;
            _wantGate[index] = false;
            _todayScenes.Add(sceneId);
        }

        /// <summary>
        /// 장면을 다음 날로 넘긴다. 이미 소비됐거나 이미 이월 목록에 있으면 아무것도 하지 않는다.
        /// <para>
        /// <b>접근 단계는 지우지 않는다</b> — 이월의 뜻이 「더 가까이 온다」이기 때문이다(<see cref="Admit"/>).
        /// </para>
        /// </summary>
        private void CarryOver(string sceneId)
        {
            int index = IndexOf(sceneId);
            if (index < 0 || _state[index] == EncounterState.Done)
            {
                return;
            }

            if (_carriedOver.Contains(sceneId) || _todayScenes.Contains(sceneId))
            {
                return;
            }

            _carriedOver.Add(sceneId);
        }

        /// <summary>소비된 장면의 미발송 유도 문자를 이월 큐에 넣는다.</summary>
        private void QueueCarriedMessage(string sceneId, int index)
        {
            if (_messageSent[index] || sceneId == FirstDaySceneId)
            {
                return;
            }

            EncounterTableSO.Scene scene = _table != null ? _table.Find(sceneId) : null;
            if (scene == null || string.IsNullOrEmpty(scene.MessageId))
            {
                return;
            }

            if (!_carriedMessages.Contains(sceneId))
            {
                _carriedMessages.Add(sceneId);
            }
        }

        private int IndexOf(string sceneId)
        {
            return _table != null ? _table.IndexOf(sceneId) : -1;
        }

        /// <summary>제자리 셔플(Fisher-Yates). 하루 안의 장면 순서에만 쓴다.</summary>
        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary>디버그 한 줄을 다시 만든다. 상태가 바뀔 때마다 부른다.</summary>
        private void BuildReport(string what)
        {
            _report.Length = 0;
            _report.Append(_day);
            _report.Append("일차 ");
            _report.Append(what);
            _report.Append(" | 오늘 ");

            if (_todayScenes.Count == 0)
            {
                _report.Append("없음");
            }
            else
            {
                for (int i = 0; i < _todayScenes.Count; i++)
                {
                    if (i > 0)
                    {
                        _report.Append(' ');
                    }

                    string sceneId = _todayScenes[i];
                    int index = IndexOf(sceneId);
                    _report.Append(sceneId);
                    _report.Append('(');
                    _report.Append(index >= 0 ? StateNameOf(_state[index]) : "없음");

                    if (index >= 0)
                    {
                        _report.Append(" 접근");
                        _report.Append(_placedStep[index]);

                        if (_wantStep[index] > _placedStep[index])
                        {
                            _report.Append("→");
                            _report.Append(_wantStep[index]);
                            _report.Append("대기");
                        }

                        if (_placedGate[index])
                        {
                            _report.Append(" 게이트");
                        }
                        else if (_wantGate[index])
                        {
                            _report.Append(" 게이트대기");
                        }
                    }

                    _report.Append(')');
                }
            }

            _report.Append(" | 점검 ");
            _report.Append(_inspectedToday.Count);
            _report.Append("곳 문자 ");
            _report.Append(_creditsSpent);
            _report.Append('/');
            _report.Append(_creditsIssued);

            if (_carriedMessages.Count > 0)
            {
                _report.Append(" 이월문자 ");
                _report.Append(Join(_carriedMessages));
            }

            if (_carriedOver.Count > 0)
            {
                _report.Append(" | 이월 ");
                _report.Append(Join(_carriedOver));
            }

            _lastReport = _report.ToString();
        }

        private static string StateNameOf(EncounterState state)
        {
            switch (state)
            {
                case EncounterState.Active:
                    return "대기";
                case EncounterState.Observed:
                    return "관찰";
                case EncounterState.Done:
                    return "완료";
                default:
                    return "미배정";
            }
        }

        private static string Join(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return "없음";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('·');
                }

                sb.Append(ids[i]);
            }

            return sb.ToString();
        }
    }
}

# CLAUDE.md

이 파일은 이 저장소에서 코드를 다루는 Claude 세션을 위한 안내서입니다.
**답변·문서·코드 주석은 모두 한국어로 작성합니다.**

> **개정: 2026-10-03(29차). 축별 붙잡힘 장면도 프리팹으로 끼우게(민 요청: 「엔딩」은 축별 붙잡힘 장면까지 포함 — 피날레 몹처럼). 씬은 고치지 않았다. EditMode 264/264, PlayScene에서 임시 장면 프리팹(장면 Animator + 몹 Animator 2개, CameraMount 이동·시야각 60→30, 이벤트 CaptureSound·CaptureCue·CaptureDone)을 배치 칸에 넣어 미리 보기(카메라가 마운트를 따름·장면 층만 그림·CaptureDone 12.00초) → 실제 붙잡힘(CaptureDone 2.00초 → YOU DIED 카드 → 재시작 k=1) → 빈 청각 칸 기본 얼굴(0.30초, k=2), 리스너 정지 중 장면 소리 재생까지 실측 → 임시 자산 삭제, 에러 0.**
> ⓐ **끼우는 곳 = 축마다 한 칸**: `Resources/CaptureCast.asset`(`CaptureCastSO`, Flow/Capture) — 청각·조도·배치의 **Prefab 칸**. 비우면 27차 기본 장면(어둠 속 대역 얼굴). 칸마다 정적 시간 · 돌아보기 · 손전등 끔 · 어둠 속 장면만(darkWorld) · 장면 시작 소리(`capture.auditory`/`illuminance`/`layout` — 음원이 오면 이 이름으로) · 최대 길이 · 반복 배속 · 기본 장면용 얼굴 ID·시간·끌려감 거리. 앞뒤 틀(소리 끊김·암전·카드·재시작·출근 자리)은 그대로.
> ⓑ **장면 프리팹 규약**: 원점 = 플레이어 발밑(`DirectionStage.FloorBelow`), +Z = 플레이어 시선(돌아보기면 돌아본 뒤). 정적 동안 꺼진 채 세웠다가 암전이 걷히는 순간 켠다 → Animator들이 기본 상태부터 돈다(트리거 불필요). 자식 `CameraMount`가 있으면 그동안 메인 카메라가 `LateUpdate`에서 그 위치·회전을 따르고, 거기 붙은(꺼 둔) Camera의 시야각도 따른다. 끝 = `CaptureDone` 이벤트(있으면 그것만 기다림) → 없으면 모든 Animator가 반복 아닌 상태를 끝냄 → 최대 길이. 이벤트 `CaptureSound(이름)`·`CaptureFlashlight(0/1)`·`CaptureCue(문자열)`(`CaptureDirector.SceneCued`). 받이 `CaptureAnimEvents`는 런타임에 붙인다. 콜라이더 끔·Rigidbody 키네마틱·Animator `AlwaysAnimate`, 두 번째부터 Animator 속도 ×반복 배속, 세 번째부터 아무 키로 건너뜀.
> ⓒ **소리 끊김을 리스너 정지로**: `AudioListener.volume = 0` → `AudioListener.pause = true`. 장면 프리팹의 AudioSource와 장면 소리(`ignoreListenerPause`)만 정적 속에서 들린다. 결근·카드 뒤·미리 보기 끝·컴포넌트 꺼짐에서 풀린다.
> ⓓ **미리 보기·검사**: `CaptureDirector.Preview(axis, 회차)` — 재시작·카드 없이 장면만(끝나면 방향 되돌림), F3 콘솔 「조우」 탭 [붙잡힘 장면 미리 보기 청각·조도·배치] + [회차 바꾸기]. 메뉴 「야간근무 ▸ 연출 ▸ 붙잡힘 연출 검사」·연출표 인스펙터 [검사](몸 위치·CameraMount 높이·Animator 시작 상태·끝 이벤트·최대 길이 초과·손전등·소리·조명). 코드: `CaptureDirector`를 partial로 나눠 장면 부분은 `CaptureDirector.Scene.cs`.
> **되살리지 마십시오:** 「붙잡힘 장면을 코드에 축별로 하드코딩」(연출표 칸 → 비면 기본 장면) · 「소리 끊김 = AudioListener.volume 0」(장면 소리까지 죽는다 — 리스너 정지 + ignoreListenerPause) · 「장면 카메라 따라가기를 코루틴에서」(애니메이션 평가 전이라 한 프레임 늦다 — LateUpdate).

> **개정: 2026-10-03(28차). 피날레 몹을 「나중에 끼우기만 하면 되게」(민 요청: 엔딩 몹은 팀원이 애니메이션까지 만들어 넣을 예정). 씬 작업(민 승인): `DirectionAnchors/stage.finale.seat` 자리 하나 추가. EditMode 264/264, PlayScene에서 임시 애니메이션 프리팹(트리거 2·상태 3·이벤트 3종)을 배역표에 넣어 나타남(이벤트 3.03초)·두드림(FinaleKnock마다 소리)·봤다 반응(상태 빠져나감 2.92초)·사라짐(이벤트 2.57초, 숨김)·FinaleCue("smile") 실측 → 임시 자산 삭제, 빈 칸(대역)으로 두드림 타이머·사라짐·재시작 때 거둠까지 실측, 에러 0.**
> ⓐ **끼우는 곳은 한 칸**: `Resources/FinaleCast.asset`(`FinaleCastSO`, Flow/Finale) — 배역 둘(`FinaleRole.WindowMan` 창밖 웃는 정장 남자 · `SeatFigure` 꺼진 CRT에 비친 내 자리 무언가)의 **Prefab 칸**. 비우면 대역 `mob.finale`, 「내 자리 무언가」가 비면 창밖 남자 프리팹을 쓴다. 칸마다 자리(`stage.finale.window`·`stage.finale.seat`)·응시 판정 ID(`rule.K4.window`)·응시 상자 여부·비트별 Animator 이름/최대 시간 덮어쓰기.
> ⓑ **프리팹 규약**: 피벗 = 발바닥 중앙 · 앞 = +Z · 키 1.2~2.6m · Rigidbody·스크립트 불필요(단단한 콜라이더는 세울 때 끔). Animator의 **트리거 또는 상태 이름** = 비트(`FinaleBeat`: Idle·Appear·Knock·Seen·Vanish·Stand) — 트리거면 SetTrigger, 상태만 있으면 CrossFade, 없으면 그 비트는 건너뜀. 한 번짜리(Appear·Seen·Vanish)의 끝 = 애니메이션 이벤트 `FinaleBeatDone` → 상태 끝/빠져나감 → 최대 시간(기본 8초) 중 먼저. 두드림 소리 = `FinaleKnock` 이벤트(없으면 2.4초마다 소리만, 소리는 `finale.knock` 없으면 L5와 같은 `E.SuitMan.confront`). 그 밖의 타이밍 = `FinaleCue(문자열)`. 이벤트 받이(`FinaleAnimEvents`)는 런타임에 붙인다. Animator는 `AlwaysAnimate`로 바꾼다(창밖·등 뒤에서 멈추면 비트 끝을 못 잡음). `Aim` 자식이 있으면 응시·조준점.
> ⓒ **부르는 쪽**: `DirectionStage.StageFinale(role)`(무대 목록에 올림 → 밤 재시작·하루 끝·무대 정리 때 거둠) · `FinaleOf` · `ClearFinale`, 몹은 `FinaleMob.Play(beat)` / `PlayAndWait(beat)` · 이벤트 `BeatFinished`·`Knocked`·`Cued` · `Supports(beat)`·`HasArt`·`LastLog`. 11단계 피날레 흐름은 이것만 부르면 되고, 팀원 프리팹이 들어와도 부르는 코드는 그대로다. 옛 미리 보기(`ToggleFinaleWindowMan`)도 이 입구로 바꿨다(나타남 → 두드림).
> ⓓ **검사·시험**: 메뉴 「야간근무 ▸ 연출 ▸ 피날레 몹 검사」·배역표 인스펙터 [검사] — 피벗·키·몸 중심·Rigidbody·Animator·비트별 트리거/상태·클립·끝 이벤트·두드림 이벤트를 한 장으로(고치지는 않음, 키·피벗은 프리팹 기본 자세 기준). 플레이 중 F3 콘솔 「조우」 탭에 배역마다 [세우기/치우기]·비트 버튼. `StandInFactory.Dress`(조준점·응시 상자·판정 ID)를 대역과 함께 쓴다. `DirectionStage.PlaySound`는 `internal`(성공 여부 반환).
> ⓔ **자리**: `stage.finale.seat` = 경비실 CRT 화면 중심에서 화면 바깥쪽 1.05m 바닥(32.41, 1.50, 46.39), CRT를 본다 — CCTV 보는 자리 바로 뒤. 「고정 몹 자리 놓기」 메뉴에 ⑦로 넣었다. 꺼진 CRT에 비치게 하는 렌더링은 11단계.
> **되살리지 마십시오:** 「팀원 몹을 Resources/StandIns 대역 프리팹으로 교체」(빌더가 다시 만들면 사라짐 — 배역표 칸에 넣는다) · 「피날레 몹 비트를 코드에서 클립 이름·시간으로 하드코딩」(이름 = 트리거/상태, 끝 = 이벤트) · 「팀원 프리팹에 우리 스크립트를 넣게 함」(받이는 런타임에 붙인다).

> **개정: 2026-10-03(27차). 8단계 — 붙잡힘 공용 연출 틀·얼굴 컷·재시작 카드(`Flow/Capture/CaptureDirector`, 근무 씬 자동 설치). 씬은 고치지 않았다. EditMode 264/264(붙잡힘 통로 3개 추가), PlayScene에서 청각·조도·배치 붙잡힘 → 얼굴 → 카드 「1일차 · 00:00 / 귀·눈·발자국 / 마지막 서명: 없음 → 00:00」 → 출근 자리 → 재시작 5회, 6번째 → 결근 → 결과창까지 실측, 에러 0.**
> ⓐ **통로**: 코어에 `EventBus.Captured`(붙잡힘) 신설. `FearAxisSystem`은 `RaiseCapturedOrCritical` — **구독자가 있으면 `Captured`만, 없으면 옛 `AxisCritical`**. 근무 씬에서는 `CaptureDirector`가 구독하므로 김진선님 `PlayResultRouter`의 사망 화면(HUD_Death)이 **뜨지 않는다**(김진선님 코드는 고치지 않음 — 김진선님 `DevModePanel`이 `AxisCritical`을 직접 올리는 경로는 그대로 사망 화면).
> ⓑ **순서(실제 시간, 시계 `Hold`)**: 이동 끔 → 소리 끊김(`AudioListener.volume` 0, 조도는 손전등 끔) → 0.5초 암전(청각·배치는 그동안 180° 돌아봄) → **어둠 속 얼굴 0.3초**(청각 `mob.boy` · 조도 `mob.dummy.stand`(다시 켜진 손전등이 비춤) · 배치 `mob.tree`(카메라가 가지 쪽으로 0.3m 끌려 들어감)). 같은 축 두 번째는 0.15초, 세 번째부터 아무 키로 건너뜀 → 암전 → 게임 시계를 시 단위로 되돌린 뒤 `NightRun.RestartAfterCapture()` → 카드(그 밤 그 축을 올린 수칙·점검 항목 **이름만** — 무엇을 했는지·어떻게 했어야 하는지는 적지 않음, 최소 1.5초·8초 자동) → 출근 자리(첫 프레임의 플레이어 위치·방향)로 순간이동 → 페이드 인. 결근이면 카드 없이 암전 유지 → `DayEnded` → 결과창.
> ⓒ **얼굴을 그리는 법**: 얼굴만 이름 없는 사용자 층(뒤에서부터 첫 빈 층, 지금 31)에 두고 그동안 **메인 카메라 = 그 층만 + 검은 배경**, 뷰모델 카메라(태블릿) 등 다른 카메라 끔, 화면 UI(조작 안내·시계) 캔버스 끔 — 좁은 방에서 벽·책이 얼굴을 가렸던 것(실측) 때문. 팔·가지가 근평면을 뚫지 않게 조준점 앞으로 튀어나온 만큼 띄우고, 그 대신 시야각을 좁혀 얼굴(세로 0.4m)이 화면을 채운다. 끝나면 전부 되돌린다.
> ⓓ **아직 안 하는 것**: 씬 재로딩(문·소품은 그대로, 플레이어만 출근 자리로) · 게임 시계 표시는 시 단위(`GameTime.JumpToHour`만 있음 — 판정 시계는 구동기가 분 단위로 맞춤) · 축별 소리(속삭임·에코)는 음원 대기. 에디터에서는 대역의 **첫 등장 한 번**이 안 보일 수 있다(비동기 셰이더 컴파일 — 빌드에는 없음).
> ⓔ **카드에 「YOU DIED」(같은 날 민 요청: 축 상징 대신 사망 UI)**: `Resources/CaptureCardLook.asset`(`CaptureCardLook`)이 디자인 사망 화면 프리팹 `0. Main/01 Scene/HUD_Death_Design`을 **참조만** 한다. 카드를 띄울 때마다 그 안의 배경(`Backgrond`, ui_bg_kill)·로고(`Txt_Death`, ui_logo_dead — 프리팹의 DOTween 3초 페이드 인 그대로)만 카드 뒤에 복제하고, 카드를 닫으면 지운다. 버튼·`HUDActions`(메인으로 이동)는 쓰지 않는다. 프리팹이 비거나 조각 이름이 바뀌면 옛 축 상징(귀·눈·발자국)으로 돌아간다. 김진선님 라우터가 쓰는 `HUD_Death`(흰 글씨)는 건드리지 않았다.
> **되살리지 마십시오:** 「붙잡힘 = 게임 오버(사망 화면 → 메인)」 · 「재시작 카드에 사망 화면 프리팹을 통째로 띄움」(버튼이 메인으로 보낸다 — 조각만 복제) · 「붙잡힘 카드에 무엇을 했는지·정답 행동을 적음」 · 「얼굴을 세상과 함께 그림」(벽에 가려짐) · 「얼굴을 근평면까지 붙여 화면을 채움」(팔·가지가 잘림 — 시야각으로 채운다).

> **개정: 2026-10-03(26차). 새 편성에서 안 쓰는 옛 시스템 전부 폐기(민 지시: 「진짜 안 쓰이는지·활용 방도를 충분히 확인하고 삭제」). 씬 작업(민 승인): PlayScene에서 옛 대상 표식·빈 앵커·`JudgeLights`·옛 구역 2개 제거. EditMode 261/261, PlayScene에서 1일차 → G1 위반 → 근무 종료 → 결과창 근무일지 5줄(G1 어김) 실측, 에러 0.**
> ⓐ **확인 방법**: 게임은 늘 `ProgramEnabled = true`(구동기)라, 그 반대편 분기와 그 분기만 쓰는 코드를 「새 편성에서 안 쓰임」으로 봤다. 타입 참조 그래프(전 .cs)·씬/프리팹/에셋의 스크립트 GUID·김진선님 코드 참조·`Resources` 로드 문자열을 모두 대조했다.
> ⓑ **지운 것**: 옛 판정 책 `RuleBook`·`RuleWatcher`·`RuleReferenceCheck`·`CardState`(+`RuleResult`·`SettleAt`)·`JudgeWorld`·조건 9종(`Rules/Conditions/`)·`SubclassSelectorDrawer` · 옛 조우 `EncounterDirector`·`EncounterTableSO`(+에셋)·`EncounterTableBuilder`·`EncounterStager`·`DayBriefText` · 옛 역설 `ParadoxDirector`(문자 형식 `ParadoxMessage`만 남김) · 옛 단서 큐 `AnomalyCueDirector`·`CueBindingTableSO`·`SpaceAnomalyTableSO`(+에셋·빌더 2개) · 옛 조도 `BandTableSO`(+에셋·생성기)·`ISpacePresenter` · `IDocumentView`·`DocumentTypes`(§0 조항)·`EventBus.DayStarted`(보내는 곳 0) · `Bands.RedThreshold` · `ProximityProbe`(새 판정기는 `PlayerPose`로 거리를 잰다 — 게임에선 보낼 대상이 0이었다) · 시험 리그(`NightRunDebugPanel`·`AxisTest*`·`TestSceneBuilder`·`TestLightRigSetup`·`DebugAxisDriver`·`_Test_AxisRig` 씬 2개) · `SceneTargetValidator` · `NightRun`의 `DeckOverride`·`RegisteredTargets`·`TargetsInUse`·`CurrentBook`·`WasVisitedToday`·`JudgeWhileTabOpen`(게임은 늘 켜짐) · `PlayerSensors.TabOpen`(늘 거짓) · `BandResolver` 공간 보류(옛 카드만 걸었다) · `SpaceZones`의 옛 점검 상자·통행 구역 · 신호 9종(문 닫힘 완료·통행 완료·옛 점검 완료·단서 전달/식별·근접 샘플·모형 관찰·밤 시작/종료 — 번호 재사용 금지) · 테스트(옛 판정 책·조건·조우·역설·RuleSO 7개 파일/클래스).
> ⓒ **고친 버그(지우다 발견)**: 결과창 「금일 근무 지침」이 **비어 있었다**(근무일지를 옛 덱으로 만들었음) → 그날 편성 새 수칙 한 줄씩, 어긴 수칙에 빨간 줄(`RuleVerdict`). 위반 시각도 옛 결과에서만 모았다 → 새 수칙 위반·G2 환청·T4 역보고 위반에서. 결과창 「점검」 칸이 옛 「다섯 공간 점검 상자」였다 → 점검표 보고 수 / 항목 수. 문 발신기 「밤 시작 때 재검사」가 한 번도 안 돌았다(보내는 이벤트 없음) → 구동기가 밤 열기 직전에 부른다. 경비실 전화가 태블릿을 든 채로도 눌렸다 → 막음.
> ⓓ **남긴 것과 이유**: `RuleSO`(태블릿 표시 4필드 — 김진선님 `TabletDocument`·`GuardRoomLoiterTrigger`가 읽음) · `NightDeckTableSO`(빈 폴백) · `SpaceLights`(빈 껍데기 — 김진선님 `PlayScene_test`·`_Recovery` 씬에 붙어 있어 지우면 Missing Script) · 문 `JudgeTarget` ID(문 발신기·문 정책이 씀) · `ParadoxMessage`·`EventBus.MessageSent`(10단계 새 역설 통로) · `JudgeTargetRegistry`(새 판정기 기준점·센서).
> **되살리지 마십시오:** 「옛 판정 책(RuleBook)·조건 에셋으로 수칙 판정」 · 「옛 조우 8장면(EncounterDirector)」 · 「ParadoxDirector·짝 역설 23쌍」 · 「AnomalyCueDirector·이상현상/바인딩 표」 · 「SpaceLights·BandTable 조도」 · 「DebugAxisDriver·_Test_AxisRig」 · 「근무일지 = 옛 덱」 · 「미방문 공간에 빨간 줄」 · 「점검 = 공간 점검 상자 1초 체류」 · 「Tab 중 판정 정지(JudgeWhileTabOpen)」 · 「EventBus.DayStarted·§0 조항」.

> **개정: 2026-10-03(25차). 옛 24장 카드 폐기(민 지시: 「버그가 생길 수 있으니 안 쓰이는 24가지는 폐기」). 씬은 고치지 않았다. EditMode 353/353(옛 카드 테스트 199개 함께 삭제), PlayScene 재생 확인(1일차 태블릿 = 새 수칙 5 + 점검 5, 에러 0).**
> ⓐ **지운 것**: 에셋 `ScriptableObjects/Rules/`(H·C·S·T 1~6 24개, 폴더째) · 코드 `DayDirector`(하루 6장 배정) · `CardScenarios` · 에디터 `CorridorCardBuilder`·`RoomCardBuilder`·`RuleCardValidator`·`ParadoxTextBuilder` · 테스트 `Classroom/Corridor/Science/ToiletCardAssetTests`·`CardScenarioTests`·`ParadoxDataTests`·`DayDirectorTests`(BalanceRedesignTests 안)·`AssetBuilderDriftTests`·축 쿼터/죽은 카드/조우 묶인 카드 불변식 3개 · 판정 디버그 패널(`NightRunDebugPanel`)의 「카드 시험」 탭 내용(탭 자리엔 폐기 안내만).
> ⓑ `NightRun.LoadDeck`은 `DeckOverride`가 없으면 **항상 빈 덱** — 새 편성을 꺼도 옛 판정 책은 비어 있다. `CollectPool`·`_director`·조우 공간 델리게이트(`EncounterSpacesToday`·`IsEncounterActive`)를 지웠다.
> ⓒ **남긴 것**: `RuleSO` 타입(태블릿 표시용 `DisplayDeck`·테스트가 씀) · `RuleBook` 엔진(`DeckOverride` 시험) · `NightDeckTableSO` 타입과 `Resources/NightDeckTable.asset`(**비움** — 김진선님 `TabletDocument`가 밤 시작 전 폴백으로 읽는다. 전에는 밤 시작 전 태블릿에 옛 카드 문구가 뜰 수 있었다) · `ParadoxDirector` · 옛 조우 연출기 `EncounterDirector`(8장면, 새 편성에서 쉼)·`AnomalyCueDirector` · 일차 하한·역설 최소 신뢰 검사(`DesignDriftTests.cs`의 `DesignInvariantTests`로 이름 바꿈)와 `ClaudeMdDriftTests`.
> **되살리지 마십시오:** 「옛 24장 RuleSO 에셋·편성표 카드 풀」 · 「DayDirector 하루 6장 배정(축 쿼터 2·2·2)」 · 「CardScenarios 카드 시나리오」 · 「카드 빌더(Corridor/RoomCardBuilder)가 카드 구현값의 정본」 · 「NightDeckTable에 카드를 다시 채움」.

> **개정: 2026-10-03(24차). 7단계 마무리 — 조도축 맵 변화(공간별 톤 Volume + 기획서 표대로 소등). 씬은 고치지 않았다(전부 런타임 생성). EditMode 552/552, PlayScene에서 조도 0 → 55(구간 2) → 95(구간 4)로 올려 복도 호박색·비네팅 → 좌측 복도 끝 2개 소등·화장실 붉은 조명 → 채도 감소·붉은 잔광·좌측 4개·과학실 앞 복도 소등까지 실측, 에러 0.**
> ⓐ **`IlluminanceMap`**(Flow/Presentation, 근무 씬에 자동 설치): 코어 `NightRun.ShownBand(space, axis)`(새 — 공간 보류 반영, 순찰 공간이 아니면 축 전체 연출 구간)를 0.25초마다 읽어 그린다. 판정은 모른다.
> ⓑ **톤**: 공간마다 런타임 Volume(1-3 교실·과학실·화장실·도서관·경비실·복도, 전역, **플레이어 카메라 볼륨 레이어 = 8**, 우선순위 5 — 실내 보정 `PostProcessing_interior` 1 위, 조우 화면 효과 20~23 아래). 플레이어 눈이 든 방 상자의 것만 무게 1(0.8초), 방 밖(좌측 복도 포함)은 복도. 값은 `Resources/IlluminanceTone.asset`(`IlluminanceToneSO`, 구간 0~4 다섯 줄 — 기획·아트 튜닝용): White Balance 색온도·틴트, 채도(실내 보정에 **더하기**), 컬러 필터(**곱하기**), 비네팅(**큰 쪽**) — 실내 보정을 덮어쓰지 않으므로 밝기 설정(`postExposure`)도 그대로. 구간이 바뀌면 4초에 걸쳐 옮겨 간다.
> ⓒ **소등(기획서 조도 표)**: 「방 상자 밖의 LampFluo 실시간 등」= 복도 등. 그중 과학실 x 범위·과학실과 복도 북쪽 끝 사이가 **과학실 앞 복도**(`LampFluoBuiltinA_ON (11)`), 나머지 6개가 좌측 복도 순서(x 오름차순 (7)·(6)·(5)·(4)·(1)·(12)). 복도 구간 n이면 **좌측 끝부터 n개**(`IlluminanceMap.LeftOffFor`), 3구간부터 과학실 앞도. 화장실 상자 안 등 2개는 **2구간부터 붉게**(색·세기·발광). 발광은 MaterialPropertyBlock. 조우 소등(`LightGroup.TurnOn`)이 다시 켜도 다음 프레임에 맞춘다.
> ⓓ **옛 `SpaceLights` 은퇴**: 씬 `JudgeLights` 아래 5개가 구간마다 등 8/6/4/2/0개 소등 + 등 색온도 6500→1200K를 **아직 하고 있었다**(3일차쯤부터 방이 통째로 어두워질 수 있었음). 이벤트 구독을 끊고 `OnBandChanged`를 막았다(`SpaceLights.Retired`, 클래스·씬 컴포넌트는 옛 단서 조건 참조 때문에 남김). 남은 조도 연출(손전등 깜빡·끊김, 4구간 시야 가장자리 형체)은 이 컴포넌트 밖이다.
> **되살리지 마십시오:** 「SpaceLights가 구간마다 등 개수·색온도를 바꾼다」 · 「조도 색온도를 라이트 색으로」(Volume White Balance) · 「톤 Volume을 기본 레이어(0)에」(카메라 마스크 256) · 「톤이 실내 보정의 채도·비네팅을 덮어쓴다」(더하기·곱하기·큰 쪽) · 「복도 등 = 복도 상자 안의 등」(복도 상자가 과학실·도서관과 겹친다 — 방 상자 밖으로 고른다).

> **개정: 2026-10-03(23차). 경비실 전화로 근무 일찍 끝내기 + 상호작용 외곽선(민 요청). 씬 작업(민 승인): 루트 `Interactables/ShiftEndPhone`(벤더 `Telephone01`, 경비실 북쪽 책상 `TeacherTable02_static` 위 31.35, 2.11, 47.55). EditMode 551/551, PlayScene에서 점검 남음 → 「남은 점검 n건」·흐린 외곽선 / 전부 보고 → 「[E] 근무 종료 보고」·외곽선 → 종료 → 결과창(경고 0), CCTV 모니터·점검 대상(소화기·사물함) 외곽선 실측, 에러 0.**
> ⓐ **전화기 종료**: 코어 `NightRun.CanEndShiftEarly` = 밤 진행 중 · 붙잡히지 않음 · 점검표 1건 이상 · **전부 보고**(빈 점검표는 「다 했다」가 아님). Flow `ShiftEndPhone`(Interaction): 조준(1.8m, 굵기 0.06 — CCTV와 같음)·막는 조건(일시정지·태블릿·CCTV 보는 중·붙잡힘) 뒤, 못 끝내면 「점검을 모두 마쳐야 근무를 끝낼 수 있습니다 · 남은 점검 n건」(조준선 어둡게, 외곽선 흐리게), 끝낼 수 있으면 「[E] 근무 종료 보고」 → **3초 안에 [E] 한 번 더**(「한 번 더 — 근무를 종료합니다」, 다른 곳을 보면 취소) → `NightRun.RequestEndNight()` — **04:00 자동 종료와 같은 길**(남은 카드 정산·조우 이월·`DayEnded` → `PlayResultRouter` 결과창). 마지막 점검 공간은 02:16부터 열리므로 사실상 그 뒤에만 가능. 안내는 `InteractionHud.ExternalPrompt`(문 안내가 우선). 메뉴 「야간근무/경비실/전화기 놓기」(`Flow/Editor/GuardRoomPhoneBuilder`)가 전화기·조준 상자·컴포넌트·외곽선 머티리얼 둘을 만든다(여러 번 눌러도 같음).
> ⓑ **상호작용 외곽선** `Flow/Interaction/InteractionOutline`: 그리고 싶은 쪽이 매 프레임 `InteractionOutline.Request(transform)` — 부르지 않으면 0.15초에 사라짐. **씬의 렌더러·머티리얼은 안 바꾼다** — `Graphics.RenderMesh`로 **플레이어 카메라(`Camera.main`)에만** 두 번 더 그린다: 마스크(`NightDuty/InteractionOutlineMask`, 스텐실 128 비트만, 큐 Transparent+49) → 뒤집은 껍질(`NightDuty/InteractionOutline`, 1080p 기준 2.2px, 스텐실 바깥만, +50). 그래서 다이얼·버튼 같은 안쪽 선 없이 실루엣만(마스크 없이 그렸더니 선화처럼 됨 — 실측). 머티리얼은 `Resources/InteractionOutline(.mat)`·`InteractionOutlineMask(.mat)`. LOD0만, 스킨 메시는 그 프레임 자세를 굽는다. 쓰는 곳 셋: **점검 대상**(`InspectionSensor` 포커스 = 2m·1초 응시로 보고 가능해진 항목 → 표식 `Inspect X-n`의 **부모 소품**에, 정상·이상 같은 선) · **CCTV 모니터**(들여다볼 수 있게 겨눴을 때, `CctvSystem.UpdateAim`) · **전화기**(겨누면 늘 — 끝낼 수 있으면 진하게 1, 점검이 남았으면 흐리게 0.35. 같은 날 민 요청 「전화기에도 외곽선」). `Request(target, strength)`로 진하기를 준다(같은 프레임 여러 번이면 가장 진한 것). 「점검 목록」 = 맵의 점검 대상으로 해석(경비실에 점검표 물건은 없음).
> ⓒ **함정 — 정적 배칭**: 플레이 중 정적 배칭된 렌더러는 `MeshFilter.sharedMesh`가 씬 전체를 합친 「Combined Mesh (root: scene) n」(서브메시 수백 개, 정점은 월드 좌표, `localToWorldMatrix` = 단위)다. 서브메시를 다 그리면 **근처 다른 물체까지 선이 생긴다**(소화기 → 젖은 바닥 표지판, 전화기 → 천장 등 — 실측). 그 렌더러 몫의 시작 서브메시는 공개 API가 없어서, **렌더러 경계 안에 드는 연속 서브메시 묶음(머티리얼 수만큼) 중 합친 경계가 렌더러 경계와 가장 닮은 것**을 고르고 렌더러마다 캐시한다(`SubmeshesOf`).
> **되살리지 마십시오:** 「근무는 04:00에만 끝난다」(점검을 다 하면 전화로 먼저) · 「빈 점검표면 바로 끝낼 수 있다」 · 「전화기는 한 번 눌러 바로 끝」(두 번) · 「점검 대상·CCTV 외곽선은 겨누기만 하면 나온다」(상호작용할 수 있을 때만) · 「전화기는 끝낼 수 있을 때만 외곽선」(겨누면 늘, 못 끝내면 흐리게) · 「외곽선은 렌더러에 머티리얼을 덧붙여서」(씬을 건드리지 않고 RenderMesh) · 「껍질만 그린다」(마스크 없이 → 안쪽 선) · 「정적 배칭 메시의 서브메시를 전부 그린다」.

> **개정: 2026-10-02(22차). 김진선님 연출 에셋 적용(노션 가이드 4편: 공포 연출 설정·크리처·화면 효과·태블릿). 씬 작업(민 승인): 루트 `HorrorEvents` 아래 가짜 놀람 캐비닛 2개 + 벌레 떼 5자리. EditMode 549/549, PlayScene에서 human tree 응시 → `HorrorGazeHold` 1.00, 캐비닛 두 종 재생, 노란 남자 대면 중 Wrongness 0.60·흔들림 0.40 → 창 닫히면 0으로, 과학실 소등 암전 2회(최대 0.96) → 0 실측, 에러 0.**
> ⓐ **사람 나무 = 김진선님 프리팹**: `mob.tree`는 이제 `HorrorCreature_HumanTree_Moving`을 **중첩**(빌더 스펙 `WrapPrefab`, 수정하지 않고 감싸기만)하고 `Aim`만 덧붙인다. 바람 흔들림(`HorrorTreeSway`)·응시 시 화면 물듦(`HorrorGazeHold`)은 김진선님 컴포넌트가 그대로 한다.
> ⓑ **화면 톤** `Flow/Direction/DirectionScreenFx`(+ `Resources/DirectionScreenFx.asset` = 김진선님 `VP_Horror_{Suffocate,Blackout,Wrongness,Creep}` 4개): 자식 전역 Volume `Fx_*`(**레이어 8 Viewmodel** — 플레이어 카메라의 볼륨 마스크가 256이라 다른 레이어면 안 보임, 우선순위 20~23, 기본 무게 0)을 지연 생성하고, 씬에 `ScreenWobble`이 없을 때만 붙이고 자식 `ScreenWobbleSource`로 흔들림을 준다. API `Push(key, kind, peak, fadeIn, hold, fadeOut)`(hold<0 = Release까지) · `Flash` · `Release` · `ClearAll` · `WeightOf`, 종류별 최댓값. 연결(`DirectionStage.ApplyScreenFx`, 대면 시작에 걸고 `Undo`에서 Release): 모형 급습·복도 끝 = Suffocate 0.8 · 과학실/화장실 소등 = Blackout 번쩍 2회 · 노란 얼굴·창밖 남자 = Wrongness 0.6 + 흔들림 0.4. 태블릿(오버레이 카메라)은 영향받지 않는다.
> ⓒ **가짜 놀람 캐비닛**: 메뉴 「가짜 놀람 자리 놓기」가 김진선님 `CabinetCreak`(열려 있는 사물함, `fake.locker.row`, 1회)을 `LockerB_static (12)`(22.18, 43.54) 자리에, `CabinetBang`(덜컹이는 사물함, `fake.locker.rattle`, 반복)을 `LockerB_static (20)`(12.21, 35.54) 자리에 놓고 원래 사물함은 끈다. 인스턴스의 **`HorrorTriggerZone`·트리거 콜라이더는 끈다** — 언제 놀랄지는 긴장 디렉터의 놀람 예산이 정한다(`DirectionFakeSpot`, `DirectionStage.PlayFakeSpot`: 가장 가까운 자리 `maxDistance` 14m 안, 재생 중·1회용 재생됨 제외 → `HorrorEvent.Play()`, 덜컹이면 암전 번쩍 0.7 2회). 자리가 없거나 멀면 옛 소리로 대신.
> ⓓ **일부러 쓰지 않은 것**: `DoorSlam`·`OpenDoorSlam`(문을 연출이 직접 움직임 — 「안 쓰는 문은 안 열린다」와 충돌하고, 예시가 동선 문 (9)라 갇힐 수 있음) · `CabinetRampage`·`DrawerRampage`(김진선님 테스트 씬에서 경비실 `HorrorAxisLink`로 축 구간에 묶임 — 기획의 「경비실 소품 요동」은 신뢰 축 연출이라 10단계에서) · Mannequin·BugSwarm(기획 가짜 놀람 목록·예산 밖) · Creep 톤(맞는 사건이 없고, 축 계기판처럼 쓰면 안 됨) · 태블릿 CSV·`GuardRoom_Knock`(피날레·4일차 사건 = 10~11단계, 역설·글리치 문자는 기존 `TabletBridge`가 이미 함).
> ⓔ **벌레 떼(김진선님 `HorrorEvent_BugSwarm`, 민 요청 — 같은 날 후속)**: 가짜 놀람 `fake.bugs`(`TensionDirector.FakeBugs`)를 목록에 넣었다(이제 4종, 같은 것 밤 2회·전체 상한은 그대로). **조건**(`FakeAllowedHere`): 정규화 전 방이 `BugSpaces` = 1-3 교실(뒤 창고 포함)·화장실·도서관이고 그 방에 `BugDwellSeconds` 6초 이상 머물렀으며, **그 방에 조우(존재형 포함)가 서 있지 않을 것**(천장 다리·소녀·노란 얼굴 옆에서 벌레가 시선을 끌면 응시 판정이 억울해짐). 자리 5개(메뉴 「가짜 놀람 자리 놓기」가 바닥을 재서 놓음, 무리는 루트 +3.6m = 천장 5.5 바로 아래): 1-3 뒤 창고 문간 쪽(51.9, 33.8 — 천장 다리 자리에서 비킴, 뒤 통로 문틈으로 보임) · 화장실 세면대 앞 형광등 밑(0.8, 34.5 — 서쪽 입구에서 보임, 소녀 동선 x 3.2와 떨어짐) · 도서관 깨진 천장 밑 셋(열람 탁자 서쪽 7.0, 46.7 / 정문 안쪽 12, 42.5 / 북서 서가 4.5, 50). 탁자 사이·칸 문 뒤에 두면 벌레가 가려져 옮겼다(실측). `DirectionStage.PlayFakeSpot`은 **시야 안(카메라 ±55°, 가림 없음) 자리를 먼저**, 그다음 가까운 자리(벌레 자리는 10m — 옆 방 자리가 걸리지 않게), 없으면 `SFX_BugCrawl` 소리만. 도서관은 조명이 없어 손전등이 닿는 곳에서만 보인다(떨어지는 2.5초 뒤 10초간 기어 다님). **김진선님 연출 인스턴스 7개 모두 디버그 숫자 키(`useDebugKey`)를 껐다**(벌레 떼는 6번 키로 게임 중 아무 때나 터졌음) — 프리팹 자산은 그대로. F3 콘솔 조우 탭에 [가짜 놀람] 줄(예산 안 씀, `NightRun.DebugForceFake` / `TensionDirector.ForceFake`). 테스트 2개(`DirectorRuleTriggerTests`).
> **되살리지 마십시오:** 「연출이 판정 대상 문을 직접 움직임」(DoorSlam류) · 「가짜 놀람 캐비닛의 자체 트리거 켜기」(놀람 예산을 우회) · 「화면 효과 Volume을 기본 레이어에」(레이어 8만 보임) · 「ScreenWobble을 하나 더 붙임」(있으면 그것을 씀) · 「김진선님 프리팹·코드를 고쳐서 맞춤」(감싸거나 중첩만) · 「사람 나무는 자체 HumanTree_20k 대역」(김진선님 프리팹) · 「벌레 떼는 어디서나」(1-3·화장실·도서관만, 조우가 선 방 제외) · 「김진선님 연출의 디버그 숫자 키를 켜 둠」 · 「가짜 놀람 자리는 가장 가까운 것」(시야 안을 먼저) · 「벌레 떼를 열람 탁자 사이·칸 문 뒤에」.

> **개정: 2026-10-01(21차). 몹 고정 자리(민 지정)·몹 모델 연결·소리 표·콘솔 오른쪽. 씬 작업(민 승인): `DirectionAnchors` 아래 고정 자리 3개. EditMode 547/547, PlayScene에서 소년 착석·천장 다리·창밖 남자 확인(창밖 남자를 2초 보면 L5 위반까지 실측), 에러 0.**
> ⓐ **고정 연출 자리**: 씬의 `StageAnchor`(Flow, 위치 = 몹 피벗, +Z = 몹이 볼 방향)가 켜질 때 코어 `StagePoints`에 점을 적고, 대본의 `StageAnchor`/`ExtraStageAnchor`가 있으면 디렉터는 플레이어 기준 자리 대신 그 점을, 실행기는 그 자리·방향을 쓴다. 없으면(테스트·다른 씬) 옛 플레이어 기준 자리. **민 지정**: 소년(착석·머리 박기) = 1-3 교실(Classroom02) **맨 뒤 줄, 학생이 칠판(서쪽)을 볼 때 오른쪽에서 둘째 책상**(46.21, 36.28) · 천장 다리 = **뒤 통로 너머 창고 천장, 사다리 `LibraryLadder (1)` 바로 위**(52.74, 5.5, 33.16) · 창밖 남자 = **도서관 북쪽 `WallOutside_4m_WindowDouble`(10,0,56) 오른쪽 창 밖**(얼굴 높이 2.66 = 블라인드 아래 틈). 같은 이름의 창이 도서관 서쪽에도 있으나 책장에 가려 북쪽을 골랐다.
> ⓑ **정확한 방**: `EncounterScript.ExactSpace` — 소년·천장 다리는 `Classroom_1_3`에서만 방아쇠(디렉터가 정규화 전 방 `_exact`도 기억). 1-1 교실에서 1-3의 소년이 대면하던 문제 방지.
> ⓒ **판정 조준점 `Aim`**: 대역의 `JudgeTarget`은 이제 자식 `Aim`(머리, 천장 다리는 다리 가운데)에 붙는다. 루트(발바닥)에 두면 손전등 비춤의 가림 검사가 바닥에 걸렸다. 응시가 필요한 몹(천장 다리·창밖 남자)만 `Aim`에 단단한 상자. **창밖 남자는 창틀·벽기둥에 레이가 막혀** 자리의 `GazeProxy`(창 안쪽 면 0.9×0.5 상자)로 응시 상자를 옮긴다(`StandInFactory.ApplyGazeProxy`).
> ⓓ **몹 모델 연결(같은 날 정정 — 민 지정)**: 원본은 **`NCAI_BrokenCompass/Assets/크리쳐`·`/사운드`**(Unity 프로젝트 밖). 새 모델은 `2. Art/04 Materials/m_creature/m_businessduck·m_skirtboy·m_redgirl·m_eggmantree`로 복사했고, 인체모형·human tree는 프로젝트에 같은 파일이 이미 있다(m_humandummy 등). 새 FBX들은 텍스처 이름이 모두 `BaseColor`라 **Unity가 도서관 표지판의 BaseColor를 잘못 물었다** — 각 폴더 `Textures/`로 내장 텍스처를 뽑아(`ModelImporter.ExtractTextures`) 해결. 매칭: 앉은 소년·천장 다리 = **skirtboy**(앉힘·매달림) · 노란 얼굴·창밖 남자 = **business duck** · 복도 끝에 선 자 = **인체모형 선 자세**(`mob.dummy.stand`) · 모형 급습 = 인체모형 구부정 · 화장실 소녀 = **red girl** · 사람 나무 = **human tree**(`HumanTree_20k_Animated`, 바람 흔들림 반복 — 민 지정. eggman tree는 복사만 해 두고 쓰지 않음) · CCTV 사람 = blackman 유지(크리쳐 폴더에 없음, 민 결정). 프리팹은 메뉴 「야간근무/연출/몹 대역·소리 연결」(`Flow/Editor/StandInPrefabBuilder`)이 `Resources/StandIns/<ID>`로 만들고, 표에서 빠진 옛 프리팹(meatman·glitchman)은 지운다. 뼈 이름은 옛 리그(pelvis·thigh.L)와 새 리그(Hips·LeftUpperLeg)를 함께 찾는다. 「대역 사진 찍기」 메뉴가 `Temp/standins.png`에 전부 찍는다.
> ⓔ **함정**: `SkinnedMeshRenderer.BakeMesh(mesh, true)`가 6000.3에서 스케일이 빠진 정점을 줬다 — `false`로 굽고 회전·위치만 곱한다(실측).
> ⓕ **소리 표** `Resources/DirectionSounds.asset`(`DirectionSoundTableSO`): `<ID>.<단계>` → 클립. 민 제공 소리는 `_Game/Audio/<class|common|hall|lab|toilet>/`(폴더마다 README = 옛 카드 기준 쓰임새). `Resources/Direction/<이름>` 클립이 있으면 그쪽 우선, 없으면 표, 그것도 없으면 `*.<단계>`. 이름 뒤 `+`는 같은 순간 겹치는 둘째 소리(C4 = 칠판 긁기 + 교탁 의자). 27개: 분필(C1)·칠판+의자(C4)·책상 타격(소년)·둔한 쿵(머리 박기)·천장 조각(천장 다리)·잠금쇠(없던 문)·문 닫힘+뛰어오는 발소리(발소리 조우)·문 천천히 열림(H2)·유리 파손(S2)·무거운 물건(모형 급습)·물 내림(T1)·옷감(T2)·칸 문 닫힘(소녀) 등. **아직 없음**: 종소리(C3 해제), 부르는 목소리, 책장 넘김(L2), 창 두드림 전용(지금은 노크로 대신). `common/`의 발소리·손전등·태블릿 차임·게임오버·타이틀 BGM은 김진선님 쪽 시스템 몫이라 연결하지 않았다.
> ⓖ **디버그 콘솔**: 기본 화면 오른쪽(끌어 옮기면 그 자리 유지). 고정 자리 몹의 [이동+실행]은 그 자리가 보이는 곳(자리 앞 `DebugViewDistance`m)으로 옮겨 그쪽을 보게 한다.
> ⓗ **(같은 날 후속, 민 스크린샷으로 자리 지정)** 노란 남자(business duck)는 도서관에서 두 연출 — **L5 창밖**(북쪽 창, 위 ⓐ) · **L3 문간**: `stage.yellow.door` = 도서관 정문 `DoorWide (2)` 앞 복도(14.05, 42.2), 도서관 안쪽(서쪽)을 본다. 이 문은 평소 잠겨 닫혀 있어 **대면 동안만 열고**(잠금도 그동안만 풂) 끝나면 닫고 다시 잠근다(`StageAnchor.revealDoor`, `DoorHandle.ForceLock` 신설). 문 발신기가 달린 문은 연출로 열지 않는다(H2 방아쇠가 됨). **검은 남자 = 경비실 창밖** — 기획서 피날레(K4 결말)의 「창밖의 웃는 정장 남자」 자리 `stage.finale.window`(로비 29.35, 45.85, 경비실 서쪽 창 안을 봄), 프리팹 `mob.finale`(blackman, 머리 응시 상자). 피날레 흐름은 11단계라 지금은 콘솔 조우 탭 [경비실 창밖 검은 남자(피날레)]로만 세운다. **화장실 소녀**: red girl 걷기 클립을 제자리 걸음으로 복사(빌더가 루트 이동을 뺌)하고 `DirectionWalker`가 `stage.girl.walk`(바깥쪽 칸 앞 3.2, 32.5) → 칸 안(3.2, 34.3)으로 0.45m/s 걸린 뒤 사라지게 한다 — 입구(서쪽)에서 보면 옆모습으로 걸어 들어간다. 칸 문은 열지 않는다(H2 오작동 방지).
> ⓘ **문 정책 재정비(민 지시: 「쓰는 문만 열고 나머지는 잠근다」)**: `DoorPolicyBuilder` 표를 근무 공간의 출입문 7개로 다시 썼다(**건물 정문 `Exterior/Doors/DoorMain`은 잠금 — 민, 2026-10-02**) — 경비실 `DoorNarrow (3)` · **도서관 `DoorWide (2)`**(그동안 정책에 없어 잠겨 있었음) · 과학실 둘(`DoorNarrowSolid (9)`, `(8)` = corridor.door.auto) · 화장실 출입·칸 둘. `judgeTargetsAreOpenable` 끔(판정 대상이어도 안 쓰는 corridor.door.back은 잠금). **동선 문의 벤더 잠금은 `PlayerInteractor` 시작 때 런타임으로 푼다**(씬 파일 그대로) — 그래서 디버그 콘솔의 잠금 무시 기본값을 껐다(실제 게임과 같게). **H2 자동 개방은 정책상 열리는 문만**(잠긴·안 쓰는 문은 건너뜀, 강제 잠금 해제 삭제). 실측: 복도에서 H2 → corridor.door.auto 개방, 바로 앞의 corridor.door.back은 그대로. 걸음 검사(0.25m 격자) — 동선 문만 열린 상태로 7공간·칸·정문 밖 모두 도달, **교실 1-1은 쓰지 않는다(민)** — 문을 잠갔고(문 앞도 레벨 선반 `CellShelves (3)·(4)`가 막음), C1(판서) 청취 구역을 1-3 출입구 앞 `cls13.door.outside`(40, 41.1, 반경 1×1)로 옮겼다(씬 SpaceZones에 추가). 옛 `cls11.door.outside` 구역과 옛 24장의 cls11.* 대상은 그대로 두었다(쓰지 않음).
> **되살리지 마십시오:** 「몹은 언제나 플레이어 앞 몇 m」(고정 자리 몹) · 「창밖 남자 = glitchman」 · 「노란 얼굴 = 옛 duck」 · 「천장 다리 = glitchman」 · 「복도 끝 = meatman」 · 「몹 모델은 m_creature의 옛 모델(boy·duck·meatman·glitchman)」(원본은 NCAI_BrokenCompass/Assets/크리쳐) · 「JudgeTarget을 대역 루트(발바닥)에」 · 「소년 조우는 아무 교실에서나」 · 「BakeMesh(mesh, true)로 경계 재기」 · 「디버그 콘솔은 왼쪽 위」 · 「검은 남자 = 도서관 창밖(L5)」(L5는 노란 남자, 검은 남자는 경비실 창밖 피날레) · 「노란 얼굴은 플레이어 앞 4m」(도서관 정문 앞) · 「화장실 소녀는 서 있기만 한다」(옆으로 걸어 칸으로) · 「판정 대상 문은 표에 없어도 열린다」 · 「H2 연출은 잠긴 지정 문을 풀어서 연다」 · 「도서관 문은 잠겨 있다」 · 「교실 1-1을 근무 공간으로 쓴다」(C1 분필은 1-3 출입구 앞).

> **개정: 2026-10-01(20차). 1일차는 몹 없이, 조우에 묶인 수칙은 그 조우와 함께만, 옛 하네스 삭제(민 지시). EditMode 545/545 통과, PlayScene에서 1일차 덱 [H2·C4·S3·G1·G2]·슬롯 0, H2 단서 → `corridor.door.auto` 자동 개방 → 본 것으로 판정 → 플레이어가 닫으면 위반 +12 확인, 에러 0.**
> ⓐ **1일차 = 조우 없음(기획서와 다름 — 민 결정).** 「몹은 2일차부터 — 긴장에 기승전결」. `NightProgram.BuildDay1`은 슬롯 없이 고정 덱 **H2 · C4 · S1|S3 · G1 · G2**(복도는 H2 고정, 교실은 C4 고정 — 둘 다 혼자 서는 수칙). 기획서의 1일차 「소년 착석(C3)」은 쓰지 않는다.
> ⓑ **조우에 묶인 수칙**: `ProgramCatalog`에서 H1 → `PeopleTree`(인체나무), C2 → `CeilingLegs`(천장 다리)로 `boundEncounter`를 달았다. 묶인 수칙은 그 조우 없이는 덱에 들어가지 않는다 — 「인체나무가 없는데 복도의 미분류 물체를 피하라」는 수칙이 뜨던 문제. **강제 수칙**(2일차 첫 역설 C2 등)이 묶인 수칙이면 그 조우를 함께 편성(`PlaceEncounter(d, bound, true)`), 못 놓으면 보고에 적는다. 소년 머리박기(`BoyBang`)는 대본에 `ExtraStandIn/ExtraAnchorId/ExtraCue`로 **천장 다리를 함께 세운다**(C2가 둘째 수칙).
> ⓒ **H2 단서 대본**: `RuleTriggers`에 H2(복도 8~20초 머무름, `cue.door.autoopen`). 9개가 됐다.
> ⓓ **H2 연출** `DirectionStage.OpenDoorAhead`: 레벨에서 자동 개방용으로 지정한 `corridor.door.auto`를 먼저 고른다(앞쪽에 보일 때만 우선, 25m 안, 평소 잠겨 있으므로 이때만 `ForceUnlock`). 없으면 잠기지 않은 닫힌 문 3~18m 중 앞쪽·가까운 것. `BeginDirectionMove` 뒤 `Open` → `DoorRelay`가 「연출 개방을 응시 중」일 때만 `DoorAutoOpenObserved`. **보지 못한 개방은 판정하지 않는다**(트리거 없음 = 벌도 보상도 없음).
> ⓔ **옛 하네스 3파일 삭제**(`NightDutyTestHarness`·`HarnessStandIns`·`DutyTabletPanel`) — 옮긴 곳은 §3.4 표. `AnomalyCueDirector.CanSend`는 새 편성에서 false.
> ⓕ **테스트**: 1일차 고정덱·재시작 편성 테스트를 새 덱으로, 「소년 착석 → C3」는 `DebugAddFinalRule("C3")` + `DebugForceEncounter`로 바꿨다. `DayOneAndBindingTests` 3개 추가.
> **되살리지 마십시오:** 「1일차 소년 착석」 · 「1일차에 몹·조우 슬롯」 · 「H1·C2를 혼자 서는 수칙으로」(조우 없이 덱에 듦) · 「강제 수칙은 조우 없이 덱에만 넣는다」 · 「하네스」(`NightDutyTestHarness`·`HarnessStandIns`·`DutyTabletPanel`, 1초 응시 흉내) · 「H2 연출은 잠긴 문을 건너뛴다」(지정 문은 연다).

> **개정: 2026-10-01(19차). 7단계 — 긴장 디렉터·놀람 예산·연출 실행기(대역·소등·런타임 구역)와 디버그 콘솔(F3). 씬 작업(민 승인): 1층 도서관 공간 상자 추가. EditMode 542/542 통과(플레이 뒤에도), PlayScene에서 콘솔로 소년 착석(C3)·과학실 소등(S4)·노란 얼굴(L3) 확인, 에러 0.**
> ⓐ **`TensionDirector`**(`Scripts/Direction/Tension/`, 코어): 그날 편성의 슬롯 A 01:00–01:52 · B 02:16–03:08 · C 03:08–03:30(위험 단계면 끔)마다 조우를 「방아쇠 → 전조(강도 3↑만 1~8초, 헛예고 30% / 청각 구간 3↑ 40%) → 대면 → 대응 창 → 끝 단서 → 결과」로 돌린다. 대면 때 대본의 단서(`FinalCues`)를 큐에 넣고, `NightRun`이 꺼내 `Send`로 판정 책에 넣는다(`FlushDirection`, 재진입 방지). 03:30 뒤 새 조우 없음. 슬롯 끝 10분 전부터는 「그 공간에 있기만 하면」으로 방아쇠를 완화, 끝까지 없으면 `Missed`.
> ⓑ **대본 표** `EncounterScripts`(조우 15개: 방아쇠·머무름·창·끝 단서·자리·대역 ID·기준점·소등·구역)와 `RuleTriggers`(조우 없는 수칙 단서 8개: C1 분필(`cls11.door.outside` 구역) · C4 붉은 불 · S2 유리 · T1 물 내림 · T2 사용 중 칸 · T5 불 켜진 칸(화장실 나갈 때까지) · L2 책장 · K2 빈 방(보고 있지 않은 채널)). 수칙 단서는 밤에 한 번, 조우 중엔 쉼, 20초 간격.
> ⓒ **`SurpriseBudget`**: 강도 3↑ 3/4/4회 · 4↑ 1/2/2회 · 5는 1–2일차 0, 3–4일차 교차만 1, 5일차 1. 강도 3↑ 사이 90초, 4↑ 뒤 120초, 조우 뒤 60~90초 휴지. **`DirectorMoods`**: 최고 감각 생존 수치 0–49 쉬움(창 ×0.8, 가짜 놀람 +1) · 50–74 기본 · 75–99 또는 재시작 k≥2 위험(전조 ×1.5, 창 ×1.3, 헛예고 절반, 슬롯 C 끔). 가짜 놀람 3종은 진짜 1에 3까지, 같은 것 밤 2회.
> ⓓ **재시작**: 디렉터를 새로 만들지 않고 `ResetToRest(k, 시작 분)` — 시작 분(00:00·02:16) 뒤에 대면한 조우·울린 단서만 다시 기다린다. 이미 본 강도 4↑의 전조는 절반. 밤이 닫히면(`CloseNight`) 모두 끝 단서 없이 중단.
> ⓔ **판단(기획서와 다른 점)**: ① 「복도 끝에 선 자」·「모형 급습」의 방아쇠는 기획서상 「과학실 퇴실」이지만 S5는 과학실 안에서 판정하므로 **과학실 10초 체류 뒤 앞쪽 7m(문밖)**에 세운다. ② 「소년 착석」은 「교실 점검 항목을 처음 비출 때」 대신 교실 3초 체류. ③ 「노란 얼굴」은 「도서관 점검 2개 뒤 출입구를 등질 때」 대신 도서관 8초 체류. 셋 다 대본 표의 `Note`에 적었다.
> ⓕ **연출 실행기**(Flow): `DirectionStage`(자동 설치)가 `EventBus.DirectionEmitted`를 받아 대역(`StandInFactory` — `Resources/StandIns/<ID>` 프리팹이 있으면 그것, 없으면 어두운 캡슐·박스, 응시 판정이 필요한 천장 다리·창밖 남자만 단단한 콜라이더), 방 소등(`LightGroup` — 그 공간 상자 안의 실시간 라이트 + `LampFluo` 발광을 MaterialPropertyBlock으로), 런타임 신호 구역(`FinalRuleRelay.SetRuntimeZone` — `science.dark` = 소등한 과학실 전체, `classroom.phantomdoor` = 없던 문 앞), C4 붉은 조명, T5 칸 안 작은 등, 손전등 끊김(가짜 놀람)을 맞춘다. 소리는 `Resources/Direction/<ID>.<단계>` 클립이 있으면 재생(지금은 없음). `DirectionCue`는 대역·완성 몹 루트의 계약(Play/SetPhase/Abort/ResetToRest, `PhaseChanged`).
> ⓖ **디버그 콘솔 F3**(`Flow/Debug/NightDutyDebugConsole.cs`, 에디터·개발 빌드만, 자동 설치): 개요(밤 시계 구간 점프 — `NightRunDriver.DebugJumpToNightMinute`, 판정 강제, 자동 연출 끔, 시계 정지, 축 ±, 손전등·달리기 신호, 공간 이동, 밤 종료·붙잡힘·재시작) · 수칙(덱 상태 ✓/✗/진행 중, [이동]·[단서]·[끝]·[조우], [덱에 추가]) · 조우(슬롯 상태·헛예고, 15개 [이동+실행]/[여기서], 다음 단계, 무대 정리) · 점검([이동]·[정상]·[이상]) · 로그. **[이동+실행]은 디렉터가 새 공간을 알 때까지(3프레임 이상) 기다렸다가 실행**한다 — 바로 실행하면 옛 자리로 판정한다(실측 버그).
> ⓗ **씬**: `SpaceZones`에 1층 도서관 상자(중심 8.2, 3.4, 48 · 반경 6.2×8, 엠비언트 설정의 실측 상자) 추가 — 복도 상자보다 앞. 없을 때는 도서관이 복도로 잡혀 L1~L5가 영영 방아쇠를 받지 못했다. `SpaceZones.TryGetSpaceBox`(정확한 값 먼저, 그다음 교실 정규화)·`TryGetSignalZone` 추가.
> ⓘ **함정 2개(§5)**: ① 열린 씬이 수정된 채로 EditMode 테스트를 돌리면 「씬 저장?」 대화상자에서 에디터 전체가 멈춘다(2026-09-30 밤·10-01 아침 두 번) — 테스트 전 `EditorSceneManager.SaveOpenScenes()`. ② 플레이 종료 뒤 도메인 리로드가 없으면 구동기가 켠 정적 스위치가 남아 옛 규칙 테스트 4건이 깨진다 — `NightRunDriver.OnDestroy`가 스위치를 기본값으로 되돌린다.
> ⓙ **(같은 날 후속) 태블릿·옛 덮개 정리.** ① 태블릿(`TabletDocument`, 김진선님 코드 — 수정 안 함)은 `NightRun.TodayDeck`의 `PlayerText`를 읽는다. 새 편성이면 `TodayDeck`이 `NightRun.DisplayDeck`(판정 조건 없는 표시용 `RuleSO` — 그날 새 수칙 → `[점검] 공간 이름 — 문구`, 늦게 열리는 항목은 「(02:16부터)」, 보고하면 「· 보고함」, 첫 점검 줄 아래 Z/X 안내)을 돌려준다. 옛 `RuleBook`은 여전히 `DeckToday`를 판정하므로 표시 카드는 판정에 들어가지 않는다. 별도 「점검표」 탭은 9단계(태블릿 UI) 몫. ② 옛 임시 태블릿 `DutyTabletPanel`(우측 지침 + 하단 4축 막대) 자동 생성 끔. ③ 옛 하네스(`NightDutyTestHarness`)는 문 발신기·잠금 무시(F4/F5)만 남기고 옛 대상 채우기·콜라이더·**옛 조우 8장면 모형**·응시 대역을 기본 끔, 상태판 키 F3 → **F9**(F3은 새 콘솔). ④ `HarnessStandIns`의 옛 「방 체류 점검」 막대·알림과 `AnomalyCueDirector`(옛 24장 단서)는 새 편성에서 쉰다.
> **되살리지 마십시오:** 「조우는 슬롯 시작에 바로 건다」(방아쇠·예산을 거침) · 「강도 1~2도 전조가 있다」 · 「헛예고도 단서를 보낸다」 · 「디버그 강제 실행은 예산을 쓴다」 · 「도서관은 SpaceZones에 없어도 된다」 · 「테스트는 씬이 더러워도 돌려도 된다」.

> **개정: 2026-10-01(18차). 6단계 후반 — 새 수칙 31장의 판정을 넣고 옛 24장 덱·옛 조우 연출기를 쉬게 했습니다. 씬 작업(민 승인): 새 수칙 기준점 4개 · `science.tape` 신호 구역 · 경비실 공간 상자를 판정 범위 안으로. EditMode 516/516 통과, PlayScene 재생 확인(1일차 덱 [H1·C3·S1·G1·G2], 경비실 스폰 = `SecurityRoom`, 손전등으로 바닥 모형을 비추면 `rule.S3.model`).**
> ⓐ **`FinalRuleBook`**(`Scripts/FinalRules/`)이 그날 편성 덱(`NightRun.Program.Deck`)을 판정합니다. `NightRun.ProgramEnabled`가 켜져 있으면 `LoadDeck`은 빈 덱을 돌려주고(옛 24장 판정 정지) `EnsureEncounter`도 건너뜁니다. `DeckOverride`를 주면 옛 덱도 함께 돕니다(시험용).
> ⓑ **점수 규칙**: 일반 수칙은 **첫 위반에서 그 축 +12 한 번**(밤당). 방아쇠가 한 번 이상 왔고 위반이 없으면 밤 종료에 **신뢰 +2**. 위협 수칙(H3·H4·C3·S5·T3·L3·L5)은 **조우(에피소드)마다 실패 +20 / 성공 신뢰 +3**, +12·+2는 없습니다. G2·T4는 점검판이 델타를 주고 여기서는 기록(`Noted`)만 합니다. K4·G3은 판정 없음.
> ⓒ **단서 계약**: 연출은 `JudgeSignal.Cue(id, 위치)`로 시작하고 같은 ID의 `JudgeSignal.CueEnd(id)`로 끝냅니다. 이름은 `FinalCues`가 정본입니다(`cue.footsteps` · `cue.voice` · `cue.chalk` · `cue.boy.seated`/`cue.bell` · `cue.redlight` · `cue.phantomdoor` · `cue.glass` · `cue.blackout.science` · `cue.hallend` · `cue.flush` · `cue.stall.occupied@<구역>` · `cue.blackout.toilet` · `cue.girl.stall` · `cue.stall.lit` · `cue.pages` · `cue.yellowface` · `cue.windowknock` · `cue.cctvperson` · `cue.emptyroom@cctv.ch<n>`). 판정기가 단서를 받을 때 플레이어가 그 공간에 없으면 해당 없음(C3·S5·T3·T1·L2).
> ⓓ **새 신호 6종**(`SignalKind` 33·43~47): `CueStarted` · `PlayerPose`(0.1초, 발밑+수평 방향) · `Running`(바뀔 때) · `BeamSample`(0.1초, 손전등 원뿔 15°·8m 안 가려지지 않은 `rule.` 기준점, 빈 샘플도 보냄) · `CctvChannel` · `CctvViewSample`. `JudgeSignal`에 `Point` 필드가 생겼습니다(6인자 생성자는 그대로).
> ⓔ **`FinalRuleRelay`**(`Flow/Sensors/`, 자동 설치)가 자세·달리기(수평 3.5 m/s)·비춤·CCTV 시청/채널을 보냅니다. 새 편성이 꺼져 있으면 아무것도 보내지 않습니다.
> ⓕ **재시작**: 판정 책은 새로 만들지 않고 `ISnapshotable`로 방아쇠·위반 여부·결과 수를 되돌립니다(진행 중 에피소드는 버림). S2 「과학실 종료」는 방아쇠가 스냅샷 전에 났으면 유지됩니다.
> ⓖ 결과: `NightRun.FinalRules` · `NightRun.FinalResults` · `EventBus.FinalRuleSettled`. S3 위반 → `E.ModelRush`, L3 실패 → `E.SuitMan`을 `NightRun.ReserveEncounter`로 예약합니다. T4 단서(`cue.girl.stall`) → `SetReverseReport("T-1", true)`.
> ⓗ **씬(PlayScene)**: 메뉴 `NightDuty ▸ 새 수칙 기준점 배치`(`RuleAnchorPlacer`)로 `rule.S1.center`(48, 1.5, 42) · `rule.S3.model`(바닥 인체 모형 `Anatomical Human Torso (1)`) · `rule.L1.shelf`(`BookShelvingDouble (10)`, 기울어진 책장) · `rule.L4.box`(`CardboardBoxA (24)`). 콜라이더 없음(응시를 가로채지 않게). `SpaceZones` 신호 구역에 `science.tape`(모형 둘레 2×2m) 추가. **경비실 상자(zones[5])를 `SecurityRoom` · 판정 범위 안(`OutOfScope` false)으로** 바꿨습니다 — K2·K3이 경비실 공간을 읽습니다. 그에 맞춰 `AnomalyCueDirector.SpaceSlots` 6 → 9(경비실 7 진입에서 배열 초과 예외가 났습니다).
> ⓘ 아직 없는 것: 단서를 내는 연출(7단계 긴장 디렉터) · 조우 소품의 기준점(`rule.H1.object` · `rule.C2.legs` · `rule.L3.face` · `rule.L5.man`) · 구역 `science.dark` · `classroom.phantomdoor` · 테이프 바닥 표시(아트). 그래서 지금 실제 플레이에서 방아쇠가 오는 수칙은 G1 · K3 · S1 · L1 · L4 · S3 · H2(연출이 문을 열 때) · K1/K2(단서 전까지는 무반응)뿐입니다.
> ⓙ 같은 날 앞서 **점검 대상 17개를 씬에 놓았습니다**(메뉴 `NightDuty ▸ 점검 대상 배치`, `InspectionTargetPlacer`): 소품 아래 자식 `Inspect <ID>` + `JudgeTarget`(`inspect.<ID>`) + 소품을 감싸는 트리거 아닌 `BoxCollider`(+4cm). 경로표는 스크립트의 `Map`.
> **되살리지 마십시오:** 「새 편성을 켜도 옛 24장 덱이 판정한다」 · 「경비실은 판정 범위 밖(`OutOfScope`)」 · 「`AnomalyCueDirector.SpaceSlots = 6`」 · 「일반 수칙은 위반할 때마다 +12」 · 「위협 수칙도 밤 종료 준수 +2를 받는다」.

> **개정: 2026-10-01(17차). 6단계 앞부분 — 새 수칙·조우 카탈로그와 밤 편성기(배정 순서 1~7)를 넣었습니다. 판정은 아직 옛 24장이 합니다. EditMode 474/474 통과, PlayScene 재생 확인(1일차 편성 [H·C3·S·G1·G2] + 소년 착석).**
> ⓐ `Scripts/Program/ProgramCatalog.cs`: **새 수칙 28장 + G1·G2·G3**(`RuleDef` — 공간·축·문구·위협·손전등·대기형·묶인 조우)과 **조우 15개**(`EncounterDef` — 주축·대응 수칙·강도·몹·발동 조건, 소년 머리 박기는 C2+C3, 천장 다리 몹 = 소년). **편성 데이터의 구현값 정본**입니다.
> ⓑ `Scripts/Program/NightProgram.cs`: `ProgramDirector.Build(ProgramRequest)` — 1일차 고정(H1|H2·C3·S1|S3 + 소년 착석) → 강제 수칙(회피 불가 쌍, 2일차 C2) → 지난 밤 예약(`Reserve` — 모형 급습 3일차부터·정장 남자) → 슬롯 조우(점수 = 주축 구간×2·미관람 +3·교차 +2·직전 같은 축 −2, 직전 같은 몹 제외, 놀람 예산) → 경비실(5일차 K4, CCTV 사람이면 K1, 아니면 K2·K3) → 남은 칸(혼자 서는 수칙, 최고 축 ×2, 최저 축 최소 1장). 손전등 수칙 하루 3장·T4 회차 2번. 슬롯 C는 3일차부터 밤 시작 최고 감각 생존 수치 ≤49일 때, 재시작 k≥2면 `NightProgram.SlotActive`가 끕니다.
> ⓒ `NightRun.ProgramEnabled`(NightRun.Program.cs, 구동기가 켬)·`Program`·`Programs`·`ReserveEncounter`. 점검 편성 뒤, 재시작 제외 밤 시작에 한 번 만듭니다. NightRun은 이제 `partial`입니다 — 확장은 `NightRun.*.cs`에 두고 본체의 `OnNightPlanned`·`ResetExtensions` 훅을 씁니다.
> ⓓ **아직 안 한 것(6단계 뒷부분)**: 새 수칙마다 판정 조건(RuleSO) — 달리기·정지 0.2m·돌아보기 90°·채널 넘김·머무름 같은 새 신호가 필요합니다. 그다음 옛 24장·옛 조우 8장면·관련 테스트를 지우고 판정을 새 덱으로 바꿉니다. 조우의 시점·위치는 7단계 긴장 디렉터.
> **되살리지 마십시오:** 「덱과 조우를 따로 뽑는다」(조우를 먼저 뽑고 대응 수칙을 고정) · 「경비실 수칙을 1일차부터」 · 「G3에 판정」 · 「역보고는 S3·T4」(T4 하나).

> **개정: 2026-10-01(16차). 최종 기획서 제작 계획 4단계(점검·보고)·5단계(센서 공통 정의)와 3단계 잔여(스냅샷 레지스트리·경계 사례)를 넣었습니다. 씬·프리팹·에셋은 고치지 않았습니다. EditMode 459/459 통과, PlayScene 재생 확인(1일차 점검표 편성·센서 자동 설치, 오류 0).**
> ⓐ **점검(`Scripts/Inspection/`)**: `InspectionCatalog`(17항목 — 복도4·교실3·과학실3·화장실3·도서관3·경비실1, 점검 항목의 구현값 정본) · `AnomalyAssigner`(5(2)·5(2)·6(3)·6(3)·7(3), 경비실 포함 4곳 이하·좌우 동 각 1 이상, 1일차 튜토리얼 고정 K-1 정상·H-4·S-2 이상, 처음 점검하는 공간엔 이상 없음, 연출 구간 1 이상 축만·가중치 = 구간, 마지막 공간은 호출 2에 열림, 환청 청각 2~3이면 1회·4면 1.5배) · `InspectionBoard`(보고 경제 −5(축·밤당 −10)/+8/+7/G2 +6/T4 역보고, 「가까이」 +6 항목당 1회, 04:00 미완료 경고 1 + 이상이면 +8).
> ⓑ **`NightRun`**: `InspectionsEnabled`(기본 false, 구동기가 켬)·`InspectionPlanOverride`·`Inspections`·`ReportInspection(id, 이상?)`·`InspectionStartle`·`MarkHallucination`·`SetReverseReport`. 판정 정지·03:30 뒤 보고 델타는 95에서 멈춤. **옛 「미방문 벌점 +9」(`UnvisitedPenalty`)는 지웠습니다** — 미완료 경고가 넘겨받음.
> ⓒ **스냅샷 레지스트리** `ISnapshotable`(NightSnapshot.cs): 점검판이 한도 사용량·가까이·환청·역보고 표시를 싣습니다. 이미 한 보고는 되돌리지 않습니다. 경고 장부는 `TotalTaken`으로 **이미 나온 처벌을 재시작 뒤 되풀이하지 않습니다**.
> ⓓ **경계 사례**: `FearAxisSystem.BeginFrame/EndFrame` — 한 신호에 두 축이 100이면 초과량이 큰 축(동점 청각>조도>배치)으로 붙잡힘. `NightRun.Send/Tick`이 묶습니다. `FearAxisSystem.Raised`(축, 양, 출처) → `NightRun.LastCaptureSources`(재시작 카드의 「그 축을 올린 수칙·점검 항목」), `DisplaySource`.
> ⓔ **센서(5단계)**: `SensingRules`(응시 10°·0.2초 틈·비춤 15°/8m·정지 0.2m·보고 2m/1초/0.5초·가까이 0.8m·태블릿 응시 기준점 78%) · `ReportReadiness`. `GazeProbe`는 **10° 원뿔**(판정 콜라이더가 있는 대상만, 가림 검사) → 없으면 옛 중앙 레이. `InspectionSensor`(Flow, 자동 설치): 2m·1초 응시로 보고 가능·포커스·틱 이벤트, 전용 키 Z=정상/X=이상 0.5초 길게, 0.8m·0.3초 들여다보면 가까이. 개발 빌드에 화면 안내 한 줄.
> ⓕ **태블릿 중 판정**: `NightRun.JudgeWhileTabOpen`(NightRun.Tablet.cs, 기본 false, 구동기가 켬) — 켜면 Tab 신호를 판정에 넘기지 않아 판정이 계속되고 `NightRun.TabletOpen`에만 적습니다. `PlayerSensors.TabOpen`은 이제 「신호를 멈춰야 하는가」(옛 규칙일 때만 참), 실제 상태는 `PlayerSensors.TabletRaised`. **그래서 태블릿을 든 채 문·CCTV 조작도 됩니다.**
> ⓖ `SpaceId`에 `Library=6`·`SecurityRoom=7`·`Classroom=8`(Classroom02) 추가, `SpaceIds.Canonical`(옛 1-1·1-3 → 교실)·`WingOf`. `EventBus.InspectionPlanned/InspectionReported/InspectionStartled`.
> ⓗ **씬에 점검 대상이 아직 없습니다.** 항목마다 `JudgeTarget` ID `inspect.H-1` 형식의 대상을 놓아야 보고 센서가 켜집니다(씬 작업, LFS 잠금 확인 뒤).
> **되살리지 마십시오:** `NightRun.UnvisitedPenalty`(+9)와 `ApplyUnvisitedPenalty`·`UnvisitedPenaltyTests` · 「Tab 중에는 모든 판정이 멈춘다」를 게임 기본으로 두는 것(옛 테스트용 기본값일 뿐) · 「응시 = 화면 중앙 레이의 첫 충돌체」만 쓰는 것 · 「재시작하면 대기 중이던 처벌이 스냅샷 수만큼 다시 나온다」 · 「한 프레임 두 축 100이면 먼저 계산된 축으로 붙잡힘」.

> **개정: 2026-09-30(15차). 최종 기획서(Claude Docs `ebc9e369-d2e8-491c-ba8a-7b071e56a19e`)를 0순위 정본으로 바꾸고, 그 「제작 계획」 1~3단계 — 신뢰 전용 구간·밤 시계·붙잡힘 뒤 재시작·경고와 처벌 — 를 코어에 넣었습니다. 씬·프리팹·에셋은 고치지 않았습니다. EditMode 413개(새 테스트 `NightFlowTests.cs`).**
> ⓐ **신뢰는 전용 경계 15/30/45/65**로 구간을 읽습니다(`Bands.OfTrust`·`Bands.OfAxis`·`Bands.TrustLowerBound`, 배열 `TrustLowerBounds`). 감각 축은 그대로 25/50/75/90. `FearAxisSystem.GetBand`·`BandResolver.Target`·`ParadoxDirector.DailyQuota` 모두 `OfAxis`/`OfTrust`를 거칩니다. `ParadoxDirector.MinTrust` = **15**.
> ⓑ **밤 시계** `NightClock`(`Scripts/Direction/NightClock.cs`): 근무 시작부터의 게임 분 0~240, 실시간 15분(16배). 판정 00:16~03:30, 이완 01:52~02:16 제외 · 호출1 01:00 · 호출2/체크포인트 02:16 · 슬롯 C 03:08. `NightClockTracker`가 경계를 한 번씩 알립니다. `NightRun.JudgingWindowEnabled`(기본 false, 구동기가 켬)가 켜지면 판정 정지 중에는 상태 신호(공간·구역·손전등·Tab)만 넘기고 Tick은 건너뜁니다.
> ⓒ **재시작**: `NightRun.RestartAfterCapture()` — 체크포인트(`SignCheckpoint`, 이완 구간·밤당 1회) 또는 밤 시작 스냅샷(`NightSnapshot`)으로 돌아가 감각 축 = max(min(스냅샷, 40), 스냅샷 − 10k), 신뢰·연출 도달값(`BandResolver.RestoreReached`)·경고는 스냅샷 그대로, **덱은 다시 뽑지 않음**. 5번 재시작한 밤에 또 붙잡히면 결근(`NightOutcome.Absent`, 감각 min(현재, 60)). k≥4면 `LeaveVoluntarily()`. 규칙 수치는 `RestartPolicy` 한 곳.
> ⓓ **경고**: `WarningLedger` — `NightRun.AddWarning(n, source)`, 3개마다 처벌 1이 대기, 판정 구간의 다음 `SpaceEntered`에서 가장 높은 감각 축(동점 청각>조도>배치) +15. **처벌·04:00 정산·판정 정지 중 델타는 95(`Deltas.SoftCap`)에서 멈춥니다** — 그것들로는 붙잡히지 않습니다(`FearAxisSystem.SoftCap`). 새 이벤트 `EventBus.WarningsChanged`·`Punished`·`NightRestarted`.
> ⓔ `NightRunDriver`가 판정 시간창을 켜고, GameTime의 절대 분을 **(현재 − 시작) × 240 / (종료 − 시작)**으로 환산해 넘깁니다. `NightRunDriver.MatchDesignPace`(기본 true)면 `SetTimeMultiplier`로 한 밤을 실시간 15분에 맞춥니다. **GameTime·태블릿 표시·재시작 때 씬 재로딩과 시계 되돌리기는 김진선님 코드라 손대지 않았습니다**(§12.2).
> ⓕ 신뢰 델타는 준수 +2 · 위협 성공 +3 · 역보고 +3 · 역설 지킴 +3 · 변조 지킴 +3(`TrustParadoxKept`·`TrustVariantKept`).
> **되살리지 마십시오:** `MinTrust = 25` · 신뢰에 감각 축 경계(25/50/75/90)를 쓰는 것 · `TrustParadoxIgnored`(+2)·`TrustVariantOriginalKept` · 「재시작하면 덱을 다시 뽑는다」 · 「처벌·정산으로 붙잡힐 수 있다」 · 「연출 구간은 어떤 경우에도 내려가지 않는다」(재시작·결근의 `RestoreReached`만 예외) · 기획서 `1c5c811a-…`를 0순위로 보는 것.

> **개정: 2026-09-30(14차). 새 기획서(Claude Docs)를 정본으로 올리고, 그 기획서대로 코어 수치를 바꿨습니다 — 7단계 개편의 1단계(§2.2·§2.3·§2.3″). 씬·에셋은 고치지 않았습니다. EditMode 358/358 통과(옛 판정 테스트 6개를 지우고 새 것 2개를 더했습니다).**
> ⓐ **구간 경계 25/50/75/90**(0–24 · 25–49 · 50–74 · 75–89 · 90–99). `Bands.LowerBounds` 한 곳.
> ⓑ **축마다 값이 둘입니다.** **생존 수치**(`FearAxisSystem`) — 붙잡힘(100)에만 쓰고, 정확한 보고·밤 재시작으로 **내려갈 수 있습니다**(`FearAxisSystem.Lower`, 신뢰는 못 내림). **연출 구간**(`BandResolver`) — max(도달 구간, 일차 하한)이고 **절대 내려가지 않습니다**(도달값 `Peak` 래칫). 연출·카드 자격·하루 덱·역설 상한은 `BandResolver.Shown`(값=생존 수치, 구간=연출 구간)을 읽습니다.
> ⓒ **일차 하한은 이제 구간입니다**: Band0/Band1/Band1/Band2/Band3(1~5일차). **생존 수치를 올리지 않습니다** — `DayFloor.Apply`는 없어졌고 `NightRun.BeginNight`이 `_bands.SetDayFloor(DayFloor.Of(Day))`를 덱보다 먼저 부릅니다.
> ⓓ **`Deltas`**(`Scripts/Core/Deltas.cs`)에 새 수치 표를 모았습니다 — 위반 +12 · 위협 실패 +20 · 점검 수칙 위반 +6 · 놓침 +8 · 정확 보고 −5(축·밤당 −10 한도) · 경고 3회 처벌 +15 · 재시작 −10 · 신뢰 +2/+3/+3/+2/+3. 아직 1단계라 **정의만** 있고 보고·경고·재시작 경로는 2~3단계에서 붙습니다.
> ⓔ `ParadoxDirector.MinTrust` 24 → **25**(Band1 하한과 같아야 함). 옛 24장 카드 테스트의 경계값(47/48, 71/72)은 49/50, 74/75로 옮겼습니다 — 옛 카드는 4단계에서 통째로 교체됩니다.
> **되살리지 마십시오:** 구간 경계 0~23 / 24~47 / 48~71 / 72~89(24 배수) · 일차 하한 곡선 0/0/12/24/48/72(축 값을 올리던 것) · `DayFloor.Apply` · `BandResolver.Resolve`/`FallMargin`(히스테리시스, 하강 −5) · 「축은 줄지 않는다」(생존 수치는 보고·재시작으로 줄어듭니다; 줄지 않는 것은 연출 구간과 신뢰) · 「Band0에서 하루 쿼터만큼 어기면 다음 구간에 닿아야 한다」·「구간 경계는 위반 델타의 배수」 테스트(하한이 연출을 밀어 올리므로 조건이 없어졌습니다) · `MinTrust = 24`.

> **개정: 2026-09-30(13차). 엠비언트 재생기와 CCTV 소리를 넣었습니다(§3.8). 씬 파일은 고치지 않았습니다. EditMode 362/362 통과.**
> ⓐ **`AmbiencePlayer`가 플레이할 때 스스로 설치됩니다**(`FPController`가 있는 씬). 공간 룸톤 6종(복도·교실·과학실·화장실·도서관·경비실)을 크로스페이드하고, 그 공간의 **청각축 표시 구간**만큼 불안 레이어 1~4를 쌓고, 공간별 원샷을 플레이어 주변 6~14m에서 3D로 냅니다.
> ⓑ 소리 파일은 **`Resources/Ambience/…`, `Resources/Cctv/cctv_*`에서 이름으로** 읽습니다. 설정은 `Resources/AmbienceConfig.asset`. 파일이 없으면 그 겹만 빠지고 경고 한 줄. 파일 36개(코드 합성)는 2026-09-30 낮에 프로젝트에 모두 넣었습니다 — 가져오기 규칙 적용·「36개 전부 있습니다」 확인.
> ⓒ `CctvSystem`에 소리: 모니터 험(3D 루프), 채널 전환 「틱-지직」(들여다볼 때도), `Signal`이 낮으면 지직 루프. 볼륨은 `CctvConfigSO`의 `humVolume`·`switchVolume`·`staticVolume`.
> ⓓ 가져오기 규칙 `AmbienceAudioImportRules` — 위 두 폴더의 루프는 CompressedInMemory, 원샷은 DecompressOnLoad(Vorbis).
> **되살리지 마십시오:** 「엠비언트 공간은 `SpaceZones` 상자로 판정한다」(도서관·경비실 상자가 없습니다 — 소리는 `AmbienceConfigSO`의 실측 상자) · 「분위기 원샷에 3번 노크」(C1 분필 3획과 헷갈립니다 — 노크는 1·2·4번만) · 「분위기 소리가 판정 신호를 보낸다」(§2.7).

> **개정: 2026-09-30(12차). 경비실 CCTV를 넣었습니다(§3.7). 씬 파일은 고치지 않았습니다. EditMode 362/362 통과.**
> ⓐ **`CctvSystem`이 플레이할 때 스스로 설치됩니다**(`PlayerInteractor`와 같은 자동 설치). 경비실 `Old CRT Monitor`에 화면 쿼드를 붙이고, 카메라 5대(복도·교실·과학실·화장실·도서관)를 만들어 **렌더 텍스처 하나**에 고른 채널만 그립니다.
> ⓑ 설정은 `Resources/CctvConfig.asset`(`CctvConfigSO`) 하나. 채널 자리는 인스펙터의 [씬 뷰로 보기]/[현재 씬 뷰로 저장]으로 고칩니다.
> ⓒ **레이어를 새로 만들지 않았습니다.** 카메라별 적외선 조명과 「CCTV에만 보이는 물체」(`CctvOnlyVisible`)는 URP `beginCameraRendering`/`endCameraRendering`에서 켰다 끕니다 — 프로젝트 설정 변경 없음.
> ⓓ `InteractionHud`에 **`ExternalPrompt`/`ExternalHot`** 정적 속성을 넣었습니다. 문 프롬프트가 없을 때만 이 줄이 뜹니다(「[E] CCTV 보기」).
> ⓔ 이번에 **뺀 것**: 녹화 영상 채널 · 플레이어 본인이 화면에 보이는 연출 · 6번째 채널 · 경비실 뒷모습 채널 · CCTV와 실제가 어긋나는 연출. 판정(K1·K2) 연결도 아직 없습니다 — 이벤트만 열어 두었습니다.
> **되살리지 마십시오:** 「CCTV 화면 증폭 2.4 · 노이즈 0.12 · 적외선 4」(하얗게 날아가고 도서관이 알갱이에 묻혔습니다 → 1.3 · 0.035 · 2.5, 화장실만 1.2) · 「CCTV는 전용 레이어가 있어야 한다」 · 「CCTV 카메라는 씬에 둔다」.

> **개정: 2026-09-22(11차). 단서 배선을 개통하고, 발신기가 없는 신호 둘을 하네스가 대신 보내게 했습니다. EditMode 362/362 통과.**
> ⓐ **`SpaceAnomalyTable.asset`을 `Resources/`로 옮겼습니다.** `AnomalyCueDirector`가 `Resources.Load`로 찾는데 `ScriptableObjects/`에 있어 그 폴백이 **언제나 null**이었습니다 — 씬에 표를 손으로 꽂지 않으면 단서를 한 건도 못 보냈습니다.
> ⓑ **`Resources/CueBindingTable.asset` 생성**(38줄). 빌더에 표 본문이 이미 다 있었고 에셋만 없었습니다. 「카드가 기다리는 판정 ID를 아무도 안 보낸다」 경고가 **0건**입니다.
> ⓒ **하네스 ⑦ 신설 — 없는 발신기를 1초 응시로 대신**(§3.4). `ModelObserved`는 프로젝트에 발신기가 **아예 없었고**(조우가 영영 관찰 완료되지 않았습니다), `DoorAutoOpenObserved`는 문을 여는 연출이 없었습니다.
> ⓓ **장면에 묶인 카드는 그 장면이 깔린 날에만 덱에 듭니다**(§4.3′). S5·T3를 Band0으로 내리면서 「자격이 곧 그 장면이 깔리는 날」이던 우연한 일치가 사라져, 장면 없는 날에 덱 한 자리를 헛되이 쓰고 있었습니다.
> ⓔ 실측: 걸어서 시작되는 카드가 **4장 → 23장**. 남은 하나는 C1이며 청취 구역 `cls11.door.outside`가 씬에 없어서입니다.
> **되살리지 마십시오:** 「`SpaceAnomalyTable`은 `ScriptableObjects/`에 둔다」 · 「바인딩 표는 기획 확인(Q13) 대기라 채울 수 없다」 · 「`ModelObserved`는 누군가 보내고 있다」.

> **개정: 2026-09-22(10차). 카드 24장의 발동 자격을 다시 짰습니다. EditMode 361/361 통과.**
> ⓐ **「축당 Band0 개방 정확히 3장」 불변식을 폐기**했습니다(§2.3′). 장수가 아니라 **위반 델타의 합**이 데드락 조건이었고, 「정확히 3장」이 1·2일차 덱을 사실상 고정시켰습니다 — 실측으로 2일차 6장 중 평균 4장이 1일차와 같았습니다.
> ⓑ **16장의 자격 창을 넓혔습니다.** Band0 풀이 축당 3·3·3 → **4·4·7**입니다. 2일차 덱 조합이 200회차에 **174가지**, 1일차와 겹치는 카드가 **평균 0.64장**이 됐습니다.
> ⓒ **H6이 살아났습니다.** 배치 Band4(90↑) 전용이라 준수만 하는 플레이어에게 **회차당 0.00회**였습니다 — 24장 중 유일하게 죽은 카드였습니다. Band3~4로 내렸습니다.
> ⓓ **S5·T3를 Band0으로 내려 조우 일차 제약이 사라졌습니다**(§4.3″). 8장면이 1일차(S-A 고정)를 뺀 **어느 날에나** 깔립니다 — 기획서 v6 §4를 온전히 따릅니다.
> ⓔ **C1은 4일차까지, T1은 5일차까지** 살아남습니다(상한 한 칸씩 위로). **S4는 18칸 창(72~89) → 52칸(48~99)**.
> ⓕ 실측 재검증: 전부 위반 **4일차 사망 100%** · 전부 준수 **5일 끝 72/72/72 · 신뢰 100** · 죽은 카드 0장.
> **되살리지 마십시오:** 「축당 Band0 개방은 정확히 3장」 · 「H6은 Band4 전용」 · 「S5는 배치 Band1~」 · 「T3는 배치 Band2~」 · 「S4는 Band3 창형」 · 「C1 상한은 Band1」 · 「T1 상한은 Band2」 · 「S-B는 3일차부터 · T-A는 4일차부터」.

> **개정: 2026-09-22(9차). 시나리오 기획서 v6(`야간근무_시나리오_엔딩_사망_로딩문구_최종제안_v3.md`)을 반영했습니다. EditMode 360/360 통과.**
> ⓐ **조우 배분이 고정표에서 「회차마다 다시 뽑기」로 바뀌었습니다**(§4.3″). 기획서 §4 「날짜별 공간을 고정하지 않는다」. 전소진·1일차 S-A·발견형 3·4·5일은 그대로 지킵니다.
> ⓑ ~~S-B는 3일차부터, T-A는 4일차부터~~ — **10차에서 해소**됐습니다. 두 카드를 Band0으로 내려 제약이 필요 없어졌습니다.
> ⓒ **그날 배분이 이월보다 먼저 자리를 가집니다.** 거꾸로면 상한 3장에 걸려 회차 뒤쪽 장면이 영영 안 깔립니다 — 실측 6/8.
> ⓓ **근무 종료가 04:00로 확정**됐습니다(기획서 §3-4 「04:00 유예안 삭제」). `PlaySystems.prefab`을 6시 → 4시로 고쳤습니다. 배속 20 그대로면 하루 12분입니다.
> ⓔ **S1 본문 교체**(기획서 §3-3). 「첫 근무는 …」은 되살리지 마십시오.
> ⓕ **신뢰 100은 사망이 아님이 기획으로 확정**됐습니다(기획서 §5-1). **Q12 종결**(§12.2).
> ⓖ **로딩 문구 13 + 4**. `LoadingTipTable`에 해금 구분이 생겼습니다(§8-8).
> ⓗ **`DayBriefText` 신설** — 태블릿 업무 상태 문구. 판정도 델타도 없습니다(§4.3‴).
> **되살리지 마십시오:** 「1일 S-A · 2일 C-A · 3일 T-B+H-A · 4일 C-B+T-A · 5일 H-B+S-B 고정」 · 「이월분을 배분보다 먼저 넣는다」 · 「근무는 6:00에 끝난다」 · 「신뢰 100 게임오버」 · 「로딩 문구는 조작 안내만」.

> **개정: 2026-09-22(8차). 씬을 고치지 않고 판정·조우를 걸어서 시험할 수 있게 하는 런타임 하네스를 넣었습니다.**
> ⓐ **`NightDutyTestHarness` 신설**(§3.4). `Assets/3.1. Programmer_lee/02 Scripts/`. 플레이 중에만 센서·문·판정 대상·조우 대상·게임 시계를 채웁니다. **씬 파일은 한 글자도 바뀌지 않습니다.**
> ⓑ **준비는 `Awake`에서 합니다.** `NightRunDriver.Start`가 밤을 여는 순간 판정이 대상 참조를 검사하므로 그보다 앞서야 합니다. `Start`에 두었더니 실제로 「대상 참조 누락」 경고 3건이 났습니다.
> ⓒ **빠진 대상은 `RuleSO.CollectReferences`로 모읍니다.** `TargetIds`만 보면 **시작 신호의 대상**(S2 `science.glass.break`)과 성공·실패·취소 조건이 가리키는 ID가 빠집니다. 판정이 검사하는 목록과 같은 것을 써야 합니다.
> ⓓ **런타임 생성물에 `HideFlags.DontSave`를 붙이지 마십시오**(§5.5-23). 플레이를 꺼도 파괴되지 않고 쌓입니다 — 두 번 플레이에 잔재 80개를 실측했습니다.
> ⓔ 실측(2026-09-22, `PlayScene`): 부착 11 · 문 8 · 만든 대상 42 · 콜라이더 4 · **미판정 0 · 에러 0**, 플레이 종료 후 잔재 0.
> **되살리지 마십시오:** 「하네스 준비는 `Start`에서 한다」 · 「빠진 대상은 `TargetIds`만 보면 된다」 · 「런타임 오브젝트는 `DontSave`로 만든다」 · 「씬을 고쳐야만 걸어서 시험할 수 있다」.

> **개정: 2026-09-22(7차). `EncounterDirector`를 넣고 `DayDirector`의 조우 TODO 2건을 채웠습니다. EditMode 358/358 통과.**
> ⓐ **`EncounterDirector` · `EncounterTableSO` 신설**(§4.3″). 조우 8장면 배분 · 필연 조우 3단계 · G 7통 + N1.
> ⓑ **밤 시작 순서가 바뀌었습니다**: `DayFloor.Apply` → **조우 배정** → `LoadDeck` → `Paradox.BeginNight` → `_book.BeginNight`. 조우가 덱보다 먼저여야 「그날 조우 공간 카드 1장」을 보장할 수 있습니다.
> ⓒ **`DayDirector`의 조우 TODO 2건이 채워졌습니다** — 조우 공간 카드 보장 · S-B가 깔린 날 S2 제외.
> ⓓ `Resources/EncounterTable.asset`이 생겼습니다. **문구는 전부 자리표시자**이며 기획서 10-3절이 정본입니다.
> ⓔ **씬에 조우 대상 27종이 필요합니다**(§4.2). 현재 0개.
> **되살리지 마십시오:** 「조우는 하루 한 장면」 · 「이월분을 배분표보다 먼저 넣는다」(8장면 전소진이 깨집니다) · 「이월된 장면은 접근 1단계부터 다시」 · 「덱을 조우보다 먼저 짠다」 · 「N1의 목적지를 표에 적는다」.

> **개정: 2026-09-21(6차, 5차의 후속). 문서·주석 전수 검사로 찾은 오차를 바로잡고 감시 테스트를 넣었습니다. EditMode 339/339 통과.**
> ⓐ **`DutyLogEntry.Mark`의 실제 버그를 고쳤습니다** — 미방문인데 위반이 난 줄이 「지시를 따름」으로 찍히고 있었습니다. 이제 **가지 않았으면 문자를 받았어도 「어김」**입니다.
> ⓑ **`Vocabulary.cs`·`BandTableSO.cs`의 `Band` 주석이 옛 경계(0~24 / 25~49 …)였습니다.** 구간을 가장 많이 참조하는 자리라 치명적이었습니다. 고쳤습니다.
> ⓒ Band3은 **좁습니다(18칸)**. 5차에서 「한 칸 넓다」고 적은 것은 틀렸습니다 — Band0~2는 각 24칸입니다.
> ⓓ **`DesignDriftTests` 9개 신설**(§3.3). 에셋↔빌더 드리프트와 이 파일의 옛 경계를 테스트가 잡습니다.
> **되살리지 마십시오:** 「미방문이어도 문자를 받았으면 지시를 따름」 · 「Band3은 한 칸 넓다」 · 「Band3은 15칸」 · 「C5·S4는 켜진 등 개수를 조건으로 쓴다」 · 「센서·단서 시스템은 아직 없다」 · 「편성표는 임시 편성표다」.

> **개정: 2026-09-21(5차). 밸런스 재설계 1차 구현을 반영했습니다.**
> ① **구간 경계가 25/50/75 → 24/48/72로 바뀌었습니다**(§2.3). 이 파일의 이전 판에 적혀 있던 「0–24 / 25–49 / 50–74」를 다시 쓰지 마십시오.
> ② **준수 신뢰 델타가 +2/+3/+4 → +4/+5/+8**입니다(§2.2·§2.9). 이제 신뢰 Band3·Band4가 실재하는 구간입니다.
> ③ **카드 7장(C2·C5·S5·T2·T3·T5·T6)의 발동 자격이 재배치**됐습니다(§2.3′). 빌더와 에셋을 **둘 다** 고쳤습니다.
> ④ **`DayFloor`(일차 하한)와 `DayDirector`(하루 배정)가 새로 생겼습니다**(§2.3″·§4.3).
> ⑤ **`NightDeckTableSO`는 이제 「일차별 덱」이 아니라 「카드 풀」**입니다(§4.3′). 뜻이 바뀌었습니다.
> ⑥ **미방문 미판정에만 감각축 +9**를 되살렸습니다(§2.5). 기획서 D절의 유일한 예외입니다.
> ⑦ 결산 표기가 **`DutyMark` 3종**이 됐습니다(§2.9). 데이터 계층까지만 구현.
> **되살리지 마십시오:** 「Band0 = 0~24 / Band1 = 25~49 / Band2 = 50~74 / Band3 = 75~89」 · 「준수 신뢰 +2~+4」 · 「신뢰 Band3·Band4는 도달 불가 구간」 · 「`H6` 배치 +25」 · 「`NightDeckTable`은 일차별 고정 덱」 · 「미판정은 전 유형 델타 0」 · 「하루 덱은 사람이 편성표에 손으로 적는다」.

> **개정: 2026-09-20(4차). 기획자가 올린 `야간근무_공간별_지침록 9.20V.html`을 반영했습니다.**
> ① **네 공간의 조도표에서 「켜진 등 개수」가 삭제**됐습니다. 조도는 색온도 하나로만 표현하며, C5·S4는 등 개수가 아니라 **색온도 구간**을 조건으로 씁니다(§2.4). 씬의 등 개수가 부족하다는 이전 지적도 함께 소멸했습니다.
> ② **카드 24장의 본문과 역설 23개의 문구를 9.20V 판으로 교체**했습니다(에셋·`CorridorCardBuilder`·`RoomCardBuilder` 모두 반영 완료). **수치 구간·유예/응시 시간·거리·델타는 그대로**입니다 — 9.20V 머리말이 명시했습니다.
> ③ **전시형 인체모형**(과학실 환경 오브젝트)이 조우 모형과 분리됐습니다. 과학실 배치축은 이 전시형을 향하는 **의자의 방향·위치**로 읽습니다(§2.7).
> ④ 화장실에서 **거울·플레이어 반사를 쓰지 않습니다.** 청각은 물방울 개수 대신 물 내림·호칭·칸 안쪽 소리입니다(§2.8).
> **되살리지 마십시오:** 「조도 구간별 등 개수(8/6/4/2/0 등)」 · 「C5·S4는 켜진 등 개수를 조건으로 쓴다」 · 「씬의 등이 기획값보다 부족하니 보충해야 한다」 · 「Band3 = 2000K 적갈」(9.20V는 **짙은 주황**) · 「화장실 물방울 개수 세기」 · 「거울 반사 연출」.

> **개정: 2026-09-20(3차). 2026-09-20 중간기획서(40쪽, NCAI3기 2조)를 1순위 정본으로 삼아 개정했습니다.**
> ① 기획서가 태블릿 문자를 **31개**(발견유도 G 7 + 재방문 N1 1 + **역설 P 23**)로 확정해, 이전 판의 「문자 12개 · 역설 4쌍」 서술이 다음 세션을 23쌍에서 4쌍으로 되돌릴 위험이 있었습니다.
> ② 기획서 B절이 「04:00 무조건 자동 종료, 종료 요청·경비실 복귀 없음」을 확정해 §12.1의 `RequestEndNight` 수락 조건 작업이 소멸했습니다.
> ③ 기획서 D절이 역설 미도달·거절 벌점을 전 유형 폐기해 §2.9의 「거절 → 배치 +6」 순환이 성립하지 않게 됐습니다.
> ④ 문 3종·손전등·응시·근접 신호를 **시스템(이성현)이 직접 구현**하기로 정해 「플레이어 센서 담당 미정」이 해소됐습니다(§10).
> **되살리지 마십시오:** 「문자 12개·역설 4(P1↔S2·P2↔H4·P3↔C1·P4↔T6)」 · 「P형 유효 미도달 배치 +6」 · 「하루 1쌍·회차 2쌍·P ID 회차 1회」 · 「동시 후보 P3→P2→P1→P4」 · 「`RequestEndNight` 수락 조건(필수 점검+조우 완료)」 · 「기준선 278/278」 · 「`MessageDirector`/`ParadoxResolver`를 새로 만든다」.

> 개정: 2026-09-17(2차). 인수인계서(`HANDOFF_야간근무_인수인계.md`, 9/17 Lee 세션 판)의 구현 현황을 반영했습니다.
> 판정 코어·카드 24장·판정 디버그 패널·EditMode 테스트 264개가 이미 있으며, 이전 판의 「Rules/Direction 비어 있음」 서술은 틀렸습니다.
> 폐기된 결정(`RED_THRESHOLD = 75`, 미방문 페널티 +22, `ComplianceMode`, §0 조항·전화 이벤트, 감쇠, 프로파일링·각인축, 과학실 Band4 등 1개)은 되살리지 마십시오.
> **이 파일과 기획서(2026-09-20)가 어긋나면 기획서가 우선**합니다. 지침록 HTML보다도 기획서가 우선입니다(§1.1). 어긋남을 발견하면 이 파일을 고치십시오.
> **2026-09-17 기획 결정(정본 HTML 미반영):** 신뢰는 포획 조건에서 빠지고, 태블릿 문자↔근무수칙 충돌을 늘리는 데에만 씁니다. (충돌 문자는 2026-09-20 기획서 12절에서 **23쌍으로 확정**됐습니다 — §2.1.) 폐기: 신뢰 100 게임오버, 신뢰에 따른 태블릿 폰트·디자인 왜곡. 정본의 해당 문장(공통 명세 1절·6절 「신뢰 100」)보다 이 결정이 우선합니다(§2.9).

---

## 1. 프로젝트

**야간근무 (Night Duty).** 폐교를 배경으로 한 1인칭 **규칙 준수 공포게임**입니다. 나폴리탄 괴담을 참조합니다.
플레이어는 야간 경비로서 태블릿에 실리는 근무수칙을 지키며 4개 공간을 순찰합니다.
**1회차는 근무 5일이고, 하루는 인게임 00:00~04:00입니다. 04:00이 되면 점검·조우 완료와 무관하게 그 자리에서 근무가 끝납니다**(기획서 A·B절, §2.1).
**수칙을 어기면 감각축(청각·조도·배치)이 오르고, 그중 하나라도 100에 도달하면 포획 엔딩(게임오버)입니다.**
**수칙을 지키면 신뢰가 오르고, 신뢰가 높을수록 태블릿 문자와 근무수칙의 충돌(역설)이 늘어납니다.** 신뢰는 게임오버를 일으키지 않습니다.

- 엔진: Unity **6000.3.17f1** · URP **17.3** (Forward+) · New Input System 1.19
- 인원: 2인 개발 (시스템 / 클라이언트, §10)
- 이 `horror house/` 폴더가 **Unity 프로젝트 루트**이고, git 저장소 루트는 한 단계 위(`NCAI_BrokenCompass/`)입니다.
  저장소 루트에도 `Assets/` 폴더가 있지만 Unity 프로젝트가 아닙니다. 루트를 Unity로 열지 마십시오.
- 빌드·린트 도구는 없습니다. 검증은 Unity 에디터에서 합니다(§3).

### 1.1 문서 우선순위 — 충돌하면 위가 이깁니다

| 순위 | 문서 | 위치 | 역할 |
|---|---|---|---|
| **0** | **야간근무 최종 기획서 — Claude Docs (2026-09-30 확정본)** | Claude Docs 문서 `ebc9e369-d2e8-491c-ba8a-7b071e56a19e` (예전 초안 `1c5c811a-…`는 쓰지 않음). 팀 공유용 변경 요약은 「야간근무 기획 변경 공유」 `594ee326-e338-422c-a381-74a9492e3f3c` | **최상위 정본(2026-09-30 사용자 결정).** 제작 순서는 이 기획서의 「제작 계획」 12단계를 따릅니다. 아래 문서들과 부딪히면 이것이 이깁니다. 공간 6곳(복도·교실·과학실·화장실·도서관·경비실), 수칙 H1–4·C1–5·S1–5·T1–5·L1–5·K1–4와 태블릿 문구, 점검 16항목·점검 이상 4틀, 구간·생존 수치/연출 구간·델타·경고·재시작, 역설·변조. 아래 1~2순위 문서는 이 기획서가 다루지 않는 판정 절차에만 씁니다 |
| **1** | **중간기획서 (2026-09-20, 40쪽)** | 기획팀 공유 PDF (읽은 추출본은 `../Docs/Claude outputs/`) | **기획 정본.** 스코프·근무 시간·신뢰↔역설 상한·역설 23쌍 총괄표(12절)·조우 8장면·이상현상 5구간표·조작 |
| **1′** | **`야간근무_공간별_지침록 9.20V.html` (2026-09-20)** | `../Docs/Claude outputs/` | **카드 문구·이상현상 5구간표의 정본.** 24수칙 본문과 역설 23개 문구를 이 판으로 교체했고, 네 공간의 이상현상 표를 다시 썼습니다. 중간기획서와 **같은 날짜**라 우열이 아니라 **분담**입니다 — 스코프·근무 시간·신뢰↔역설 상한·조우 8장면·조작은 중간기획서가, **카드 본문·역설 문구·공간별 이상현상 구간 내용은 9.20V가** 정본입니다 |
| 2 | `야간근무_공간별_지침록_개발명세반영본.html` (2026-09-12) | `../Docs/Claude outputs/` | **판정 절차의 상세 정본.** 카드별 「개발 판정 상세」, 공통 개발 명세 1~7절. **문구·이상현상은 9.20V가, 스코프·상한은 기획서가 이깁니다.** 이 판에만 있는 것은 판정 절차·검증 방법입니다 |
| ~~2′~~ | ~~`Editor/CorridorCardBuilder.cs` · `Editor/RoomCardBuilder.cs`~~ | — | **2026-10-03(25차) 폐기.** 옛 24장 카드 구현값의 정본이었다 |
| 3 | **`HANDOFF_야간근무_인수인계.md`** | `../Docs/Claude outputs/` | **새 세션이 가장 먼저 읽는 단일 인수인계서.** 결정·함정·진행 상황·다음 작업·결정 대기 |
| 3′ | `다음작업_결정_2026-09-20.md` | `../Docs/Claude outputs/` | 기획서 대조 결과와 이번 주 작업 순서. 이 파일 §12.1·§12.2의 근거 |
| 4 | 이 파일 | `horror house/CLAUDE.md` | 작업 규약 요약 |
| 5 | `형상관리_매뉴얼.md` | `../Docs/` | git/LFS 운영 규칙 |
| — | `게임플로우_작업내역_설정가이드.md` (2026-09-14) | `../Docs/Claude outputs/` | 진선님 게임 흐름(Main→Loading→Play→Result) 설정법. Part 5에 판정 시스템 연결 시 바꿀 곳 |

- **2026-09-30부터 0순위 새 기획서가 모든 충돌에서 이깁니다.** 옛 24장 카드(H·C·S·T 1~6)와 그 테스트는 **2026-10-03(25차)에 폐기**했습니다 — 아래 옛 정본 설명은 **옛 카드의 기록**입니다.
- **정본이 셋이면 정본이 없습니다.** 2026-09-20 기준 정본은 **같은 날짜의 두 문서**이고, 겹치지 않게 분담합니다. **스코프·수치 구간·델타·유예/응시 시간·거리·신뢰↔역설 상한·조우 8장면·조작 = 중간기획서.** **카드 24장과 역설 23개의 문구·네 공간의 이상현상 5구간 내용 = 9.20V.** 9.20V 머리말이 「수치 구간·유예·응시 시간·거리·델타는 유지한다」고 못박았으므로, 9.20V에서 수치를 읽어 쓰지 마십시오. 9/12 판은 판정 절차를 채우는 용도로만 씁니다.
- **수칙 전문·델타·판정 상세를 이 파일이나 코드 주석에 옮겨 적지 마십시오.** 사본이 어긋나면 어느 쪽이 맞는지 알 수 없게 됩니다. 카드를 구현·수정할 때는 정본의 해당 카드 7항목(발생 자격·시작 / 준수 / 위반 / 종료·반복 / 예외·충돌 / 씬 연결·설정값 / 검증 절차·기대 결과)을 직접 읽으십시오. 구현값 요약표는 인수인계서 §6에 있습니다. **역설 23쌍 총괄표(기획서 12절)와 공간별 이상현상 60칸(기획서 H절)도 같습니다** — 이 파일에는 「정본은 기획서 N절」이라고만 적습니다.
- 인수인계서가 이전 문서(9/16 인수인계, 판정코어 연결약속, `NightDuty.Core_현황과_사용법`, 시스템/클라이언트 개발계획서, 4주 스케줄, 8주 절단 제안서, 절단 확정 v2, 구현난이도 판정서, 진행기록·카드분석)를 **모두 대체**했습니다. 해당 파일들은 `Docs/`에 더 이상 없습니다. 다시 찾지 마십시오.
- 이미 내려진 결정은 다시 논의하지 않습니다. 바꿔야 한다면 **사용자에게 먼저 확인**받으십시오.

---

## 2. 확정 게임 규칙 요약

### 2.1 스코프

| 항목 | 확정값 |
|---|---|
| 회차 | **근무 5일** (기획서 A절. 코드 `GameSession.FinalDay = 5`와 일치) |
| 근무 시간 | 인게임 **00:00~04:00**. 시작 지점은 **경비실**. 04:00에 **점검·조우 완료와 무관하게 자동 종료**하며 종료 요청·경비실 복귀는 없습니다(기획서 B절, §2.5-9) |
| 공간 | **4개**: 복도 · 교실(1-1/1-3) · 과학실 · 화장실 |
| 점검 ID | **5개**: 복도 · 1-1 · 1-3 · 과학실 · 화장실 (`SpaceId`와 일치) |
| 근무수칙 | **(옛 기록 — 2026-10-03 폐기)** 24장, 공간당 6장: 복도 **H1~H6** · 교실 **C1~C6** · 과학실 **S1~S6** · 화장실 **T1~T6** |
| 하루 배정 | **6장** (배치 2 · 청각 2 · 조도 2). 1일차만 S1 고정 + 배치 2 · 청각 2 · 조도 1. 옛 배정기는 2026-10-03 폐기(§4.3′) — **옛 기록**, 지금은 새 편성 |
| 인체모형 장면 | **8개**: H-A · H-B · C-A · C-B · S-A · S-B · T-A · T-B |
| 태블릿 문자 | **31개** = 발견유도 **G 7**(G-H1·G-H2·G-C1·G-C2·G-S1·G-T1·G-T2) + 재방문 **N1 1** + 역설 **P 23**(P1~P23) |
| 역설 쌍 | **23쌍.** 짝은 **카드 ID 순서**입니다 — P1↔H1 · P2↔H2 · … · P6↔H6 · P7↔C1 · … · P12↔C6 · P13↔S2 · … · P17↔S6 · P18↔T1 · … · P23↔T6. **S1만 역설이 없습니다**(1일차 고정 카드라 신뢰 구간에 들어가지 못함) |

도서관·탈의실은 이번 스코프에 없습니다.
**23쌍의 짝 수칙 · 따름/거절 델타 · 발송 시점 총괄표는 기획서 12절이 정본입니다.** 이 파일에 옮겨 적지 마십시오(§1.1).

### 2.2 네 개의 축

**2026-09-30 새 기획서 — 축마다 값이 둘입니다.**
- **생존 수치**(`FearAxisSystem`): 0~100. **붙잡힘에만** 씁니다. 오르는 것은 위반·실패, **내려가는 것은 정확한 보고와 밤 재시작**뿐입니다(`FearAxisSystem.Lower`). 감쇠·상시 증가는 없습니다.
- **연출 구간**(`BandResolver`): max(도달 구간, 일차 하한). **절대 내려가지 않습니다.** 도달 구간은 이번 회차에 생존 수치가 닿았던 가장 높은 값(`Peak`)의 구간입니다. 월드 연출·카드 자격·하루 덱·역설 상한은 모두 `BandResolver.Shown`을 읽습니다.
- 수치는 **`Deltas`(`Scripts/Core/Deltas.cs`) 한 곳**에 있습니다.

| 사건 | 축 | 변화 |
|---|---|---|
| 근무수칙 위반 | 그 수칙의 감각 축 | **+12** |
| 위협 대응 실패 | 〃 | **+20** |
| 점검 수칙 위반(가벼운 놀람 연출) | 〃 | **+6** |
| 이상을 정상으로 보고(놓침) | 그 이상의 축 | **+8** |
| 이상을 정확히 보고 | 그 이상의 축 | **−5** (축마다 밤당 −10까지) |
| 정상을 이상으로 보고 · 점검 미완료 | — | **경고 1** · 3회면 처벌 이벤트: 가장 높은 감각 축 **+15** |
| 붙잡혀 그 밤 재시작 | 감각 3축 | **−10**씩 |
| 준수 / 위협 성공 / 역보고 / 역설 지킴 / 변조 지킴 | 신뢰 | **+2 / +3 / +3 / +3 / +3** (줄지 않음) |

- 재시작(−10×k, 하한 min(스냅샷, 40))과 경고·처벌은 15차에 들어갔습니다(`RestartPolicy`·`WarningLedger`). **처벌·04:00 정산·판정 정지 중 델타는 95에서 멈춥니다.** 정확 보고·놓침은 점검 단계에서 붙습니다. 옛 24장 카드는 에셋에 적힌 개별 델타를 그대로 씁니다.
- **청각·조도·배치 중 하나가 100이면 종료 잠금**입니다. **신뢰 100은 종료 잠금이 아닙니다**(값만 100에서 멈춤, `FearAxisSystem.IsTerminal`). 종료 원인 축, 카드/문자 ID, 현재 공간을 기록하고 **종료 요청을 한 번만** 보냅니다. 그 뒤의 미적용 델타와 신규 판정·문자·조우·정산은 모두 중단합니다.
- 델타는 카드마다 고정값입니다. 프로파일링 가중치나 각인축은 없습니다.
- **배치 최대는 +15입니다.** `H6`의 +25는 2026-09-20에 +15로 고쳤습니다(에셋·빌더 모두). 되살리지 마십시오 — H6는 배치 90~99에서만 열리는 카드라 +25면 어기는 즉시 게임오버였습니다.
- 청각·조도·배치는 월드 연출을 움직입니다. **신뢰는 월드에 그리지 않습니다.** 신뢰 구간은 역설 문자 편성(§2.9)의 입력으로만 씁니다.
- **일차 하한은 연출 구간에만 걸립니다**(§2.3″). 생존 수치는 올리지 않고, 신뢰는 대상이 아닙니다.

### 2.3 구간(Band) — 폭이 균일하지 않습니다

```
Band0 = 0–24 · Band1 = 25–49 · Band2 = 50–74 · Band3 = 75–89 · Band4 = 90–99 · 100 = 붙잡힘
```

- **신뢰는 전용 경계 15/30/45/65를 씁니다**(Band0 0–14 · Band1 15–29 · Band2 30–44 · Band3 45–64 · Band4 65~). 축을 모르는 곳에서는 `Bands.OfAxis(axis, value)`, 신뢰만이면 `Bands.OfTrust`. 경계 숫자는 `Bands.TrustLowerBounds` 한 곳입니다.
- **`value / 20` 같은 균등 분할 계산을 하지 마십시오.** 반드시 `Bands.Of(value)`를 씁니다.
- **2026-09-30 새 기획서로 25/50/75/90입니다.** 24 배수 경계는 「위반 두 번이면 반드시 다음 구간」을 맞추려던 장치였는데, 이제 연출 구간은 일차 하한과 래칫이 밀어 올리므로 필요 없습니다.
- **Band3(75–89)·Band4(90–99)가 좁은 것은 의도입니다.** 붙잡히기 직전 구간이 길면 긴장이 풀립니다.
- **연출은 `Bands.Of`가 아니라 `BandResolver`의 연출 구간을 봅니다.** `Bands.Of`는 원시 표일 뿐입니다.
- 경계 숫자는 **`Bands.cs`의 `LowerBounds` 배열 한 곳에만** 있습니다. if 사슬로 흩뿌리지 마십시오.
- 90~99 표현이 엔딩 직전 경고를 맡습니다. 별도 경고 시스템은 추가하지 않습니다.
- 옛 카드의 발동 자격(`eligibleBand`)은 옛 판정 책과 함께 2026-10-03에 폐기했습니다. 새 수칙의 편성·연출은 **연출 구간**(`BandResolver.Shown`)을 봅니다. 공통 「붉게 보이면」 임계값은 없습니다.

### 2.3′ 카드 발동 자격 — 데드락 조건은 장수가 아니라 델타의 합입니다

> **2026-09-30: 이 절은 옛 24장 카드 기준 기록입니다.** 옛 24장 카드는 2026-10-03(25차)에 폐기했습니다. 연출 구간이 일차 하한으로 밀려 올라가므로 아래 「데드락」 조건은 더 이상 성립 조건이 아닙니다(해당 감시 테스트는 지웠습니다).

**배정 알고리즘이 성립하는 조건.** 하루 6장을 청각 2·조도 2·배치 2로 뽑으려면 어느 구간에서든 축당 최소 2장이 열려 있어야 하고, **그 2장을 다 어겼을 때 다음 구간 하한(24)에 닿아야** 합니다. 닿지 않으면 그 축은 영영 Band0에 묶입니다.

| 축 | Band0에서 열리는 카드 | 불리한 2장의 합 |
|---|---|---|
| 청각(8장) | **C1 · S2 · S3 · T2** | 12+12 = 24 ✔ |
| 조도(5장) | **C5 · H3 · S6 · T5** | 12+12 = 24 ✔ |
| 배치(10장) | **C2 · C6 · H1 · H4 · S5 · T1 · T3** | 12+12 = 24 ✔ |

> **2026-09-21판의 「정확히 3장」은 폐기했습니다.** 데드락을 막으려던 규칙인데, 실제 조건은 위의 합이지 장수가 아니었습니다. 그리고 3장 상한이 1·2일차를 고정시켰습니다 — 준수만 하는 플레이어의 축은 일차 하한에 정확히 머무르므로 「일차 = 구간 = 카드 세트」가 되고, 실측으로 **2일차 6장 중 평균 4장이 1일차와 같았습니다.**

**2026-09-22에 자격을 옮긴 카드는 16장**입니다.

| 카드 | 이전 | 지금 | 왜 |
|---|---|---|---|
| C1 | 청각 Band0~**1** | 청각 Band0~**2** | 4일차에 0%였습니다. 첫 수칙이 사라지는 지점을 5일차로 밀었습니다 |
| C2 · C5 · H4 · S3 | Band**1**~4 | Band**0**~4 | Band0 풀 보강 |
| C3 · H2 · T4 | Band**2**~4 | Band**1**~4 | 한 칸씩 아래로 |
| C4 · H5 · T6 | Band**3**~4 | Band**2**~4 | 5일차에만 나오던 카드들 |
| **H6** | 배치 Band**4**~4 | 배치 Band**3**~4 | **회차당 0.00회**였습니다 — 하한이 5일차에 72까지만 가므로 90은 위반 누적 없이 못 닿습니다 |
| **S5** | 배치 Band**1**~4 | 배치 Band**0**~4 | 트리거 ID가 조우 장면 `scene.sb`라, 자격이 24↑면 S-B를 3일차 이전에 깔 수 없었습니다 |
| **T3** | 배치 Band**2**~4 | 배치 Band**0**~4 | 같은 이유(`scene.ta`, 48↑ → 4일차 이전 불가) |
| S4 | 조도 Band3~**3** (18칸) | 조도 Band**2**~**4** (52칸) | 조도 카드가 다섯 장뿐인데 하나가 좁은 창에 갇혀 실질 풀이 넷이었습니다 |
| T1 | 배치 Band0~**2** | 배치 Band0~**3** | 5일차(하한 72)에 0%였습니다. 장기 카드가 마지막 날 사라졌습니다 |

**상한형은 둘 남았습니다**: C1(청각 0~71) · T1(배치 0~89). 전부 하한형이면 후반 풀이 초반의 상위집합이 되어 반복감이 생깁니다. S4는 창형에서 하한형으로 바뀌었습니다.

**실측(준수만 하는 플레이어, 200회차)**

| | 이전 | 지금 |
|---|---|---|
| 2일차 덱 조합 | 사실상 고정 | **174가지** |
| 2일차 ↔ 1일차 겹침 | 평균 4장 | **평균 0.64장** |
| 회차당 등장 최저 | **0.00회**(H6) | **0.70회** |
| 죽은 카드 | 1장 | **0장** |

- **2026-10-03(25차): 옛 24장 카드·카드 빌더·하루 배정기와 이 절의 감시 테스트(에셋↔빌더 드리프트·죽은 카드·조우 묶인 카드·축 쿼터)를 모두 지웠습니다.** 위 표는 기록입니다.
- `IsEligible`은 **트리거 신호를 받는 순간 실시간 검사**입니다. 덱에서 걸러지는 것이 아니라 덱에 있어도 안 열립니다. 밤 중에 축이 오르면 그 밤 안에 열릴 수 있습니다.

### 2.3″ 일차 하한 — 「잘할수록 아무 일도 안 일어남」을 막는 장치

`DayFloor`(2026-09-30 새 기획서로 개정). 근무일마다 **연출 구간의 바닥**을 정합니다. **생존 수치는 올리지 않습니다.**

일차 하한 곡선: **Band0/Band1/Band1/Band2/Band3**

| 근무일 | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| 연출 하한 | Band0 | Band1 | Band1 | **Band2** | **Band3** |

- 연출 구간 = max(도달 구간, 일차 하한). **한 번 오른 연출 구간은 내려가지 않습니다**(하한이 낮아져도, 생존 수치가 깎여도).
- **신뢰는 대상이 아닙니다.** 신뢰는 준수로만 오릅니다.
- **반드시 덱을 짜기 전에 겁니다.** 카드 자격이 연출 구간을 보고 정해지므로 `NightRun.BeginNight`이 `_bands.SetDayFloor(DayFloor.Of(Day))`를 `LoadDeck`보다 먼저 부릅니다.
- **왜 필요한가.** 잘 지키는 플레이어도 날이 갈수록 학교가 나빠지는 것을 봐야 합니다. 반면 죽음(붙잡힘)은 플레이어가 한 일로만 다가오게 — 그래서 생존 수치는 건드리지 않습니다.
- **Band4(90~99)는 하한으로 주지 않습니다.** 그 구간은 「당신이 어겨서 여기까지 왔다」는 뜻으로 남겨 둡니다.
- 곡선을 바꾸려면 **`DayFloor.cs`의 `Floors` 배열만** 고칩니다.
- 2026-09-21의 「하한이 축 값 자체를 올린다」는 2026-09-30 새 기획서로 폐기됐습니다.

### 2.4 조도 — 네 공간 공통 색온도표 하나뿐

| 구간 | 색온도 |
|---|---|
| Band0 | 6500K 백색 |
| Band1 | 4500K 옅은 노랑 |
| Band2 | 3200K 주황 |
| Band3 | 2000K 짙은 주황 |
| Band4 | 붉은 잔광 |

- **9.20V가 네 공간의 조도표에서 「켜진 등 개수」를 전부 삭제했습니다.** 조도는 이제 **색온도 하나로만** 표현합니다. 공간마다 다른 조도표를 만들거나, 등 개수를 판정·표현 조건으로 되살리지 마십시오. 씬의 등이 복도 10·1-1 4·1-3 5·과학실 4·화장실 2로 기획값과 어긋난다는 이전 지적도 **함께 소멸했습니다** — 등을 보충할 필요가 없습니다.
- 조도 톤은 `IlluminanceToneSO`(`Resources/IlluminanceTone.asset`)가 한 곳에서 관리합니다(옛 `BandTableSO`는 2026-10-03 폐기). 공간별로 복사하지 마십시오.
- **C5·S4는 등 개수가 아니라 색온도 구간을 조건으로 씁니다.** C5는 **조도 24 이상**(2026-09-21에 Band2~ → Band1~로 내렸습니다), 즉 **4500K 옅은 노랑 · 3200K 주황 · 2000K 짙은 주황 · 붉은 잔광을 모두 포함**합니다. S4는 **Band3(75–89, 짙은 주황)에서 기존 응시 대상에만** 적용하고 **90~99에서는 배정하지 않습니다**(창형). 새 위치의 등을 추가하거나 전체 천장등으로 응시 판정을 넓히지 마십시오.
- **색 이름은 플레이어 안내 표현입니다.** 구간이나 대상 판정을 화면 색 픽셀로 측정한다는 뜻이 아닙니다. 판정은 축 값과 `Bands.Of()`로만 합니다.

### 2.5 판정 공통 규약 (정본 공통 명세 1절 요약)

1. **카드 상태는 5종**입니다: 대기 / 진행 중 / 준수·위반 / 미판정 / 종료 잠금. 시작하지 않은 카드를 성공으로 계산하지 않습니다.
2. **미판정은 델타 0**입니다. 카드 미배정, 단서 미발생, 대상 참조 누락, 실제 선택 불가능이 모두 미판정입니다. 참조 누락은 크래시가 아니라 미판정이어야 합니다.
2′. **예외는 하나뿐입니다 — 공간 미방문에만 감각축 +9**(`NightRun.UnvisitedPenalty`, 2026-09-21). 그날 배정된 공간에 **한 번도 들어가지 않은** 카드에만 물립니다. 방문했으나 단서가 안 났다 · 문자가 안 왔다 · 참조가 없다 · 선택이 불가능했다는 **전부 0 그대로**입니다 — 그것들은 플레이어의 선택이 아니기 때문입니다.
   **왜 9인가.** 가서 어기면 +12입니다. 미방문이 +9면 **가는 쪽이 항상 3만큼 이깁니다** — 도망은 불이익이되 「가서 어기느니 안 가고 만다」가 되지는 않습니다. 이 한 줄이 없으면 **경비실에 숨어 버티기가 최적해**가 됩니다(축이 한 칸도 안 오르고 완주).
   정산 시점은 **수칙 정산 뒤, 문자 정산 앞**입니다(`NightRun.ApplyUnvisitedPenalty`). 이미 준수·위반으로 정산된 카드는 건너뜁니다. 한 공간에 카드가 여럿이면 **카드마다** 물립니다.
3. **소리가 났다는 사실만으로 카드를 채점하지 않습니다.** 이상현상이 발생했다고 카드가 활성화되는 것은 아닙니다.
4. 위반은 **즉시** 큰 델타 하나를 적용합니다. 준수는 **카드마다 지정한 완료 시점**(`settleAt`: 통행 종료 / 퇴실 / 밤 종료)에만 지급합니다.
5. 준수와 위반은 배타적이며 **같은 카드의 하룻밤 결과는 하나**입니다. 대상 개수나 왕복 횟수로 곱하지 않습니다. **이미 정산한 결과는 되돌리지 않습니다.**
6. **진행 중인 사건은 대상·구간·연출을 고정**합니다. 델타로 구간이 바뀌어도 관련 월드 변화는 사건 종료 뒤 안전한 시점에 적용합니다. **단, 100 도달은 지연하지 않습니다.**
7. **한 공간 방문에서 새로 시작하는 단기 판단 사건은 하나**입니다. 후보가 여럿이면 그날 덱 순서의 첫 유효 카드를 우선합니다. 이미 켜진 장기 의무는 계속 감시합니다.
8. **동시 성립 시 실패가 우선**입니다. 같은 시각 여러 카드가 반응하면 그날 덱 표시 순서로 처리합니다.
9. **밤 종료 순서(2026-09-20 기획서 B절로 변경):** 게임 시계가 **04:00에 닿으면 점검·조우 완료 여부와 무관하게 그 자리에서** 근무가 끝납니다 → 입력 잠금 → 남은 수칙을 덱 순서로 정산 → 미판정 문자 취소 → 근무 일지 → 다음 날. **종료 요청도, 경비실 복귀도, 수락 조건도 없습니다.** 종료 시퀀스(차임·페이드·빨간 줄 0.4초·「DAY n」·태블릿 자동 오픈)의 정본은 기획서 8-2절입니다.
10. 개발 로그와 근무 종료 리뷰에는 **어느 카드·문자의 결과인지 분리해** 저장합니다.
11. 저장 기능이 있다면 진행 중 상태·남은 시간·적용한 델타·문자 노출·조우 예약을 함께 복원하고, 로드 직후 이미 닿아 있는 트리거를 신규 진입으로 세지 않습니다. **저장 기능이 없다면 이 때문에 새로 만들지 않습니다.**

### 2.6 판정 입력의 공통 정의 (정본 공통 명세 2절 요약)

| 입력 | 기준 |
|---|---|
| 문 E | **명령 수락**과 **닫힘 완료**를 별도 신호로 구분합니다. 사거리 밖이라 무시된 입력은 조작이 아닙니다. 조작 출처(플레이어/연출)를 구분합니다. **T3만 닫힘 완료를 요구**하고, 나머지 조작 금지는 수락 시점에 실패합니다. |
| 응시 | 카메라 중앙의 **첫 가시 충돌체**를 대상 ID로 봅니다. 벽·닫힌 문을 관통해 세지 않습니다. 대상이 바뀌거나 가려지면 연속 시간은 0입니다. 0.1초 해상도, 프레임레이트와 무관한 고정 간격 누적. |
| 시각 단서 식별 | 안전 관찰 지점에서 대상을 중앙에 **0.2초**(시험값). 모형 관찰 1초와 별개입니다. H1·T1은 실제 자동 움직임 중에 봐야 합니다. S4는 식별 0.2초를 금지 응시 시간에 포함하지 않습니다. |
| 청각 단서 전달 | 플레이어가 지정 청취 구역 안에 있고 해당 이벤트 ID 음원이 정상 재생되면 전달입니다. 실제로 들었는지, OS 음소거인지는 추측하지 않습니다. |
| 근접 | 플레이어 **발밑 기준점**과 등록한 **바닥 기준점**의 **수평 거리**. 「1.5m 미만」이 금지이며 **정확히 경계는 바깥**입니다(H6만 2m). |
| 공간 경계 | 문 상태가 아니라 발밑 기준점의 진입·이탈로 판정합니다. 화장실 칸 밖도 화장실 실내입니다. 경계 위에서는 직전 공간을 유지합니다. 복도(손전등 Off)와 교실(On) 사이에는 중립 구역을 둡니다. |
| 일반 점검 완료 | 기존 신호가 있으면 연결하고, 없으면 지정 점검 구역 **1초 체류 후 공간 이탈**로 봅니다. 복도 단순 통행은 점검 완료가 아닙니다. |
| 모형 관찰 | **가림 없는 1초 관찰 + 그 방문의 퇴실**을 모두 마쳐야 당일 조우 업무가 끝납니다. |
| Tab(태블릿) | 열려 있는 동안 **이동, 문 조작, 게임 시계, 판정 타이머, 관련 음원·문 동작이 모두 멈춥니다.** 닫으면 이어서 재개합니다. 대기 중 신규 사건은 시작하지 않습니다. Tab이 100 종료나 이미 성립한 실패를 취소하지는 않습니다. **손전등은 예외입니다(2026-09-30 기획 변경)** — 태블릿을 연 채로도 켜고 끌 수 있고, 판정 신호는 태블릿을 닫는 순간 현재 상태로 한 번 나갑니다(`FlashlightRelay`). |

**시간 상수:** 모형 관찰 1초 · H2/C3 금지 응시 3초 · S4 금지 응시 2초 · 식별 0.2초 · 손전등 유예 2초 · T2 물 내림 시퀀스 12초(정확히 12초 퇴실은 실패) · 근접 1.5m(H6만 2m).
수치는 코드에 박지 말고 데이터(SO)에 둡니다.

**조작 표면 (기획서 9절):** 1인칭 마우스 룩 + **WASD 걷기만**입니다. **달리기도 점프도 없습니다**(무력감이 의도입니다). `E`는 **문류 여닫기 전용**이며 그 밖의 오브젝트에는 반응하지 않습니다. 손전등은 전용 토글 키(F 또는 우클릭, 미정), 응시는 별도 키 없이 화면 중앙 N초, `Tab`은 태블릿 열기/닫기입니다.
판정에 쓰는 동작은 **7종**입니다: 문류 여닫기 / 손전등 On·Off / 응시 / 근접 / 체류·이탈 / 시각·진입 / 태블릿 확인.
**근무수칙·이상현상 콘텐츠를 늘릴 때도 이 7종 안에서만 만듭니다.** 새 조작을 요구하는 카드는 구현하지 말고 기획에 되돌리십시오.

> **현행 코드와 어긋남:** `FPController`에 `runSpeed`(LeftShift)와 `jumpForce`(Space)가 **아직 남아 있습니다.** 달리기가 남아 있으면 근접 1.5m·유예 2초 같은 시험값이 전부 무의미해집니다. 제거 대상입니다(§8-8).

### 2.7 인체모형과 문자 — 옛 조우 기준 기록(2026-10-03 폐기)

- **인체모형은 시야 밖에 있을 때만 위치가 바뀌는 유일한 존재**입니다. 이 게임에 추격 AI는 없습니다.
- **S-A만 1일차 과학실 보관 위치에 고정**합니다. **S-A를 실제로 보기 전에는 다른 공간의 모형을 전부 숨깁니다.**
- **2일차부터 하루 한 장면**입니다. 나머지 7개 후보의 순서는 **회차 시작에 한 번 섞어 저장**합니다.
- 선정은 **그날 첫 일반 점검을 마친 뒤**에 합니다. 아직 안 본 후보 중 도달 가능하고 시야 밖에서 준비할 수 있는 장면을 고르되, **오늘 아직 안 간 공간 · 직전 조우와 다른 공간을 우선**합니다. 선정한 장면은 잠그고, 준비 위치를 플레이어가 보고 있으면 대기합니다. **시야 안에서 생성하거나 움직이지 않습니다.**
- 안 본 장면은 같은 자리에 남고, 본 장면만 그 회차에서 소비합니다. 강제 카메라 회전·체류 즉사는 없습니다.
- **관찰 = 가림 없이 화면 중앙에 1초.** **관찰 + 그 방문의 퇴실**을 모두 마쳐야 그날 조우 업무가 끝납니다.
- **발견형(H-B · C-B · T-B)은 두 선택 모두 델타 0**이며 새 판정을 만들지 않습니다.
- **전시형 인체모형은 조우 모형이 아닙니다(9.20V).** 과학실에 놓인 **환경 오브젝트**이며 S1·S-A·S-B의 조우 모델과 **다른 물건**입니다. 전시형을 봤다고 최초 조우가 완료되거나 그날 조우 후보가 소모되지 않습니다. 과학실 **배치축은 이 전시형 모형을 향하는 의자의 방향·위치 변화**로 읽습니다(Band1 의자 하나 → Band2 여러 개 → **Band3 반원 배치가 이 공간의 대표 하이라이트** → Band4 반원을 유지한 채 바깥 의자 하나만 출입구를 향함, 미리 회전시켜 두고 **플레이어를 실시간 추적하지 않습니다**). **의자 변화에 새 응시·근접 판정을 붙이지 마십시오.**
- 기존 선/앉은 포즈만 씁니다. 새 포즈는 만들지 않습니다.
- **모형 효과음은 카드 단서 ID를 보내지 않습니다.** C-A 타격음은 C3를, C-B 칠판음은 C4를 시작하지 않습니다. 분위기 효과음도 마찬가지입니다.
- **문자는 이동만 요구합니다.** 발송 / 전문 노출 / 목적지 도착 / 모형 관찰은 서로 다른 기록입니다. 확인 버튼이나 열람 시간 벌점은 없습니다.
- **G(발견유도) 7개:** 모형을 아직 관찰하지 않은 상태에서 **서로 다른 일반 점검 2곳을 완료하면 1개** 발송합니다. 모형을 자연 발견하면 취소합니다. **목적지 진입만 판정**하며 무시해도 델타가 없습니다.
  **G의 주인은 카드가 아니라 조우 장면입니다** — H-A→G-H1 · H-B→G-H2 · C-A→G-C1 · C-B→G-C2 · S-B→G-S1 · T-A→G-T1 · T-B→G-T2. **S-A에만 유도 문자가 없습니다**(1일차 고정). 그래서 G는 `EncounterDirector` 없이는 구현할 수 없습니다(§12.1-7).
- **N1(재방문) 1개:** 회차 최대 1회. **역설 문자를 보낸 날에는 발송하지 않습니다.** 새 이상현상 없이 목적지 도착만 요구합니다.
- **P(역설) 23개:** 도착 보상 0, **미도달·거절 벌점 없음**(기획서 D절이 전 유형 「별도 벌점 없음」으로 확정). 하루 상한은 **근무 시작 시 신뢰 구간별 0~4쌍**(§2.9). 전문 노출 전에 짝 카드가 정산되면 P형은 미판정으로 취소합니다. **수칙 결과를 먼저 처리하고**, 게임이 아직 진행 중일 때만 문자 결과를 적용합니다.
  **회차 한도와 P ID 1회 제한은 기획서에 없습니다.** 동시 후보 우선순위도 기획서에 없고 현행 코드는 **덱 순서**입니다. 둘 다 기획 확인 대기입니다(Q14, §12.2).
- 세부 조건(발송 자격·목적지·발송 시점·충돌표)은 **기획서 10-3·10-4·10-5·12절**이 정본입니다. **역설 23개의 문구 자체는 `지침록 9.20V`가 정본**이고(카드 에셋의 `paradoxText`는 9.20V 판으로 교체 완료), 9/12 판 3·4절은 그 다음입니다.

### 2.8 설계 금기 — 선의로 어기기 쉽습니다

- **플레이 중 축·델타·위반 팝업이나 게이지를 표시하지 마십시오.** 플레이어는 자기가 어겼다는 사실을 모른 채 환경이 변하는 것만 봐야 합니다.
- **공지 카드나 별도 인수 목록 UI를 추가하지 마십시오.**
- **태블릿에는 수칙 본문만 노출합니다.** 「개발 판정 상세」와 공통 명세는 제작자용입니다.
- **수칙 본문이 요구한 행동만 채점합니다.** 괴담의 이유나 기관 설명은 판정 조건이 아닙니다. 「목록에 없던 것」은 목록 UI를, 「이름」은 개인정보·음성 인식을, 「손이 네 개」는 추가 모델을 요구하지 않습니다.
- 의도적으로 모형을 보지 않는 플레이에 **강제 시선이나 체류 즉사를 추가하지 마십시오.**
- **달리기·점프를 되살리지 마십시오.** 걷기만이 기획 의도입니다(§2.6). **판정 동작 7종 밖의 조작을 추가하지 마십시오.**
- **화장실에 거울·플레이어 반사를 쓰지 마십시오(9.20V).** 청각은 물방울 개수가 아니라 **물 내림 · 호칭 · 칸 안쪽 소리**로 구성합니다.
- **수칙을 성립시키려고 연출을 강제하지 마십시오.** 배치 72 이상(Band3)에서 칸이 전부 열려 있으면 T5의 「닫힌 칸 아래 빛」과 청각 90 이상의 「닫힌 칸에서 들숨」이 **동시에 성립하지 않을 수 있습니다.** 9.20V는 이때 들숨을 생략하기로 했고, **문을 강제로 닫는 추가 연출은 넣지 않습니다.** T5도 닫힌 칸·빛 단서가 실제로 제시되지 않았으면 **판정을 시작하지 않아야 합니다**(미판정, 델타 0).
- 새 공지·카드·모형·문자를 추가하지 마십시오. **24수칙 · 8장면 · 31문자 구성은 기획서에서 이미 고정**됐습니다. 문구·목적지·짝 카드를 Claude가 지어내거나 바꾸지 마십시오.
- 태블릿 폰트·디자인을 신뢰에 따라 왜곡하지 마십시오(폐기된 안). **신뢰 100이 사망이 아니라는 것은 2026-09-22 시나리오 기획서 v6 §5-1로 확정**됐습니다(Q12 종결). 중간기획서 9-1절의 「신뢰축이 오르면 이 화면에서 글자가 흔들림」 한 문장만 남아 있고, 이 금지가 그대로 우선합니다.
- 정본은 **새 범용 규칙 엔진이나 대규모 구조 개편을 요구하지 않습니다.** 팀의 현재 데이터 구조에 연결하십시오.

### 2.9 신뢰와 역설 (2026-09-20 기획서로 확정) — 옛 역설 연출기 기준 기록, 2026-10-03 폐기(새 역설은 10단계)

- **신뢰의 유일한 용도는 태블릿 문자와 근무수칙 사이의 충돌(역설)을 늘리는 것**입니다. 게임오버·월드 연출·태블릿 폰트/디자인 왜곡에는 쓰지 않습니다.
- **하루 상한은 근무 시작 시점의 신뢰 구간으로 정해집니다**(기획서 C절, 10-5절). **5단계에서 새 기획서의 일차별 0/1/1/2/2와 변조(신뢰 Band3부터)로 바뀝니다** — 아래 표는 현재 코드입니다.

| 근무 시작 시 신뢰 | 그날 태블릿에 실리는 역설 |
|---|---|
| Band0 **0–14** | **없음** |
| Band1 **15–29** | 최대 **1쌍** |
| Band2 **30–44** | 최대 **2쌍** |
| Band3 **45–64** | 최대 **3쌍** |
| Band4 65~ | 최대 **4쌍** |

- 대상은 항상 **「그날 배정된 수칙 중에서 자격을 충족한 것」**입니다.
- **구간은 밤 시작에 한 번 읽고 그날은 고정**합니다(`ParadoxDirector.BeginNight`이 `_quotaToday`에 스냅숏, 2026-09-20 구현 완료). 발송할 때마다 현재 신뢰를 다시 조회하면 안 됩니다 — 밤 도중 준수 정산으로 신뢰가 오를 때 그날 상한이 함께 늘어납니다.
- **정산은 3분기입니다**(기획서 D절).

| 플레이어 선택 | 짝 수칙 | 역설 문자 | 축 결과 | 결산 표시 |
|---|---|---|---|---|
| 역설을 **따름** | **위반** → 짝 수칙의 감각축 델타 | 완료(보상 없음) | 감각축만 오름. **신뢰는 오르지 않음** | 짝 수칙에 **빨간 줄** |
| 역설을 **거절** | **준수** → 신뢰 **+4/+5/+8** | 미이행 → **벌점 없음** | 짝 수칙의 신뢰만 오름 | **무표시** |
| 짝 수칙 **미판정** | 미판정 | **문자도 취소** | **델타 0** | **무표시** |

- **역설에 독립적인 미도달·거절 벌점은 없습니다.** 옛 「P형 유효 미도달 배치 +6」은 폐기됐습니다. G·N1도 벌점이 없습니다.
- 읽지 않았다는 이유만으로 준수를 확정하지 않습니다. 현상이나 선택 기회가 없으면 판정하지 않습니다.
- 벌점이 사라지면서 **「거절 → 배치 압박」 순환은 소멸**했습니다. 남는 순환은 준수 → 신뢰 ↑ → **그날 실리는 역설 개수 ↑** 하나뿐이며, 역설의 기계적 기여는 짝 수칙이 이미 하던 일(위반/준수)과 같습니다. 역설의 값어치는 **수치가 아니라 연출과 선택의 무게**에 있습니다.
- 구현 자리: **`ParadoxDirector`가 이미 있습니다**(`Assets/_Game/Scripts/Direction/`). 문구 생성은 `Scripts/Editor/ParadoxTextBuilder.cs`. **`MessageDirector`나 `ParadoxResolver`를 새로 만들지 마십시오** — 위 3분기는 전부 짝 카드의 `RuleWatcher` 결과로 자연히 해결됩니다. 별도 해결기는 같은 판정을 두 곳에서 하게 만듭니다.
- 신뢰는 `IFearAxisReader.GetBand(FearAxis.Trust)`로 읽고, `BandResolver`의 월드 방송 대상에는 넣지 않습니다.
- **`EventBus.MessageSent` 구독자가 프로젝트 전체에 0개**입니다. 태블릿 UI가 생기기 전에는 문자를 보내도 화면에 뜨지 않습니다. 역설 작업을 태블릿 UI보다 앞세우지 마십시오.
- **23쌍 총괄표(짝 수칙 · 따름/거절 델타 · 발송 시점)의 정본은 기획서 12절입니다.** 이 파일에 베끼지 마십시오.
- 역설 문자의 공통 규칙(전문 노출 전 짝 카드 정산 시 취소, 수칙 결과 먼저 처리, 도착 보상 0, P형 방문에는 짝 카드 외 신규 사건 없음)은 23쌍 전부에 그대로 적용합니다.
- 산술 참고(**2026-09-21 재설계 뒤 실측**): 준수 +4/+5/+8, 하루 6장 × 5일을 전부 준수하면 신뢰가 **100 상한에 닿습니다**. 일차별 역설 상한은 **0 → 1 → 2 → 3 → 4쌍, 누적 10쌍**입니다. **Band3·Band4는 이제 실재하는 구간입니다** — 이전 값(+2~+4)에서는 5일 전부 준수해도 60~90에 머물러 4쌍밖에 못 봤고, 3쌍·4쌍 구간은 존재하지 않는 구간에 가까웠습니다.
- 신뢰 100은 **게임오버가 아니라 상한**입니다. 완벽한 플레이가 상한에 붙는 것은 설계 의도이며, 기획(민)이 2026-09-21에 그대로 두기로 결정했습니다. 준수 델타를 낮춰도 하루 6장 × 5일이면 100에 닿으므로, 낮추면 역설 쌍수만 줄어듭니다.
- **결산 표기는 3종입니다**(`DutyMark`, 2026-09-21 신설).

  | 표기 | 언제 |
  |---|---|
  | `None` | 지켰거나 판정이 시작되지 않음 |
  | `Struck` 「어김」 | 그냥 어겼거나, 그 공간에 가지 않음 |
  | `Instructed` 「지시를 따름」 | **그날 짝 역설 문자를 받은 상태에서** 어김 |

  **수치 손해는 `Struck`과 똑같습니다 — 다른 것은 종이가 부르는 이름뿐입니다.** 둘을 같은 빨간 줄로 그리면 플레이어는 이틀이면 「문자는 함정, 무시가 정답」을 배우고 그 뒤로 역설을 고민하지 않습니다. 23쌍을 준비해 놓고 2일차부터 전부 버리는 셈입니다.
  판정 근거는 `ParadoxDirector.WasSentToday(cardId)`이며 `NightRun.BuildDutyLog`가 읽습니다. **결산 UI는 아직 없습니다** — 데이터 계층까지만 구현했습니다.
  **수치나 델타는 여전히 표시하지 않습니다.** 드러나는 것은 「어겼다 / 지시를 따랐다」는 **말**이지 축 값이 아닙니다(§2.8).

---

## 3. Unity 에디터와 작업하기

### 3.1 연결

이 프로젝트에는 `com.unity.pipeline`(0.7.0-exp.1)이 설치되어 있어, 실행 중인 에디터를 **Unity CLI** 또는 **Unity CLI의 MCP 서버**(`unity mcp`)로 직접 조작할 수 있습니다.

```powershell
unity status        # 에디터 인스턴스가 "ready"인지 확인
unity command       # 에디터가 노출하는 명령 목록
unity command console_status   # 컴파일 실패 여부와 콘솔 카운트
```

- MCP로 연결된 세션에서는 같은 명령이 `unity` MCP 도구(`editor_status`, `console_status`, `recompile`, `run_tests` 등)로 보입니다.
- **작업 전에 에디터가 `ready`인지 먼저 확인하십시오.** 에디터가 닫혀 있으면 모든 명령이 「Pipeline instance를 찾을 수 없음」으로 실패합니다. MCP 도구에는 프로젝트를 여는 명령이 없으므로, 사용자에게 `unity open "<horror house 경로>"` 또는 Unity Hub로 열어 달라고 요청하십시오.
- **`com.unity.pipeline`은 `Packages/manifest.json`에 있어야 합니다.** 병합 때 빠질 수 있습니다(현재 stash 복구본, 미커밋 — 커밋 여부는 팀 결정 대기, §12.2). MCP 서버가 에디터보다 먼저 켜지면 도구가 0개로 보입니다 → 에디터를 띄운 뒤 앱을 재시작합니다.
- 에디터 창이 뒤에 있으면 패키지 해석·플레이 틱이 멈춥니다 → `editor_focus`.
- 에디터가 자동화 모드로 열리지 않았기 때문에 **모달 대화상자가 뜨면 명령이 멈춥니다.** 확인 대화상자를 띄우는 메뉴를 MCP로 실행하지 마십시오. 응답이 없으면 에디터 화면을 확인해 달라고 요청하십시오.
- **연결이 안 되는데 에디터는 켜져 있다면 Safe Mode(컴파일 에러)를 먼저 의심하십시오.** `unity pipeline list`로 확인한 뒤 C# 컴파일 에러를 고치고 Unity를 재시작합니다.
- `Library/Pipeline/.unity-pipeline-port`에는 에디터에서 C#을 실행할 수 있는 인증 토큰이 들어 있습니다. **내용을 출력하거나 공유하지 마십시오.**
- `eval`, `run_script`는 임의 코드 실행입니다. 읽기 목적 외에는 사용자 확인 후 쓰십시오.

### 3.2 씬·에셋 수정 규칙

- **에디터가 연결되어 있으면 `.unity` / `.prefab` / `.asset` YAML을 직접 편집하지 마십시오.** fileID·GUID를 틀리기 쉽고, 에디터가 재임포트 전까지 변경을 모르며, 활성 씬이 아닌 파일을 고치는 실수가 생깁니다. 에디터 명령(`create_gameobject`, `set_component_properties`, `save_scene` 등)을 쓰십시오.
- 에디터가 없을 때만 파일을 직접 고치고, 그 사실을 명시하십시오.
- 씬·프리팹 수정 전에는 **LFS 잠금** 확인을 사용자에게 요청합니다(§9).
- `switch_build_target`, `clear_baked_lighting`, `delete_asset`, `package_add/remove`, 설정 변경(`set_*_settings`)처럼 되돌리기 어려운 명령은 **사용자 확인 후** 실행합니다.

### 3.3 검증

1. 코드 수정 후 `Assets/Refresh` → 25~30초 뒤 `recompile_status` → `console_status`로 **컴파일 에러 0**을 확인합니다.
2. `run_tests`로 EditMode 테스트를 돌립니다. **기준: 264/264 통과**(2026-10-03 27차 실측). 전체 실행은 `async_tests: true`로 — 동기 실행이 에디터를 멈춘 적이 있습니다.
   - **`DesignDriftTests`가 드리프트 감시입니다.** 일차 하한이 줄지 않고 Band4에 닿지 않는지, 그리고 **이 파일 본문에 옛 구간 경계가 남아 있는지**를 검사합니다. 여기가 깨지면 값이 아니라 **두 곳이 서로 다른 말을 하고 있다**는 뜻입니다. 결과가 크면 파일로 저장되므로 요약만 grep합니다. (옛 24장 카드의 에셋↔빌더·축 쿼터·죽은 카드 검사와 `CardScenarioTests`는 2026-10-03에 카드와 함께 지웠습니다.)
3. 플레이 모드 확인은 근무 씬 `Assets/0. Main/01 Scene/PlayScene.unity`에서 합니다. **F3 디버그 콘솔**(`NightDutyDebugConsole`)로 축·시계·조우·수칙 단서를 조작합니다. (옛 `_Test_AxisRig` 씬·판정 디버그 패널·`DebugAxisDriver`는 2026-10-03 폐기.)
4. 플레이 중에는 `set_component_properties`가 안 됩니다 → `eval`. `FindAnyObjectByType`는 DontSave 오브젝트를 찾지 못합니다.
5. 병합 후 빌드 씬 목록이 옛것이면 Unity를 재시작합니다(**재시작 전에 저장하지 마십시오**). 현재 빌드 씬은 9개입니다(0번 `0. Main/01 Scene/MainScene`).
6. 에디터를 쓸 수 없을 때의 대안: `mono-mcs`와 UnityEngine 최소 스텁으로 `-langversion:7.2` 컴파일(`UNITY_EDITOR` / 심볼 없음 / `NIGHTDUTY_DEBUG` 세 구성). 스텁 누락 에러는 코드 문제가 아닙니다. 지금은 Unity MCP로 직접 컴파일·테스트하는 것이 기본입니다.
7. 조도 축 연출은 에디트 모드에서도 **Game 뷰**로 확인합니다(§8-6).

### 3.4 런타임 테스트 하네스 — 삭제했습니다 (2026-10-01, 20차)

`NightDutyTestHarness`·`HarnessStandIns`·`DutyTabletPanel`(옛 §3.6 임시 태블릿·4축 막대)은 **파일째 지웠습니다**(민 지시). 옛 24장·옛 조우 8장면을 위한 자리표시를 채우던 코드라 새 편성(6단계~)에서는 할 일이 없었고, F3 키를 새 콘솔과 다퉜습니다.

아직 필요하던 일은 이렇게 옮겼습니다.

| 옛 하네스가 하던 일 | 지금 |
|---|---|
| 센서 부착(`PlayerSensors`·`FlashlightRelay`) | 각자 자동 설치(`FlashlightRelay.EnsureFor`) |
| 켜진 채 저장된 손전등 끄기(§5.5-22) | `FlashlightRelay.EnsureFor`가 플레이어 밖의 `Flashlight_ON`을 끈다 |
| 문 발신기 `DoorRelay` | `DoorRelay` 자동 설치 |
| 에디터가 초점을 잃으면 멈춤(`Run In Background`) | 디버그 콘솔 `Awake`가 `Application.runInBackground = true` |
| 잠긴 문 무시(F4)·문 정책 무시(F5) | 디버그 콘솔 `Awake`가 `PlayerInteractor.IgnoreLocks = true`(에디터·개발 빌드만) · 개요 탭 [잠긴 문 무시]·[모든 문 열기] 버튼 |
| 상태판(F9)·표식 보이기(F6)·구역 상자(F7) | 디버그 콘솔 F3(§19차 ⓖ). 표식·구역 시각화는 아직 콘솔에 옮기지 않았습니다 |
| 1초 응시로 `DoorAutoOpenObserved` 흉내 | 진짜 연출로 대체 — H2 단서가 문을 연다(20차 ⓓ) |
| 옛 조우 모형·`ModelObserved` 흉내·게임 시계 덮어쓰기 | 폐기(옛 체계 전용) |

**확인할 때는 F3 디버그 콘솔을 쓰십시오.** 수칙 탭의 [이동]·[단서], 조우 탭의 [이동+실행]으로 실제로 걷지 않고 수칙·연출을 볼 수 있습니다.

---

### 3.5 문이 안 열리던 이유 (2026-09-22 실측)

**증상:** 걸어다녀도 문이 하나도 안 열립니다. 밖으로 나갈 수 없어 퇴실 시험이 문 앞에서 멈춥니다.

**원인은 셋이고 전부 다릅니다.**

1. **에디터가 초점을 잃으면 플레이가 통째로 멈춥니다.** `Run In Background`가 꺼져 있어, 알트탭 한 번이나 원격 조작 중에는 `Time.frameCount`가 그대로 섭니다(실측: 3분 동안 frame=2). 게임이 얼어 있으니 아무 키도 듣지 않고 트리거도 안 걸립니다. → **디버그 콘솔(F3) `Awake`가 `Application.runInBackground = true`를 런타임에만 덮습니다**(20차, 옛 하네스에서 옮김). 프로젝트 설정은 그대로입니다.
2. **벤더 `DoorScript`의 조건이 셋인데 안내가 하나도 없었습니다.** ⑴ **카메라**가 문 앞 트리거 상자 안(`inZone`), ⑵ **문짝이 아니라 손잡이**를 약 25° 안쪽으로 조준(`dotProd < -0.9f`), ⑶ `E`. 그런데 문 56개 전부 `doorTexts.enabled = false`라 「Press [E] to open」이 안 뜨고, `doorSounds` 클립도 전부 비어 있고, 조준선도 안 보였습니다(알파 0.25). **조건은 멀쩡한데 보이지 않는 조작**이라 사람이 문 앞에서 막혔습니다. → **§4.6의 상호작용이 이걸 대체합니다.**
3. **기획에 없는 문이 잔뜩 열려 있었습니다.** 지금은 `Resources/DoorPolicy.asset`이 가립니다(§4.6) — 실측 수납가구 30 · 열리는 문 10 · 잠긴 문 16. 밖으로 나가는 길은 **정문 `Exterior/Doors/DoorMain` (27.6, 1.5, 49.0)**입니다. 시험 동안 잠금은 디버그 콘솔이 기본으로 건너뛰고(개요 탭 [잠긴 문 무시]), 정책까지 무시하려면 개요 탭 [모든 문 열기]입니다(옛 F4·F5는 하네스와 함께 삭제).

---

### 3.7 경비실 CCTV (2026-09-30)

씬을 고치지 않는 런타임 설치입니다. 파일은 `Assets/_Game/Flow/Cctv/`(`CctvSystem`·`CctvConfigSO`·`CctvOnlyVisible`·`Editor/CctvConfigSOEditor`)와 `Assets/_Game/Resources/Cctv/CctvScreen.shader`, `Assets/_Game/Resources/CctvConfig.asset`입니다.

- **설치.** `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` + `SceneManager.sceneLoaded`. 흐름이 Main→Loading→PlayScene이라 첫 씬만 보면 놓칩니다. 모니터(`CctvConfigSO.MonitorPath`, 못 찾으면 이름으로)가 **있는 씬에서만** 루트 `CCTV (auto)`를 만듭니다. 런타임 오브젝트에 `DontSave`를 붙이지 않습니다(§3.4와 같은 이유).
- **렌더.** 320×240 RT 하나. 카메라 5대는 전부 꺼 두고, 고른 채널 하나만 **한 프레임 켜서** 그립니다 — 들여다보는 중 12fps, 곁눈 6fps. 플레이어가 모니터 5m 안이고 화면이 시야 절두체 안일 때만 그립니다. 카메라는 후처리·그림자·AA·HDR·MSAA·오클루전 컬링 모두 끔.
- **밤이라 새까만 문제.** 카메라마다 자식 스폿 조명 `IR`을 두고, **그 카메라가 그리는 동안만** 켭니다. 플레이어 시야에는 이 빛이 절대 안 보입니다.
- **화면.** `NightDuty/CctvScreen`(URP 언릿 HLSL): 흑백 → 증폭(`_Gain`) → 감마 → 노이즈 → 굴러가는 띠 → 주사선 → 채널 전환 지직거림(`_Static`)·신호(`_Signal`) → 비네트. **노이즈는 작게 두십시오** — 화면이 플레이어 카메라의 후처리를 한 번 더 거쳐서, 0.1만 돼도 어두운 채널(도서관)이 알갱이뿐입니다.
- **모니터 메시.** `Old CRT Monitor`는 **로컬 Z가 위, −Y가 앞**입니다. 화면 중심 로컬 (0, −0.472, 0.075), 크기 0.30×0.225m(메시 실측). 쿼드는 `MeshCollider` 대신 얇은 `BoxCollider`.
- **조작.** 화면을 조준하면 「[E] CCTV 보기」. 들어가면 `FPController` 끔 · `PlayerTablet.readInput=false` · 뷰모델 렌더러 숨김, 카메라가 화면 앞 0.32m로 0.35초 보간. **A/D·←/→·휠·숫자 1~5**로 채널, **E/Esc/S**로 나옵니다. `NightRun.IsCaptured`이거나 `timeScale <= 0`이면 강제로 나옵니다.
- **글자.** 화면 위 TextMeshPro 「CAM01  복도 / REC  hh:mm」(시계는 `GameTime.CurrentTimeText`). 한글 글리프가 있는 TMP 폰트를 찾아 쓰고, 없으면 「CAM01」만 씁니다.
- **CCTV에만 보이는 물체.** 아무 오브젝트에 `CctvOnlyVisible`을 붙이면 평소엔 렌더러가 꺼지고 CCTV 카메라가 그릴 때만 보입니다. `channel = -1`이면 전 채널, 0~4면 그 채널에서만. 그림자는 끕니다(그림자로 들킵니다).
- **판정용 이벤트.** `CctvSystem.ChannelChanged(int)` · `ViewEntered` · `ViewExited`, 조회용 `Active`·`IsViewing`·`CurrentChannel`·`ChannelLabel(i)`·`ChannelCamera(i)`·`SetChannel(i)`. **K1·K2 판정은 아직 이 이벤트에 붙어 있지 않습니다**(§12.1).
- **실측(2026-09-30 플레이).** 5채널 모두 화면·글자 정상, 좌우 반전 없음(비상구 표지 방향을 카메라 직접 렌더와 대조). 04:00 자동 종료 → ResultScene 전환도 막지 않았습니다. 성능 계측은 아직 안 했습니다.

### 3.8 엠비언트 (2026-09-30)

파일: `Assets/_Game/Flow/Ambience/`(`AmbiencePlayer`·`AmbienceConfigSO`·`Editor/AmbienceEditorTools`), `Assets/_Game/Resources/AmbienceConfig.asset`. 설치 방식은 §3.7 CCTV와 같습니다.

- **세 겹.** ① 룸톤 — 카메라(귀) 위치가 든 상자의 루프, 2.5초 크로스페이드. 상자 목록 순서가 우선순위이고, 어디에도 없고 건물 안이면 복도. ② 불안 레이어 — 청각 구간 n이면 `dread_b1..bn`이 6초에 걸쳐 차오름(등전력). ③ 원샷 — 공간별 목록에서 무작위, 간격은 구간별(B0 45~90초 → B4 7~18초), 같은 파일 연속 금지, 피치 ±6%.
- **구간은 `EventBus.BandChanged`(청각)로만** 받습니다. 도서관은 판정 공간이 없어 **복도 구간**을, 경비실은 **전 공간 최고 구간 − 1**을 따릅니다(`BandSource`). 밤이 아니면(`!NightRun.IsNightActive`) 구간 0, 붙잡히면 1초에 걸쳐 전부 끔.
- **공개 API**: `AmbiencePlayer.Active`, `PlayAt(경로|파일명, 위치, 볼륨)`, `PlayStinger("stinger_hit"|"stinger_riser"|"stinger_breath"|"stinger_whisper")`, `Duck(레벨, 초)`, `CurrentZone`, `CurrentBand`, 이벤트 `OneShotPlayed`.
- **소리 파일은 코드 합성**(numpy, 48kHz, OGG Vorbis). 루프는 전부 원형 합성이라 이음새가 없고, 크기는 A가중으로 맞췄습니다(룸톤 −38, 도서관만 −44 dBFS). 합성 스크립트는 세션 스크래치에만 있고 저장소에는 없습니다.
- **단서와 겹치는 소리를 만들지 않았습니다**: 3번 노크(C1)·분필·교탁 긁힘(C4)·유리(S3)·물 내림(T2)·세면대(T4).
- 메뉴: `NightDuty/엠비언트 설정 에셋 만들기`, `NightDuty/엠비언트 소리 파일 검사`(없는 파일 목록).
- 실측(2026-09-30 플레이, 가짜 클립 주입): 경비실→복도 이동 시 룸톤 전환, 청각 Band3 주입 시 레이어 1~3 켜짐·4 꺼짐, `PlayAt` 재생, 채널 전환음 재생. 콘솔 오류 0.

## 4. 코드 구조

### 4.1 어셈블리 — 의존은 단방향

```
Assets/_Game/
├── Scripts/NightDuty.Core.asmdef     references: []   판정 · 수치 · 편성 (Tests·Editor에 InternalsVisibleTo)
│   ├── Core/          AssemblyInfo · Vocabulary · Bands · EventBus · IFearAxisReader · Deltas · SensingRules
│   ├── Data/          RuleSO(태블릿 표시 전용) · NightDeckTableSO(비어 있음, 김진선님 폴백)
│   ├── Rules/         JudgeSignal (신호 어휘만)
│   ├── FinalRules/    FinalRuleBook · FinalJudges (새 수칙 판정)
│   ├── Program/       NightProgram · ProgramCatalog (밤 편성 · 수칙/조우 카탈로그)
│   ├── Inspection/    InspectionBoard · InspectionCatalog · InspectionPlan
│   ├── Direction/     NightRun(.Final/.Program/.Direction/.Tablet) · JudgeTarget · JudgeTargetRegistry · NightClock · ParadoxMessage
│   │   └── Tension/   TensionDirector · EncounterScripts · SurpriseBudget · StagePoints · DirectionEvent
│   ├── Stats/         FearAxisSystem · BandResolver · DayFloor · DaySummary · DutyLogEntry · NightSnapshot · RestartPolicy · WarningLedger
│   └── Editor/        NightDuty.Editor.asmdef — InspectionTargetPlacer · RuleAnchorPlacer
├── Tests/EditMode/NightDuty.Tests.EditMode.asmdef   EditMode 테스트 264개(2026-10-03 27차)
├── Resources/         NightDeckTable(빈 폴백) · CaptureCardLook(재시작 카드의 YOU DIED) · CaptureCast(붙잡힘 연출표) · FinaleCast(피날레 배역표) · DoorPolicy · CctvConfig · AmbienceConfig · DirectionSounds · DirectionScreenFx · IlluminanceTone · StandIns/
└── Flow/              asmdef 없음 → Assembly-CSharp. 씬과 코어를 잇는 구동기 (§4.4)
    │                  NightRunDriver · NightDutyResultMapper · SpaceZones(공간·구역 신호) · TabletBridge · FlowAutoInstall · SpaceLights(빈 껍데기)
    ├── Sensors/       PlayerSensors · GazeProbe · FlashlightRelay · DoorRelay · FinalRuleRelay · InspectionSensor (근무 씬에 자동 설치)
    ├── Finale/        FinaleCastSO(배역표 — 팀원 몹 프리팹 칸) · FinaleMob(비트 재생) · FinaleAnimEvents (11단계 피날레 몹)
    ├── Capture/       CaptureDirector(+.Scene) (붙잡힘 틀·재시작 카드, 근무 씬에 자동 설치) · CaptureCastSO(연출표 — 축별 장면 프리팹 칸) · CaptureAnimEvents · CaptureCardLook (카드의 YOU DIED 모습)
    ├── Direction/ · Presentation/ · Interaction/ · Cctv/ · Ambience/ · Debug/(F3 콘솔)
    └── Editor/        JudgeGizmos · JudgeSceneReport · SpaceZonesEditor · StandInPrefabBuilder · DoorPolicyBuilder · GuardRoomPhoneBuilder · …
```

- **`NightDuty.Client` 어셈블리는 아직 없습니다.** 진선님 코드(GameFlow·Result·HUD)와 Lee의 시험 리그는 개인 폴더의 `Assembly-CSharp`에 있습니다. `_Game`으로 옮길지는 결정 대기(Q8)입니다.
- **`NightDuty.Core`는 연출을 참조하지 않습니다.** 축이 바뀌면 Core는 이벤트만 올리고, 무엇을 그릴지는 클라이언트가 정합니다. 「축이 올랐으니 여기서 바로 불을 끄면 되겠다」는 유혹이 반드시 옵니다. **asmdef에 참조를 추가해 우회하지 마십시오.**
- 개인 폴더 `Assets/3.1. Programmer_lee/`의 옛 시험 리그(`AxisTestLightRig`·`AxisTestAnomalyRig`·`NightRunDebugPanel`·`_Test_AxisRig` 씬)는 2026-10-03에 지웠습니다. 걸어서 시험할 때는 근무 씬의 F3 콘솔을 씁니다.
- 옛 에디터 메뉴(`NightDuty ▸` 카드 에셋 생성·이상현상 표·씬 대상 검사·조도 표·테스트 씬)는 2026-10-03에 지웠습니다. 지금 메뉴는 `야간근무 ▸`(연출·경비실·문 정책 등)입니다.

### 4.2 판정 흐름

```
센서·문·연출 ──JudgeSignal──▶ NightRun.Send / Tick (현재 공간·태블릿 상태를 직접 든다)
                                   ├▶ FinalRuleBook (그날 편성 새 수칙 판정, FinalWorld) ── 위반/위협/준수 ─▶ FearAxisSystem.Apply
                                   ├▶ TensionDirector (조우·수칙 단서·가짜 놀람 시점) ─▶ EventBus.DirectionEmitted ─▶ DirectionStage
                                   └▶ InspectionBoard (점검표 보고·정산)
FearAxisSystem ─▶ BandResolver (래칫 · 일차 하한) ─▶ EventBus.BandChanged / BandProgress (연출·김진선님 HorrorAxisLink)
NightRun.RequestEndNight ─▶ 새 수칙 밤 종료 정산 · 점검 정산 ─▶ DaySummary(근무일지 = 편성 수칙 줄) ─▶ EventBus.DayEnded
포획 시 EventBus.Captured(axis) 1회 (구독자 없으면 옛 AxisCritical, 원인은 NightRun.Cause) ─▶ CaptureDirector ─▶ RestartAfterCapture
```

- 새 파일의 자리는 「고르는 것인가(Direction·Program) / 판단하는 것인가(FinalRules·Inspection) / 숫자를 올리는 것인가(Stats) / 그리는 것인가(Flow)」로 정합니다.
- 2026-10-03(26차): 옛 판정 책(RuleBook·RuleWatcher·조건 9종·JudgeWorld)·옛 조우/역설 연출기·옛 단서 큐(AnomalyCueDirector·표 2개)·근접 발신기(ProximityProbe)·옛 점검 상자/통행 구역 신호를 지웠습니다. 되살리지 마십시오.
- 역설 문자 발송기는 아직 없습니다(10단계). 문자 형식은 `ParadoxMessage`, 통로는 `EventBus.MessageSent` → `TabletBridge`.
- **`RequestEndNight` 수락 조건은 만들지 않습니다.** 04:00 무조건 종료가 확정됐습니다(§2.5-9).

### 4.3 핵심 API

```csharp
namespace NightDuty {
    enum SpaceId  { None=0, Corridor=1, Toilet=2, Classroom_1_1=3, Classroom_1_3=4, ScienceRoom=5, Library=6, SecurityRoom=7, Classroom=8 }   // 교실 두 값은 SpaceIds.Canonical로 Classroom
    enum FearAxis { Auditory=0, Illuminance=1, Layout=2, Trust=3 }
    enum Band     { Band0=0, Band1=1, Band2=2, Band3=3, Band4=4 }

    static class Bands { static Band Of(int value); static int LowerBound(Band); static int UpperBound(Band);
                         static float Progress(int value, Band band); static Band Higher(Band, Band); }   // 원시 표. 연출 구간은 BandResolver
    interface IFearAxisReader { int GetValue(FearAxis); Band GetBand(FearAxis); }

    static class NightRun {   // 회차 창구
        StartNewRun(); BeginNight(int day, Func<int> clockMinutes /* 근무 시작부터의 분 0~240 */); Tick(float sec);
        JudgingWindowEnabled; IsJudgingNow; AddWarning(int, string); Warnings; HighestSensory();
        SignCheckpoint(); RestartAfterCapture(); LeaveVoluntarily(); RestartsTonight; NightStartSnapshot; Checkpoint;
        Send(in JudgeSignal); RequestEndNight();   // 04:00 ShiftEnded · 경비실 전화(CanEndShiftEarly)
        BuildSummary(); Day; Axes; IsCaptured; Cause;
        // 상태: CurrentSpace · TabletOpen · TodayDeck(태블릿 표시) · FinalRules · Program · Inspections
        // 디버그: DebugForceCapture · DebugAddAxis · DebugRebroadcast (밤 시계 이동은 NightRunDriver.DebugJumpToNightMinute)
    }

    static class EventBus {
        event Action<SpaceId, FearAxis, Band, Band> BandChanged;   // space, axis, from, to
        event Action<SpaceId, FearAxis, float>      BandProgress;
        event Action<DaySummary>                    DayEnded;
        event Action<FearAxis>                      Captured;      // 100 도달, 1회 (CaptureDirector가 구독)
        event Action<FearAxis>                      AxisCritical;  // 옛 사망 통로 — Captured 구독자가 없을 때만
        static void ClearAll();
    }
}
```

- **`JudgeTarget`:** MonoBehaviour, `_ids[]`, `PrimaryId`, `IdOf(Component)`, `SetIds`, OnEnable 등록. Registry는 ID별 개수·소유자 목록을 가지며 SubsystemRegistration에서 초기화됩니다.
- **`DaySummary`:** 기존 11인자 생성자 유지 + `Outcome`, `Cause`, `ViolationMinutes`, `Results`, `FormatMinutes`.
- **`DayFloor`**(정적, `Scripts/Stats/`): `Band Of(int day)` · `LastDay`. 곡선은 `Floors` 배열 한 곳(§2.3″).
- **`BandResolver`**: `SetDayFloor(Band)` · `Target(axis)`(연출 구간) · `Peak(axis)` · `Shown`(IFearAxisReader: 값=생존 수치, 구간=연출 구간) · `GetShown(space, axis)` · `SetHold` · `BroadcastAll`.
- **`FearAxisSystem.Lower(axis, amount, sourceId)`**: 생존 수치 빼기. 신뢰·잠금 중·0 이하 무시.
- **`Deltas`**(정적, `Scripts/Core/`): 새 기획서 수치 표(§2.2). `SoftCap` 95.
- **`NightClock`**(정적)·**`NightClockTracker`**(`Scripts/Direction/NightClock.cs`): `PhaseAt` · `IsJudging` · `CanStartEncounter` · `CanSignCheckpoint` · `RealSecondsAt` · `MinuteAtRealSeconds`. 추적기 이벤트 `PhaseChanged`·`Call1`·`Call2`·`ResidualAlert`·`ShiftEnded`, `Reset(minute)`·`Advance(minute)`.
- **`WarningLedger`**·**`NightSnapshot`**·**`RestartPolicy`**(`Scripts/Stats/`): 경고 장부, 밤 시작/체크포인트 스냅샷, 재시작 규칙 수치(`ReliefFloor` 40 · `AbsenceAfterRestarts` 5 · `AbsenceNextNightCap` 60 · `CalmFrom` 2 · `SwapRuleFrom` 3 · `VoluntaryLeaveFrom` 4). `RestartResult(Kind, K, StartMinute, CapturedAxis)`.
- **`BandResolver.RestoreReached(int[])`**·`ReachedCopy()`: 연출 도달값을 내리는 **유일한** 길(재시작·결근만).
- **점검**(`Scripts/Inspection/`): `InspectionCatalog.All/Find/FindByTarget/InSpace` · `InspectionPlan`(Assignments·Spaces·LateSpace·Call1ItemId·Hallucinations) · `AnomalyAssigner.Build(day, shown)` · `InspectionBoard`(Report·Startle·Settle·CanReport·IsOpen·ReliefUsed, `ISnapshotable`). `NightRun.ReportInspection/InspectionStartle/MarkHallucination/SetReverseReport/Inspections`.
- **`SensingRules`**·**`ReportReadiness`**(`Scripts/Core/SensingRules.cs`): 판정 공통 정의 수치 한 곳. 센서가 숫자를 따로 들지 않습니다.
- **`FearAxisSystem.BeginFrame/EndFrame`**(한 프레임 두 축 100) · `Raised` 이벤트 · `NightRun.LastCaptureSources`·`RaisedSources(axis)`·`DisplaySource`.
- **새 수칙 판정**(`Scripts/FinalRules/`, 18차): `FinalRuleBook(deck, axes)` · `Dispatch(in JudgeSignal, bool judging)` · `EndNight()` · `NoteExternal(ruleId, violated, reason)` · `Judge(id)` · `Results` · `World`(공간·손전등·자세·달리기·판정 ms) · `Anchor`(기준점 위치 찾기, 테스트가 바꿈) · 이벤트 `Settled`·`ReverseReportArmed`·`EncounterRequested`, `ISnapshotable`. 판정기 = `FinalJudges.Create(RuleDef)`, 이름 = `FinalCues`. `NightRun.FinalRules`·`FinalResults`, `EventBus.FinalRuleSettled`. 발신기 `FinalRuleRelay`(Flow).
- **긴장 디렉터**(`Scripts/Direction/Tension/`, 19차): `TensionDirector(program, day, restarts, rng)` · `Observe(in JudgeSignal)` · `Tick(minute, dt, highestSensory, auditoryShown, captured)` · `TryDequeue(out JudgeSignal)` · `ForceEncounter` · `FireRuleNow`/`EndRuleNow` · `SkipPhase` · `AbortAll` · `ResetToRest(k, startMinute)` · `Runs`/`RuleRuns`/`Budget`/`Mood`/`Busy`, 이벤트 `Emitted`. 표 `EncounterScripts`·`RuleTriggers`, `SurpriseBudget`, `DirectorMoods`, `DirectionEvent`. `NightRun.Tension`·`DirectorAutoRun`·`DebugForceEncounter`·`DebugFireRuleCue`·`DebugEndRuleCue`·`DebugSkipDirectionPhase`·`DebugAddFinalRule`·`DebugLowerAxis`, `EventBus.DirectionEmitted`. Flow: `DirectionStage`·`StandInFactory`·`LightGroup`·`DirectionCue`·`NightDutyDebugConsole`(F3).
- ~~`DayDirector`~~ — **2026-10-03(25차) 폐기**(§4.3′).
- **`DutyLogEntry`·`DutyMark`·`RuleVerdict`**(`Scripts/Stats/DutyLogEntry.cs`): 근무일지 한 줄 = 그날 편성 수칙 하나(`NightRun.BuildSummary`, 덱 순서). 어기면 `Struck`, 역설 문자를 받고 어기면 `Instructed`(10단계). 미방문 빨간 줄 없음(26차).

### 4.3′ 옛 하루 덱 배정 — 폐기했습니다 (2026-10-03, 25차)

옛 24장 카드 풀(`NightDeckTable`)에서 하루 6장을 축 쿼터로 뽑던 배정기(`DayDirector`)와 그 카드 풀은 **옛 24장 카드와 함께 지웠습니다.** 그날 수칙은 새 편성(`ProgramDirector` → `FinalRuleBook`)이 정합니다. `NightRun.LoadDeck`은 `DeckOverride`가 없으면 빈 덱을 돌려주고, `Resources/NightDeckTable.asset`은 비워 둡니다(김진선님 `TabletDocument`의 밤 시작 전 폴백이 읽으므로 타입·에셋은 남김).

### 4.3″ 옛 조우 8장면 — 2026-10-03 폐기 (기록)

> 옛 조우 연출기(`EncounterDirector`·`EncounterTableSO`·`EncounterStager`)는 새 편성에서 한 번도 돌지 않아 26차에 지웠습니다. 조우는 `ProgramDirector`(편성)·`TensionDirector`(시점)·`DirectionStage`(실행)가 합니다. 아래는 기록입니다.

| 일차 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| **장면 수** | 1 | 1 | 2 | 2 | 2 |

**어느 날 어느 장면인지는 회차마다 다시 뽑습니다**(2026-09-22, 기획서 v6 §4 「날짜별 공간을 고정하지 않는다」). 고정된 것은 **장면 수**뿐이고, 합이 8이라 전소진이 성립합니다. 뽑기는 거절 표본(섞어 보고 제약이 깨지면 다시)이며, 64번 모두 실패하면 옛 고정표로 물러납니다 — 정상 경로에서는 쓰이지 않습니다.

**뽑기가 지키는 제약 넷.** 하나라도 풀면 설계가 무너집니다.

1. **1일차는 S-A 하나.** 최초 조우이고 기획서 §3-3이 「이전 근무자의 마지막 확인 장소는 과학실」로 못박았습니다.
2. **발견형 3장(H-B · C-B · T-B)은 3·4·5일에 한 장씩.** 두 선택 모두 델타 0이라 **잘하는 플레이어가 수치 손해 없이 겪는 유일한 공포**입니다.
3. **하루 장면 수**(위 표). 상한은 이월 포함 3장입니다 — 그 이상은 공포가 아니라 소란입니다.

> **2026-09-22 자격 재설계로 제약 하나가 사라졌습니다.** S5·T3의 배치 자격이 Band1·Band2였을 때는 「S-B는 3일차부터 · T-A는 4일차부터」를 지켜야 했습니다 — 두 카드의 트리거 ID가 곧 장면 ID라, 더 이르면 **장면은 있는데 카드가 없는 밤**이 됐기 때문입니다. 두 카드를 Band0으로 내려(§2.3′) 매듭을 풀었습니다. 되살리지 마십시오.

**실측(300회차)** — S-A만 1일차 고정이고 나머지 일곱은 2~5일에 22~38%로 고르게 흩어집니다. 발견형 셋은 3·4·5일에만.

- **그날 배분이 이월보다 먼저 자리를 가집니다.** 거꾸로 하면 상한 3장에 걸려 새 배분이 계속 밀리고 회차 뒤쪽 장면이 한 번도 안 깔립니다 — 2026-09-22에 실측으로 6/8이었습니다. 접근 단계는 이 순서에 걸려 있지 않습니다(`_placedStep`이 회차 내내 누적).
- 장면 확정은 **그날 첫 일반 점검을 마친 뒤**입니다. 그 전에는 아무것도 준비하지 않습니다.
- 배분 검사는 **여러 시드로** 돌립니다(`EncounterPlanTests`, 40회차). 한 시드만 보면 우연히 통과합니다.

**필연 조우 3단계** — 안 보려는 플레이어도 결국 만나되, 금기(강제 시선·체류 즉사·추격 AI)는 지킵니다.

1. **접근 단계화** — 못 보고 퇴실할 때마다 모형이 한 칸 다가옵니다(최대 3). **접근 단계는 밤을 넘어 누적**됩니다 — 매일 1단계로 되돌리면 「어제 못 본 것이 오늘 더 가까이 온다」가 죽습니다.
2. **퇴실 게이트** — 안 본 채 나가려 하면 출입구를 지나는 순간 모형이 문과 플레이어 사이에 섭니다. **`S-B` · `T-A` · `H-B` 셋뿐**입니다(전부에 붙이면 패턴이 읽힙니다). 하룻밤 1회, **출구를 막지 않습니다.**
3. **다음 날 이월** — 그래도 안 봤으면 장면이 소비되지 않고 넘어갑니다.

**시야 규약은 예외가 없습니다.** 배치는 `IsVisible`이 false일 때만 실행되고, 시야 안이면 대기합니다. 게이트도 마찬가지입니다. `IsVisible`이 예외를 던지면 **「보인다」로 봅니다**(판단 불가일 때 안 움직이는 쪽이 안전). 미주입이면 「항상 안 보임」 + 경고 1회 — 반대로 두면 조우가 조용히 사라져 빈 게임이 되는데 그게 알아채기 훨씬 어렵습니다.

**문자** — G의 주인은 카드가 아니라 **조우 장면**입니다(`S-A`에만 없습니다). 서로 다른 점검 2곳마다 1통. **자연 발견 시 취소가 아니라 다음 날 이월**이라 7통이 전부 소진됩니다. N1은 2일차 고정·회차 1회이고 **역설을 보낸 날에는 나가지 않습니다.**

### 4.3‴ 태블릿 업무 상태 문구 — 판정이 아닙니다 (2026-09-22) — 2026-10-03 폐기, 기록

**2026-10-03 폐기** — `DayBriefText`(옛 24장 기준 업무 상태 문구, 읽는 곳 0)는 지웠습니다. 새 1일차 온보딩 문구는 최종 기획서 「1일차 온보딩」이 정본이며 9단계에서 새로 씁니다. 아래는 기록입니다.

**카드로 만들지 마십시오.** 역설(P)·발견유도(G)·재방문(N) 어느 덱에도 속하지 않습니다. 읽지 않았다고 델타를 부과하지 않고, 수락·거절·미도달 벌점도 없습니다. 카드로 만드는 순간 판정 대상이 됩니다.

| 상수 | 언제 |
|---|---|
| `HandoverNotice` | 1일차, 태블릿을 주운 직후 맨 처음(퇴실 기록 부재) |
| `SafetyNotice` | 1일차, 수칙 첫머리(사고 기반 안내) |
| `FirstCardHowTo` | S1 조작 안내. **수칙 본문과 구분해** 표시합니다 |
| `FirstEncounterNotice` | 최초 조우 관찰 직후 한 줄 |
| `BriefFor(day)` | 2~5일차 시작 태블릿의 한 줄 |
| `ExitInstruction` · `ExitConfirmed` · `ExitRemaining` | 엔딩 E04 · E08 |

**DAY는 바깥 날짜가 아니라 회사가 붙인 관측 회차**입니다. 04:00에 의식을 잃고 다시 00:00의 경비실에서 깨어나지만 DAY 숫자와 완료 기록은 올라갑니다. 그래서 2일차 문구가 「1회차 관측 기록이 접수되었습니다」로 시작합니다.

**특정 공간을 지목하지 않습니다.** 조우 공간이 회차마다 달라지므로(§4.3″) 「이미 어디를 봤다」고 단정할 수 없습니다.

**표시 주체는 태블릿 UI입니다. 아직 없으므로 지금은 아무도 읽지 않습니다.**

### 4.6 문 상호작용 — 조준선이 가리키는 것 하나 (2026-09-22)

`Assets/_Game/Flow/Interaction/`. **게임 코드입니다**(하네스가 아닙니다). 파일 셋이고 각각 하나씩만 압니다.

| 파일 | 아는 것 | 모르는 것 |
|---|---|---|
| `PlayerInteractor` | 조준·게이트·키·판정 출처 | 문이 어떻게 생겼는지 |
| `DoorHandle` | 벤더 `DoorScript`를 어떻게 여닫는지 | 누가 언제 부르는지 |
| `InteractionHud` | 조준선과 안내 줄 | 판정도 상호작용도 |

**흐름.** 카메라 앞으로 2.5m 레이 → 맞은 콜라이더의 부모에서 문을 찾음 → 상태에 맞는 안내 → `E`.

```
PlayerInteractor.Update (실행 순서 50)
  ├ 게이트: 일시정지 · PlayerSensors.TabOpen · NightRun.IsCaptured
  │   (밤이 아닐 때는 막지 않습니다 — 출근·퇴실은 밤 바깥입니다)
  ├ Physics.RaycastAll(2.5m, 트리거 포함) → 가장 가까운 DoorHandle
  ├ 안내: 「[E] 문 열기」 · 「[E] 문 닫기」 · 「잠겨 있습니다」
  │   자동문(AUTOMATIC)과 스스로 닫히는 문(autoClose)은 안내하지 않습니다
  └ E → DoorRelay.BeginPlayerMove() → DoorHandle.Open()/Close()

DoorRelay.Update (실행 순서 60)  ← 같은 프레임의 뒤
  └ Animation이 움직이기 시작 → ResolveSource() → Player
```

**⚠ 판정 출처가 이 설계의 핵심입니다.** `DoorCommandAccepted`의 `ActionSource`가 `Player`인지 `Direction`인지가 **C6·H1·T3를 가릅니다**(§4.4.1). 예전에는 「0.4초 안에 아무 데서나 E가 눌렸나」라는 추측뿐이었습니다. 이제 상호작용기가 **조준한 그 문에만** `DoorRelay.BeginPlayerMove()`를 걸어 못 박습니다. 추측 경로는 폴백으로 남아 있습니다.

그러려면 벤더가 자기 키로 몰래 여는 일이 없어야 합니다. 그래서 첫 프레임에 문 56개의 **`controls.openButton`을 `KeyCode.None`으로 거둡니다**(`DoorHandle.SilenceVendorInput`, 런타임 값만). 이걸 빼면 같은 `E`에 둘이 동시에 반응해 **한 프레임에 두 번 열리고**, 벤더가 먼저 열면 출처를 못 박을 기회가 없습니다.

**벤더의 열기 규칙을 그대로 옮겼습니다**(`DoorScript.Update` 226~247행). 잠금이 꺼져 있으면 `OpenDoor`, 켜져 있고 열쇠가 있으면 `OpenLockDoor`, 열쇠가 없으면 `PlayClosedFXs`(덜컹, 안 열림)입니다. 잠긴 문은 움직이지 않으므로 **판정 신호도 나가지 않습니다.**

**`Assets/NOT_Lonely/`를 타입으로 참조하지 않습니다.** `DoorHandle`이 전부 리플렉션으로 벗겨 씁니다 — `DoorRelay`가 `UnityEngine.Animation`만 보는 것과 같은 이유입니다(§5.1-2). 벤더 폴더가 없는 팀원의 빌드도 깨지지 않고, 문을 못 찾으면 조용히 쉽니다. **나중에 `_Game`에 자체 문 컨트롤러를 만들면 `DoorHandle` 하나만 갈아 끼우면 됩니다.**

**설치.** `NightRunDriver`와 같은 방식입니다 — 씬에 이미 있으면 건너뛰고, 없으면 `AfterSceneLoad`에 자기가 섭니다. `InteractionHud`는 **`PlayerInteractor`가 부릅니다**(각자 `RuntimeInitializeOnLoadMethod`로 서면 순서가 보장되지 않아 서로를 못 찾습니다 — 실측).

**HUD는 씬에 이미 있는 것을 씁니다.** `HUD_Play/Img_Reticle`의 알파를 평소 0.25 → 대상을 잡으면 0.95로 올립니다. 안내 줄(`Txt_Prompt`)만 없어서 없으면 런타임에 만들고, **글꼴은 같은 캔버스의 다른 글씨에서 빌립니다**(TMP 기본 글꼴에 한글이 없어 그냥 만들면 네모로 나옵니다). 민이 씬에 `Txt_Prompt`를 만들어 꽂으면 그쪽을 씁니다.

**잠금 무시.** `PlayerInteractor.IgnoreLocks`는 기본 꺼짐(디버그 콘솔도 이제 켜지 않음, 개요 탭 [잠긴 문 무시]로 켤 수 있음). 동선의 문은 정책(`DoorPolicy.asset`)이 「열리는 문」이면 시작할 때 벤더 잠금을 푼다(21차 ⓘ).

**확인**(실측 2026-09-22): 정문 앞 1.8m에서 문짝 가운데 조준 → 안내 「[E] 문 열기」, 조준선 0.95. `toilet.door`를 열면 콘솔에 `[DoorRelay] DoorCommandAccepted(None, 'toilet.door', Player, False, 0)`. 벤더 키를 거둔 문 56/56. EditMode 362/362.

**조준은 좁습니다(2026-09-22 좁힘).** 사거리 **1.8m**, `SphereCast` 반경 **6cm**, **트리거 무시**입니다.
- 2.5m·`RaycastAll`이었을 때는 복도 건너편 문까지 안내가 떴습니다. 게다가 `RaycastAll`은 벽에 가린 문도 잡았습니다.
- 트리거를 세면 안 됩니다 — 문마다 앞에 벤더의 커다란 트리거 상자(1.2 × 2.2 × 3.3m)가 서 있어서, **문을 보지 않고 서 있기만 해도** 잡힙니다. 문짝·손잡이·몸통에는 트리거가 아닌 콜라이더가 따로 있습니다.
- 반경 0이면 안 됩니다. 여닫이 **두 짝 사이 실틈으로 레이가 빠져나가** 정문 한가운데를 겨눴는데 아무것도 안 잡혔습니다(실측).

**열리는 문은 기획이 정합니다** — `Resources/DoorPolicy.asset`(`DoorPolicySO`). 메뉴 「NightDuty/문 개폐 정책 에셋 생성」으로 만듭니다.

| 분류 | 무엇 | 안내 |
|---|---|---|
| `Storage` | 이름이 `Locker`·`Bookcase`·`Drawer`·`TeacherTable`… 로 시작 — 서랍·사물함·책장 | 「[E] 열기」 |
| `Openable` | 표에 적힌 경로·판정 ID, 또는 `JudgeTarget`이 붙은 문 | 「[E] 문 열기」 |
| `Sealed` | 그 밖의 문 | 「잠겨 있습니다」 + 벤더의 덜컹 연출. 문이 안 움직이므로 **판정 신호도 안 나갑니다** |

실측 분류(2026-09-22): **수납가구 30 · 열리는 문 10 · 잠긴 문 16.** 목록은 씬을 실측해 추린 **첫 안**이고 정본이 아닙니다 — 민이 걸어 보고 인스펙터에서 고치십시오. 정책은 절대적입니다: **F4(잠금 무시)는 열쇠만 건너뛰지 기획을 건너뛰지 않습니다.** 정책까지 무시하려면 F5입니다.

**민이 씬에서 할 일:** ① `PlaySystems` 프리팹에 `PlayerInteractor`·`InteractionHud`를 붙이면 자동 설치가 사라집니다. ② `HUD_Play`에 `Txt_Prompt`를 정식으로 만듭니다. ③ 잠긴 문 12개의 열쇠 연출. ④ `doorSounds` 클립 3종 발주(열림·닫힘·잠김).

---

### 4.4 시스템 ↔ 게임 흐름 연결 (2026-09-17 구현, 담당 Lee)

```
GameSession.StartNewRun() ──▶ NightRun.StartNewRun()
Play 씬 로드 ─ GameTime이 있으면 NightRunDriver 자동 생성 (Assets/_Game/Flow/, Assembly-CSharp)
   Start ─▶ BeginNight(GameSession.CurrentDay, gameTime.CurrentMinutes)
   Update ─▶ 시계가 흐를 때만 Tick(Time.deltaTime)  ← 판정 시간은 실제 초. 배속 곱하지 않음
   GameTime.ShiftEnded ─▶ RequestEndNight() ─▶ DayEnded ─▶ PlayResultRouter ─▶ NightDutyResultMapper ─▶ Result 씬
   포획 ─▶ Captured(구독자 호출 뒤 NightRun이 밤을 스스로 닫음) ─▶ CaptureDirector 얼굴·재시작 카드 ─▶ RestartAfterCapture ─▶ 출근 자리
         (결근이면 DayEnded ─▶ 결과창. Captured 구독자가 없으면 옛 AxisCritical ─▶ 진선님 사망 화면)
   OnDestroy(밤이 열린 채) ─▶ NightRun.AbandonNight()
```

- **구동기는 씬·프리팹에 놓지 않아도 됩니다**(자동 생성). 직접 놓으면 자동 생성은 건너뜁니다. 씬 전환 때 옛 구동기가 새 밤을 버리지 않도록 소유자 검사를 합니다.
- 결과창 근무 일지는 `DaySummary.DutyLog`(`DutyLogEntry`: 덱 순서 번호·본문·빨간 줄)에서 옵니다. 줄 = 그날 편성 새 수칙, 빨간 줄 = 어긴 수칙(`RuleVerdict.Violated`). 카드 ID는 넘기지 않습니다.
- 위반 시각은 `GameTime.FormatTime`으로 결과창과 같은 표기(12시간제)로 바꿉니다.
- 라우터의 `ShiftEnded` 가짜 결과는 **밤이 없을 때(구동기 없는 시험)만** 씁니다. 디버그 메뉴 「Force Death」는 `NightRun.DebugForceCapture`를 거칩니다.
- 남은 TODO: **게임 시계를 00:00~04:00으로 맞추고 `ShiftEnded`를 04:00에 물리기**(§8-7), 태블릿 `Tab` 신호(`JudgeSignal.Tab`)와 시계 정지(Q6, 진선), **플레이어 센서 씬 부착**(코드는 `Flow/Sensors/`에 이미 있습니다 — §4.2), `ResultController`의 「충돌 처리」 표시 숨김·`ImprintAxis` 정리.
- **종료 요청 거절 처리(옛 Q2)는 소멸했습니다.** 04:00이 되면 무조건 끝납니다.

### 4.4.1 신호 규칙 (상세는 인수인계서 §5)

- **호출 흐름:** 메인 시작 → `StartNewRun()` / Play 시작 → `BeginNight(GameSession.CurrentDay, () => 현재 게임 분)` / 매 프레임(Tab·일시정지 아닐 때) → `Tick(Time.deltaTime)` / `GameTime.ShiftEnded` → `RequestEndNight()` → `DayEnded(DaySummary)` → 결과 저장 → Result 씬 / `Captured` → `CaptureDirector` → `RestartAfterCapture()`(결근이면 `DayEnded`).
- **신호 규칙:** 응시·근접 샘플은 **0.1초 고정 간격**(반드시 **누산기**로 — §5.5-18), 응시는 대상이 없어도 빈 ID로 보냄. 판정 시간은 `Tick`으로만(`JudgeSignal.Tick`을 Send하지 않음). Tab 중에는 `JudgeSignal.Tab(bool)`만. 출처는 `ActionSource.Player`/`Direction`. `NightBegan`·`NightEndAccepted`는 NightRun이 만들므로 보내지 않음.
- **같은 순간 순서:** `Tick` → `PassageCompleted` → `ZoneExited` → `InspectionCompleted` → `SpaceExited`.
- **대상 ID**는 소문자·숫자·점이며 카드 데이터와 글자까지 같아야 합니다(예: `corridor.door.13`, `cls11.chalk3`, `toilet.stall.inner`, `scene.sb`). 목록은 인수인계서 §5.4.
- 카드별 「보내기 전 조건」(코어가 모르는 씬 조건)은 인수인계서 §5.3에 있습니다. 클라이언트가 확인하고 보냅니다.
- 플레이어 센서가 **씬에 붙어 있지 않아**(코드는 있습니다) **실제 플레이에서는 거의 모든 카드가 미판정으로 끝납니다.** 통합 확인은 지금도 디버그 신호로 하지만, 이번 주 목표는 **디버그 신호 없이 걸어서** C1 한 장을 판정시키는 것입니다(§12.1-1).

### 4.5 코드에 남은 폐기 흔적 — 새 코드에서 쓰지 마십시오

| 위치 | 현재 | 조치 |
|---|---|---|
| ~~`Bands.RedThreshold`~~ | **2026-10-03 삭제** | — |
| ~~`ClauseZeroType`·`EventBus.DayStarted`·`IDocumentView`~~ | **2026-10-03 삭제**(보내는 곳·구현이 0) | 되살리지 마십시오 |
| `DaySummary.ImprintAxis`, `Conflicts*` | 호환용(null/0) | 진선님 `ResultController`가 아직 `ImprintAxis`를 씀 → 연결 작업 때 함께 제거 |
| `AxisCritical(FearAxis)` | 옛 사망 통로 — `Captured` 구독자가 없을 때만(근무 씬에선 안 옴) | 새 코드는 `Captured`를 구독. 원인은 `NightRun.Cause`. 1회 발화·이후 델타 중단은 `FearAxisSystem`이 보장. **신뢰로는 발화하지 않음** |
| 진선님 `FakeDayData` | `criticalAxis == Trust`면 신뢰 100 표시 | 신뢰 포획 경로가 없어졌으므로 연결 작업 때 정리 |

- Band4 = 90~99, 100 = 종료 잠금, 전 공간 Band4 등 0개는 **코드에 반영**됐습니다(옛 `BandTableSO`는 2026-10-03 폐기).

---

## 5. 구현 규칙과 함정

- **테스트 전에 씬을 저장하십시오(19차).** 열린 씬이 수정된 채로 EditMode 테스트를 돌리면 「씬 저장?」 대화상자가 떠 에디터가 멈춥니다(원격 세션에서는 아무도 누르지 못함). `EditorSceneManager.SaveOpenScenes()` 뒤에 돌립니다. 플레이 직후에는 `NightRunDriver.OnDestroy`가 정적 규칙 스위치를 되돌렸는지(`NightRun.ProgramEnabled == false`) 확인합니다.

### 5.1 C#과 어셈블리

1. **C# 9까지만** 씁니다. file-scoped namespace(`namespace X;`), `record`, `global using`은 **금지**입니다.
2. **`Assets/NOT_Lonely/`의 스크립트를 `_Game` 코드에서 참조하지 마십시오.** 벤더 스크립트는 asmdef가 없어 `Assembly-CSharp`에 들어가므로 asmdef 어셈블리에서 참조할 수 없고, 폴더가 gitignore라 패키지가 없는 팀원의 빌드가 깨집니다. 프리팹·모델·머티리얼은 씬에서 GUID로 참조해도 됩니다. `SimpleFPController`는 참고용이며, 플레이어 컨트롤러는 `_Game`에 새로 만듭니다.
3. `Editor` 어셈블리는 `Assembly-CSharp`을 참조할 수 없습니다. 개인 폴더 타입이 필요하면 `Type.GetType("이름, Assembly-CSharp")`를 씁니다.
4. `.csproj`는 `.cs`가 하나 이상 있는 어셈블리에만 생성됩니다. 빈 어셈블리에 `.csproj`가 없는 것은 정상입니다.

### 5.2 이벤트와 컴포넌트

5. **`from == to`인 `BandChanged`가 옵니다**(전체 재방송, `OnEnable` 기준값 송출). **연출은 멱등해야 합니다.**
6. **`OnDisable`에서 `EventBus` 구독을 반드시 해제**하십시오. static이라 씬을 바꿔도 살아 있어 파괴된 오브젝트로 이벤트가 날아갑니다.
7. **`EventBus.ClearAll()` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`은 죽은 코드가 아닙니다.** 도메인 리로드를 끄면 구독이 살아남아 중복 발화합니다. 지우지 마십시오.
8. `AddComponent` 직후 `OnEnable`이 즉시 실행됩니다. `SerializedObject`로 필드를 채우기 **전**이므로 초기화를 `OnEnable`에만 의존하지 마십시오.
9. 조명 intensity에 배수를 곱할 때는 **원본 값을 따로 보관**하십시오. 현재 값에 곱하면 매 프레임 지수적으로 어두워집니다.

### 5.3 판정 구현

10. 데이터는 ScriptableObject + `[SerializeReference]` 폴리모픽으로 만듭니다. 기획팀이 인스펙터에서 24장을 직접 편집합니다. 필드는 정본 5절(`cardId, playerText, eligibleBand, triggerId, targetIds, successCondition, failureCondition, settleAt, successDelta, failureAxis, failureDelta, radius, graceSeconds, gazeSeconds`)을 기준으로 하되, 이름은 예시이므로 팀 구조에 맞춥니다.
11. **모든 카드의 씬 대상 참조가 연결됐는지 시작 전에 검사하는 코드**를 만듭니다. 누락은 미판정으로 처리합니다.
12. **시야 밖 판정에 `OnBecameInvisible()`을 쓰지 마십시오.** 씬 카메라·그림자 카메라에도 반응해 에디터에서만 오작동합니다. `GeometryUtility.TestPlanesAABB`(카메라 프러스텀) + `Physics.Linecast` 가림 검사 + N프레임 연속 비가시를 조합합니다.
13. 식별(0.2초) 타이머와 금지 응시 타이머는 분리합니다.
14. 연출 구간은 내려가지 않습니다(래칫). 히스테리시스는 없습니다 — 생존 수치가 줄어도 연출은 도달값(`Peak`) 기준입니다. **색온도만 구간 안에서 보간합니다** — 9.20V가 등 개수를 삭제했으므로 등을 끄고 켜는 스냅은 없습니다.

### 5.4 오디오

15. **「정확히 세 번」(C1) 같은 횟수 단서는 반드시 단발 클립**으로 발주합니다. 루프 클립은 횟수를 셀 수 없어 해당 카드를 판정할 수 없게 됩니다. C1은 세 획 전체가 전달돼야 활성화됩니다.
16. T2의 12초 시퀀스는 판정 데이터와 클립 길이를 같은 값으로 씁니다.
17. 분위기 효과음과 모형 효과음은 카드 단서 ID를 보내지 않습니다(§2.7).

### 5.5 신호·센서 함정 (2026-09-20 실측)

18. **0.1초 고정 간격은 반드시 누산기로 구현하십시오.** `SpaceZones`는 `Time.time` 게이트 방식이라 실제 0.116초가 흘러도 0.1초로 보고하며 **드리프트합니다.** 근접에는 영향이 작지만, **같은 방식으로 응시를 보내면 H2·C3의 3초가 실제 3.4초**가 됩니다. 남은 시간을 빼고 더하는 누산기(`_acc += dt; while (_acc >= 0.1f) { _acc -= 0.1f; … }`)를 쓰십시오.
19. **`FPController`가 `transform.Translate`로 움직여 벽 충돌이 없습니다.** 판정 공간을 **걸어서 통과**할 수 있어 공간·근접 판정을 믿을 수 없습니다. 고치기 전에 잰 실측값을 근거로 시험값을 확정하지 마십시오.
20. **Tab 중 공간 전환 신호가 유실됩니다.** `SpaceZones`는 Tab 여부를 모른 채 `_current`를 갱신하는데 코어는 Tab 중 신호를 버립니다 → **「나간 적 없는 공간에서 나감」** 상태가 생깁니다. 태블릿을 여는 쪽과 공간 추적을 같은 게이트로 묶으십시오(Q6이 미결이라 지금은 실재 위험입니다).
21. **`JudgeTarget.IdOf`는 `GetComponentInParent`로 ID를 찾습니다.** 따라서 **콜라이더가 없는 빈 오브젝트는 레이가 영원히 맞히지 못합니다.** 식별·응시 대상에는 작은 `BoxCollider`를 붙이고 Renderer는 꺼 두십시오. 근접은 **수평(XZ) 거리**만 보므로 높이는 아무래도 좋습니다.
22. **`JudgeWorld.FlashlightOn`은 매 밤 `false`로 시작합니다.** 손전등을 **켜 둔 채 밤이 시작되면, 초기 1회를 보내지 않는 한 코어는 꺼진 것으로 압니다.** `Flashlight(isOn)`은 상태가 바뀔 때마다(에지) **그리고 밤 시작 직후 현재 상태 1회**를 보내야 합니다. 유예 2초는 코어가 세므로 센서 쪽에 타이머를 만들지 마십시오.
23. **런타임에 만든 오브젝트에 `HideFlags.DontSave`를 붙이지 마십시오.** 이름과 정반대로 동작합니다 — 씬이 내려갈 때 **파괴를 면해** 에디터 메모리에 남고, 플레이할 때마다 쌓입니다(2026-09-22 실측: 두 번 플레이에 잔재 80개, 다음 플레이에서 중복 인스턴스 2개). 런타임 생성물은 hideFlags를 **건드리지 않는 것**이 맞습니다: 플레이 종료 시 씬과 함께 사라지고, 플레이 중에는 씬 저장 자체가 막혀 있어 씬 파일이 더러워질 일이 없습니다. 잔재가 이미 있으면 `Resources.FindObjectsOfTypeAll<GameObject>()`로 이름을 훑어 `DestroyImmediate`로 지웁니다(`FindAnyObjectByType`로는 안 잡힙니다 — §3.3-4).

---

## 6. 폴더 구조

- `Assets/_Game/` — **팀의 실제 게임 콘텐츠.** Scripts/, Tests/, ScriptableObjects/, Resources/, Scenes/, Prefabs/, Materials/, Art/, Audio/. 전부 git으로 추적합니다. 벤더 프리팹을 고칠 때는 원본을 수정하지 말고 `Assets/_Game/Prefabs/`에 **Prefab Variant**를 만듭니다.
- `Assets/3.1. Programmer_lee/` — Lee 개인 폴더. `01 Scene/PlayScene_Lee.unity`(2026-09-30 `0. Main` PlayScene 복사본 — 메인 씬을 다른 사람이 쓰는 동안 Lee 작업용. 라이팅 데이터는 원본 것을 같이 참조하며, 빌드 목록에는 없음), `02 Scripts/`(시험 리그·판정 디버그 패널).
- `Assets/3.2 Programmer_Kim/` — 진선님 개인 폴더. SceneFlow·SceneFlowConfig·GameTime·GameSession·DayResult·DayIntro·ResultController·AxisBarView·DutyLogView·ViolationLogView·HUDActions·PlayResultRouter·`PlaySystems.prefab`.
- `Assets/0. Main/` — **공통 작업 폴더**(2026-09-17). 게임 흐름 씬 `01 Scene/{Main,Loading,Play,Result,Contract}Scene`과 `06 Data/`(SceneFlowConfig · LoadingTipTable)가 여기 있습니다. 프리팹 · 스크립트는 아직 진선님 개인 폴더에 있습니다.
- 개인 폴더는 원칙적으로 **일회성 실험과 단일 시스템 테스트 씬 전용**입니다. 공유 코드와 출시 콘텐츠는 처음부터 `_Game`에 둡니다. 나중에 옮기면 SO·프리팹의 스크립트 참조가 모두 깨집니다(진선님 GameFlow 이전 여부는 Q8).
  - 폴더명 표기가 불일치합니다(`3.1.` 뒤에는 점이 있고 `3.2` 뒤에는 없습니다). **경로를 하드코딩하지 마십시오.**
  - `3.2 Programmer_Kim`은 진선님 폴더이며 브랜치명(`Programmer_Jinsun`)과 다릅니다.
- `Assets/1. Design/`, `Assets/2. Art/` — 기획·아트 팀 작업 폴더.
- `Assets/NOT_Lonely/` — 서드파티 패키지(`HQ_AbandonedSchool`, `Object Placement Tool`, `SimpleFPController`). **gitignore 대상**이며 팀원마다 **같은 버전**을 로컬에 설치합니다.
- `Assets/Scenes/`, `Assets/Settings/` — URP 템플릿 기본 씬과 렌더 설정.
- `.gitkeep`은 의도적으로 비워 둔 폴더 표시입니다. 실제 파일이 생긴 폴더의 `.gitkeep`은 지웁니다.
- `../Docs/` — `형상관리_매뉴얼.md`, `Claude outputs/`(정본 HTML·인수인계서·게임플로우 가이드).

---

## 7. 씬과 라이팅

- 조도 연출용 라이트는 **Point/Spot + Realtime**입니다. Mixed는 간접광이 남아 Band4 「전부 소등」에서도 방이 은은하게 밝습니다.
- **벤더 데모씬의 조명은 전부 Area Light(베이크 전용)** 이라 실시간으로 켜고 꺼도 화면에 반영되지 않습니다.
- URP Forward+이므로 오브젝트당 라이트 수 제한은 없습니다. 다만 Additional Light Shadow 아틀라스가 2048이라 **등 8개가 모두 그림자를 던지면 부족**합니다. 대부분 `Shadows: None`으로 둡니다.
- 새 씬은 `Assets/_Game/Scenes/`에 새로 만듭니다(공간 4개 + 경비실).
- **벤더 데모씬 사본(`DemoScene_LeeTest.unity`, 약 19MB)은 부품 창고로만 씁니다.** 그 안에서 작업하거나 커밋하지 마십시오. 로드가 느리고 `Ran out of Graphics Ring Buffer space`를 일으킬 수 있으며, 병합 충돌은 사실상 해결할 수 없습니다.

---

## 8. 환경 필수 수정과 알려진 문제

1. **벤더 셰이더 수정 — 모든 PC에서 각자 해야 하며 커밋할 수 없습니다.**
   `Assets/NOT_Lonely/HQ_AbandonedSchool/Shaders/NOT_Lonely_LightRays.shader`에서 `uniform float4 _CameraDepthTexture_TexelSize;` **두 줄을 삭제**합니다(원래 224행과 507행).
   URP 17.3이 이 변수를 자체 선언하므로 d3d11에서 재정의 에러가 납니다. 변수는 쓰이지 않아 기능 변화가 없습니다.
   **증상:** `redefinition of '_CameraDepthTexture_TexelSize'`. `URP.unitypackage`를 다시 임포트하면 수정이 되돌아갑니다.
2. `HQ_AbandonedSchool`은 Built-in RP 형태로 배포되며, 함께 들어 있는 `URP.unitypackage`를 반드시 임포트해야 합니다(현재 작업본은 임포트 완료).
3. 콘솔의 `Account API did not become accessible...` 경고는 무시해도 됩니다.
4. 압축 해제 시 백신이 `.cs`/`.unity` 파일을 격리하는 사례가 있었습니다. 벤더 폴더에 파일이 실제로 있는지 확인하십시오.
5. ~~`BandTable.asset` 조도 Tint~~ — 옛 조도 리그와 함께 2026-10-03 폐기(지금은 `Resources/IlluminanceTone.asset`).
6. Scene 뷰가 초록색이면 조명 문제가 아니라 디버그 드로우 모드(`Contributors / Receivers`)가 켜진 것입니다. 해제법을 아직 못 찾았으니 **Game 뷰**를 쓰십시오.

**기획서와 어긋나 있는 현행 씬·에셋·코드 (2026-09-20 실측, 전부 고쳐야 할 것):**

7. ~~**게임 시계가 0:00~6:00**~~ — **2026-09-22 해결.** `PlaySystems.prefab`을 **00:00~04:00**으로 고쳤습니다(기획서 v6 §3-4 「04:00 유예안 삭제」). **배속은 아직 ×20**이라 하루가 현실 12분입니다 — 기획 잠정값 ×30~34로 가면 7~8분입니다. 바꾸기 전에 한 번 걸어서 실제 동선 시간을 재야 합니다(§12.1-5).
8. **`FPController`에 달리기(`runSpeed`, LeftShift)와 점프(`jumpForce`, Space)가 남아 있습니다.** 기획서 9절은 걷기만입니다(§2.6). 제거 대상입니다.
11. `corridor.box`(34.3, 3.4, 44.5)가 경비실 제외 상자(z 44.55~47.85) 경계에서 **0.05m** 떨어져 있습니다. H4 시험 전에 확인하십시오.
12. **C6의 문 절반은 현재 검증되지 않습니다.** 문 신호가 없으니 `DoorObligationCondition`은 늘 「의무 없음」이고 두 교실 점검 여부만으로 판정됩니다. 준수가 나와도 절반짜리입니다.
13. ~~옛 `ParadoxDirector` 발송 시점 문제~~ — 2026-10-03 옛 역설 연출기 폐기로 해당 없음(새 역설은 10단계).
14. ~~**문을 여는 안내가 없습니다**~~ — **2026-09-22 해결.** `_Game/Flow/Interaction/`의 상호작용이 조준선과 안내 줄을 맡습니다(§4.6). **남은 것은 씬 쪽입니다:** `doorSounds`의 클립 3종이 전부 비어 있고(열림·닫힘·잠김 발주 필요), 문 12개가 잠겨 있는데 열쇠 연출이 없습니다. 벤더의 `doorTexts`는 이제 쓰지 않습니다.
15. **`GameTime.use12HourFormat = true`라 자정이 `12:00`으로 보입니다.** 00:00~04:00 근무가 화면에서는 12:00 → 4:00으로 읽혀 **낮처럼 보입니다.** 24시간제로 바꾸거나 AM/PM을 붙여야 합니다.
16. ~~**라커 142개가 실시간 추가광을 하나도 못 받습니다**~~ — **2026-09-22 씬에 반영 완료.** 원인: 벤더 셰이더 `NOT_Lonely_MaskedPBR`(그리고 `NOT_Lonely_Tesselation`)의 키워드 스페이스에 **`_CLUSTER_LIGHT_LOOP`가 없습니다.** URP 17은 Forward+에서 이 키워드로 클러스터 라이트를 순회하는데, 없으면 `GetAdditionalLightsCount()`가 0을 돌려줍니다. 복도 스팟 바로 아래 라커만 새까맣게 남습니다. 텍스처·머티리얼·라이트맵은 전부 정상입니다. → **셰이더를 `Assets/2. Art/Shaders/`로 복사해 고치고**(`#pragma target 4.5` + 클러스터·리플렉션 프로브 키워드 추가), 머티리얼 사본을 만들어 라커 142개에 지정합니다. 벤더 폴더는 gitignore 대상이라 원본을 고치면 커밋할 수 없습니다.
17. ~~**계단 바리케이드가 라이트맵에 없습니다**~~ — **2026-09-22 씬에 반영 완료(49개).** 원인: `ContributeGI` 스태틱인데 `lightmapIndex = -1`입니다 — 원본을 복제해 옮기고 재베이크를 안 했습니다. 계단 바로 위 Baked 면광원 2개(intensity 15)의 빛이 라이트맵 안에만 있어서, 같은 프리팹인데 바로 옆 형제(`lm=3`)는 밝고 이쪽은 새까맣습니다. → 가장 가벼운 해법은 그 12개의 **`Contribute GI`만 해제**하는 것입니다. 그러면 `Corridor_LightProbes`(505개)에서 받습니다. 재베이크 불필요.
> **㉑㉒는 2026-09-22에 씬에 반영했습니다.** 도구는 `_Game/Flow/Editor/SceneArtFixes.cs`(메뉴 「NightDuty/아트/…」)에 남아 있습니다.
> - 라커 **142개**를 `Lockers_URP17`·`Lockers02_URP17`로 교체했습니다. 셰이더는 `Assets/2. Art/Shaders/NL_MaskedPBR_URP17.shader`.
> - 계단 구역에서 `ContributeGI`인데 `lightmapIndex = -1`인 렌더러 **49개**의 Contribute GI를 껐습니다(LOD·소품 포함). 이름을 박지 않고 「구역 안 · ContributeGI · 미베이크」로 찾으므로 오브젝트가 늘어도 따라옵니다.
>
> **⚠ 셰이더를 URP 17로 옮길 때 반드시 고쳐야 하는 것 — 처음에 이것 때문에 라커가 분홍이 됐습니다.**
> URP 7 Amplify 템플릿은 `InputData inputData;`를 선언만 하고 필요한 필드만 채웁니다. URP 17의 `InputData`는
> 필드가 더 많아서(`positionCS` · `normalizedScreenSpaceUV` · `shadowMask` · `tangentToWorld` …)
> `_CLUSTER_LIGHT_LOOP`가 켜진 변형에서 **「variable 'inputData' used without having been completely initialized」**로 깨집니다.
> 세 줄이면 됩니다 — `InputData inputData = (InputData)0;` 로 바꾸고,
> `inputData.positionCS = IN.clipPos;` 와 `inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.clipPos);` 를 채웁니다.
> 섀도마스크 키워드(`LIGHTMAP_SHADOW_MIXING`·`SHADOWS_SHADOWMASK`)는 **넣지 마십시오** — 이 프로젝트는 `mixedBakeMode = IndirectOnly`이고
> 템플릿이 `shadowMask`를 샘플하지 않아 틀린 변형만 늘어납니다.
>
> **㉓ 과학실은 프로브만 넣었습니다.** `Interior/science classroom/ScienceRoom_LightProbes` — **프로브 135개**(1.2m 격자,
> 지오메트리 안에 박히는 85개는 버림). 8개였던 것이 135개가 됐지만 **새 프로브에는 아직 구운 데이터가 없습니다.**
> 재베이크를 해야 살아납니다. 창 광원은 **만들지 않았습니다** — 과학실에는 창문 지오메트리가 없어(1층 내부 방) 지어내면 틀립니다.

---

#### 라이트맵 재베이크는 반드시 이 명령으로 (2026-09-22)

메뉴 **「NightDuty/아트/라이트맵 재베이크 (창 광원 켜고 굽고 되돌림)」** (`_Game/Flow/Editor/LightmapRebake.cs`).

**그냥 Generate Lighting을 누르면 씬이 통째로 어두워집니다.** 창문 채광을 맡는 `WindowLights_<방이름>` 그룹의
Baked 면광원 **35개가 평소 전부 꺼져 있습니다**(런타임 비용을 없애려는 벤더 관례). 그대로 구우면 모든 방이 창 빛을 잃습니다.
이 명령은 ⑴ 꺼진 것을 전부 켜고 ⑵ 굽고 ⑶ 끝나거나 취소되면 원래대로 되돌립니다.

**실측: 진행률 1.6%에 2분 30초 — 전체 약 2시간 30분입니다**(ContributeGI 렌더러 4,154개 · 2048 아틀라스 5장 · ProgressiveCPU).
2026-09-22에 한 번 돌려 보고 **취소했습니다** — 창 광원 35개는 자동으로 되돌아갔고 기존 라이트맵 5장도 그대로입니다.
**자리를 비울 때 돌리십시오.** 끝나면 Ctrl+S입니다.

재베이크가 고치는 것: 과학실 미베이크 56개 + 새 프로브 135개 + 씬 전체 미베이크 820개(서고 504 · 복도 131 · 외부 92 …).

18. **과학실이 반만 구워져 있고, Baked 광원이 0개이고, 프로브가 8개뿐입니다 (2026-09-22 진단, 확실).** 다른 다섯 방에는 전부 `WindowLights_<방이름>` Baked 면광원 그룹이 있는데 **과학실에만 없습니다.** 프로브도 교실(114개)의 14분의 1입니다. 과학실 볼륨 안 `ContributeGI` 렌더러 131개 중 56개가 미베이크입니다. **지금 재베이크를 돌려도 과학실은 까맣게 구워집니다 — 구울 빛이 없습니다.** → ① `WindowLights_ScienceRoom` 추가 ② 프로브 그룹 추가 ③ 그다음에 재베이크. `SpaceLights`·라이트 방향·컬링마스크·Forward+ 한계는 전부 정상임을 확인했고 배제했습니다.

### 8-8. 로딩 문구는 해금 전후를 가릅니다 (2026-09-22)

`LoadingTipTable`에 배열이 둘입니다.

- **`tips`(13개)** — 조작·규칙 안내. 언제 떠도 안전합니다.
- **`unlockedTips`(4개)** — 현장에서 알게 되는 내용(퇴실 기록 부재 · 03:58 응답 · 자산 목록에 없는 모형 · 두 번째 관측에서 멈춘 기록). **DAY 1에 뜨면 스포일러**입니다.

기획서 v6 검증항목 9: 「로딩 문구가 해금 전 내용을 누설하지 않는지 확인한다」. 파견 동의 전에는 **「무단 이탈 경비의 대체로 철거까지 5일 근무」만** 알 수 있어야 합니다.

**해금 기준은 `GameSession.CurrentDay >= 2`입니다.** DAY 1의 로딩은 계약 화면 → Play 씬이라 아직 태블릿을 줍기 전이고, DAY 2부터의 로딩은 결과창을 거친 뒤라 이미 주운 뒤입니다. **태블릿 습득 상태를 따로 들고 있게 되면 그 값으로 바꾸는 것이 더 정확합니다** — 지금 기준은 습득 게이트(§12.1)가 없어서 쓰는 대용입니다.

---

## 9. Git / LFS

자세한 내용은 `../Docs/형상관리_매뉴얼.md`를 따릅니다.

- **git은 사용자가 직접 합니다. Claude는 git 명령(status·log·커밋·병합·stash·lfs lock 등)을 실행하지 않습니다.** git 상태가 필요하면 사용자에게 묻고, 커밋 메시지는 요청받았을 때 작성만 합니다.
- **씬·프리팹 수정 전에 잠금 확인을 사용자에게 요청합니다.** `.unity`와 `.prefab`은 LFS 잠금 대상입니다(Force Text YAML로 저장되며 LFS 저장이 아니라 잠금만 추적).
  절차: `git lfs locks` → `git lfs lock "Assets/경로/파일.unity"` → 커밋·푸시 → `git lfs unlock "Assets/경로/파일.unity"`.
- **`Assets/NOT_Lonely/`는 gitignore입니다.** Git 클라이언트의 **"Discard All"이 이 미추적 폴더를 통째로 삭제할 수 있습니다.**
- 텍스처·모델·오디오·영상·폰트·압축 파일·네이티브 플러그인은 `.gitattributes`에 따라 LFS로 추적합니다. LFS를 우회해 대용량 바이너리를 커밋하지 마십시오. `.gitattributes`를 개인적으로 수정하지 마십시오.
- `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, `UserSettings/`는 커밋 대상이 아닙니다.
- 데스크톱 앱 세션에서 프로젝트 루트에 `Claude outputs/` 폴더가 생길 수 있습니다. 프로젝트의 일부가 아니므로 커밋에서 제외하도록 사용자에게 알립니다.
- 커밋 프리픽스: `feat:` · `fix:` · `art:` · `chore:` · `docs:`
- 커밋 메시지는 **Summary(50자 내외) + Description** 형식으로, 파일 목록보다 **왜 이렇게 했는지**와 **나중에 실수하기 쉬운 점**을 적습니다.

---

## 10. 사람과 브랜치

> **이 세션에서 말을 거는 사람이 「이성현(Lee)」이고, 「민」은 같은 사람의 호칭입니다.**
> 문서·보고에서 **「민이 ○○한다」처럼 3인칭으로 쓰지 마십시오.** 시스템 담당이자 씬 담당이 곧 대화 상대입니다.
> 씬 작업도 **본인이 직접** 합니다 — 2026-09-21 확인.


| 사람 | 역할 | 브랜치 | 개인 폴더 |
|---|---|---|---|
| 이성현 (Lee, 「민」) | **시스템** — 판정 · 수치 · 편성(덱·조우·문자) · 규칙 데이터 스키마 · **시스템 통합(게임 흐름 연결)** · **플레이어 판정 센서(문 3종 · 손전등 · 응시 · 근접 · 공간)**. **`PlayScene`의 판정 관련 씬 작업도 직접 합니다**(대상 자리잡기 · 센서 부착). | `Programmer_Lee` | `Assets/3.1. Programmer_lee/` |
| 김진선 (Kim) | **클라이언트** — **공동 작업용 통일 씬**, 태블릿 UI · HUD · 결과창 · 공간 연출 · 오디오, 그리고 태블릿과 한 몸인 **`Tab(isOpen)` 신호와 시계 정지**(Q6). 씬 잠금을 가장 자주 잡습니다. 아트팀과 잠금 시간을 조율하고 당일 해제합니다. | `Programmer_Jinsun` | `Assets/3.2 Programmer_Kim/` |

- 원격에는 `main`, `Programmer_Lee`, `Programmer_Jinsun`, `hyunuung`, `Art` 브랜치가 있습니다. 개인 브랜치에서 작업한 뒤 `main`에 병합합니다.
- **플레이어 센서 담당은 2026-09-20에 정해졌습니다.** 문 3종(`DoorCommand` · `DoorCloseCompleted` · `DoorAutoOpenObserved`) · 손전등 · 응시 · 근접 · 공간은 **시스템(이성현)이 직접 구현**합니다. 진선님 작업을 기다리지 않습니다. 태블릿과 분리할 수 없는 **`Tab(isOpen)`만 진선님 몫**입니다.
- 센서 **부착**은 카메라와 트랜스폼만 있으면 되고 UI도 아트도 필요 없습니다. 신호별 인자·함정 계약은 `다음작업_결정_2026-09-20.md` §8과 이 파일 §5.5를 따릅니다. **가장 싼 첫 걸음은 `Flashlight(isOn)` 하나** — H3·S6가 그날로 완전히 돕니다.
- 공간 레이아웃이나 대상 서수(「세 번째 칸」 등)를 바꾸면 수칙의 의미가 바뀝니다. 시스템 담당에게 알리십시오.

---

## 11. 작업 방식

### 11.1 세션 시작 절차

1. **기획서(2026-09-20 중간기획서)** 확인 → `../Docs/Claude outputs/HANDOFF_야간근무_인수인계.md` 통독 → `다음작업_결정_2026-09-20.md` → 이 파일 확인.
2. Unity 연결 확인: `editor_status`(ready) → `console_status`(에러 0) → EditMode 테스트(**362개** 통과 기준).
3. git 상태는 **사용자에게 묻습니다.**
4. 다음 작업(§12.1)은 사용자 확인 후 착수합니다.

### 11.2 원칙

- 작업을 맡기면 **끝까지 해 주기를 기대**합니다. 단, 파괴적이거나 되돌릴 수 없는 작업은 먼저 확인합니다.
- 설계 산출물은 `../Docs/Claude outputs/`에 markdown으로 둡니다. 다이어그램은 아티팩트로도 발행합니다(현행 아키텍처 페이지: https://claude.ai/artifact/5qgPXsVt7thj32VdMHFgPu, v3 — 패널 개편 미반영).
- **재지 않은 것을 적지 마십시오(2026-09-21 지시).** 이 파일과 설계 문서가 반복해서 틀린 원인은 하나였습니다 — **확인하지 않고 쓴 문장.** 규칙:
  1. 수치·상태·개수를 적기 전에 **에디터에서 읽어 확인**합니다(`eval`로 상수·에셋·씬을 직접 조회). 기억이나 이전 문서를 근거로 삼지 않습니다.
  2. **「있다/없다/끝났다/아직이다」는 특히 위험합니다.** 「센서가 아직 없다」를 확인 없이 적어 두면 다음 세션이 이미 있는 것을 또 만듭니다. 상태를 적을 때는 실측한 날짜를 함께 적습니다.
  3. **`enumValueIndex`는 열거자 값이 아니라 순번입니다.** `SignalKind`처럼 값이 띄엄띄엄한 열거형에서 이걸 값으로 쓰면 틀립니다. 값이 필요하면 `(int)card.TriggerKind`처럼 프로퍼티로 읽으십시오. 2026-09-21에 실제로 이 실수로 기준선이 오염됐습니다.
  4. 같은 사실이 **여러 절에 흩어져 있으면 한 곳만 고치고 끝내지 마십시오.** 문서 전체를 훑어 같은 숫자를 전부 맞춥니다.
  5. 고친 뒤 **`DesignDriftTests`를 돌립니다**(§3.3). 옛 경계가 본문에 남아 있으면 이 테스트가 잡습니다.
- **구현이 끝날 때마다 이 파일을 같이 갱신합니다.** 사용자가 따로 요청하지 않아도 합니다(2026-09-21 지시). 갱신 대상은 **확정값이 바뀐 것**입니다: 수치·경계·델타·카드 자격 / 새로 생기거나 뜻이 바뀐 타입 / 폐기되거나 되살리면 안 되는 것 / 다음 작업 목록. 머리말에 개정 블록을 하나 추가하고 **「되살리지 마십시오」에 옛 값을 명시**합니다 — 다음 세션이 이 파일을 정본으로 믿기 때문에, 여기가 낡으면 없어진 규칙을 구현합니다.
  중간 과정·시도했다 버린 것·작업 로그는 적지 않습니다. 그건 `../Docs/Claude outputs/`와 프로젝트 문서의 몫입니다.
- 작업이 끝나면 인수인계서의 진행 상황·다음 작업을 갱신하고, 이 파일과 어긋난 부분이 생기면 함께 고칩니다.
- 병렬 에이전트를 쓸 때는 **공유 타입 시그니처를 모든 프롬프트에 똑같이 넣고** 「자기 것만 정의하고 나머지는 이름으로만 참조하라」고 지시합니다. 컴퓨터 조작 도구와 에디터·디바이스 반영은 **메인 세션 한 곳에서만** 합니다.
- 경로는 세션마다 다를 수 있습니다. 절대경로를 하드코딩하지 말고 연결된 폴더를 먼저 확인하십시오.
- **기기 파일 전송(원격 세션):** 기기 셸이 없으면 스테이징/커밋으로 옮깁니다. 기기로 올릴 때는 매번 **새 `/mnt/user-data/outputs/<새 폴더>`** 에 준비합니다(같은 경로를 재사용하면 옛 내용이 올라감). `expectedMtimeMs`를 쓰고, 올린 뒤 크기를 확인합니다.
- **IMGUI(디버그 패널):** 버튼 동작은 `Later(...)` 큐에 넣어 Update에서 실행, `GUI.matrix`는 finally에서 복구, 지원 안 되는 기호(✔✖⚠■) 금지, F1/F2 입력은 `#if ENABLE_LEGACY_INPUT_MANAGER`.

---

## 11.5 대역 오브젝트 · 소리의 시각화 · 야간 베이크 (2026-09-23)

### ㉠ 바깥을 완전히 깜깜하게 + 간접광 재베이크 (완료)

`Sky_night`(Procedural)는 노출 0.03이어도 태양 고도 8°에서 **지평선이 빛났습니다**. 아예 검은 하늘을 만들었습니다.

| | 값 |
|---|---|
| 스카이박스 | **`Assets/2. Art/Materials/Sky_Black.mat`** — Procedural · 노출 **0** · 틴트/지면 검정 · 해 원반 없음 |
| 환경광 | **Flat** `(0.020, 0.022, 0.030)` |
| 안개 | ExponentialSquared `(0.020, 0.024, 0.032)` 밀도 **0.012** |
| `Sun` | 강도 **0.25** · `(0.62, 0.70, 1.00)` |

**베이크 품질을 시험용으로 낮췄습니다.** 원래 설정으로는 8분에 0.8%(몇 시간)였습니다.

| | 원래 (되돌릴 값) | 지금 |
|---|---|---|
| 해상도 | 28 texels/unit | **10** |
| 간접 샘플 | 512 | **128** |
| 직접 샘플 | 32 | **16** |
| 바운스 | 3 | **2** |

설정 파일 `Assets/2. Art/01 Scene/Art_DemoSceneSettings.lighting`. **룩을 확정할 때 왼쪽 값으로 되돌려 다시 구우십시오.**

실측 결과: **ContributeGI 미베이크 820개 → 8개**, **과학실 미베이크 56개 → 0개**, 라이트맵 5장 → 1장, 프로브 2,212개. 약 **30분**. 창 광원 35개는 자동으로 되돌아갔습니다.

### ㉡ 없는 것들의 대역 — `HarnessStandIns.cs` (2026-10-01 삭제)

파일째 지웠습니다(§3.4). 새 편성의 대역은 `StandInFactory`(19차 ⓕ)가 세웁니다. 태블릿 줍기 대역도 함께 사라졌습니다 — 태블릿 습득은 9단계 몫입니다.

### ㉢ `AnomalyCueDirector.CueFired` 신설 — 소리를 눈으로

밖에서 큐 전달을 구독할 수단이 **없었습니다**. 이벤트를 하나 냈습니다.

```csharp
public static event Action<SignalKind, string, CueBindingTableSO.Binding> CueFired;
```

`Send(...)`에서 `NightRun.Send` 직후 발행하고, 구독자가 터져도 판정은 그대로 갑니다. 정적 이벤트이므로 `SubsystemRegistration`에서 비웁니다. **진짜 음원이 와도 이 이벤트는 그대로 둡니다** — 자막·접근성·개발용 표시가 같은 자리에 붙습니다.

옛 `HarnessStandIns`가 이걸 듣고 파문·자막을 띄웠으나 삭제했습니다(§3.4). `AnomalyCueDirector`는 새 편성에서 쉽니다(19차 ⓙ).

### ㉣ 조사하면서 확인한 사실 (다음 작업에 필요)

- **`EventBus` 공개 이벤트는 여섯뿐**: `BandChanged` · `BandProgress` · `DayStarted` · `DayEnded` · `AxisCritical` · `MessageSent`. **`ClueDelivered`·`ClueIdentified`는 이벤트가 아니라 `SignalKind`**이고 방향이 반대입니다(연출기 → 코어).
- **`EventBus.DayStarted`를 발행하는 코드가 없습니다.** 구독해도 오지 않습니다.
- **축 델타를 밖에서 볼 수 없습니다.** `FearAxisSystem.ValueChanged`가 인스턴스 이벤트라 `NightRun` 밖에서 구독이 안 됩니다.
- **`ProximityProbe`에 `CurrentId` 같은 것이 없습니다.**
- **태블릿을 열면 센서 샘플이 통째로 멈춥니다**(`PlayerSensors.Update`가 `_acc = 0f; return;`). 대기 중이던 큐 전달도 버려집니다 — Q6이 여기 걸려 있습니다.
- 조우 대상 ID는 코드 상수가 아니라 **`Resources/EncounterTable.asset`의 데이터**입니다(`scene.xx.1~3` 24개 + 게이트 3개).

---

## 11.4 오클루전 컬링과 야간 조명 (2026-09-22 저녁)

### ㉠ 벽이 사라지고 관물대가 깜빡이던 것 — 오클루전 컬링이었습니다

**증상.** 복도 관물대가 카메라를 움직이면 사라졌다 나타나고, 벽이 지워진 자리로 바깥이 하얗게 비쳤습니다.

**측정 방법이 중요합니다.** 같은 자리·같은 각도에서 `useOcclusionCulling`만 켜고 끈 두 장을 렌더해 픽셀을 비교했습니다. 대조군(켜짐 vs 켜짐, 꺼짐 vs 꺼짐)은 **0.0%**였고 실험군(켜짐 vs 꺼짐)은 최대 **57%**였습니다 — 컬링이 화면의 절반을 지우고 있었습니다.

**원인.** 벤더의 모듈형 벽 키트는 코너·출입구·창문 조각이 **속이 빈 L자**인데 `Occluder Static`입니다. 그 조각의 AABB가 **사람이 설 수 있는 자리를 삼킵니다**(예: `WallInterior_CornerOutside (1)`의 4.3 × 4 × 4.3 상자 안에 복도 통행로가 들어갑니다). 복셀화가 그 칸을 「막힌 곳」으로 보면 거기 선 카메라는 거의 모든 것을 잃습니다.

**한 것.**
1. 공간 상자를 0.5m 격자 · 눈높이 세 단계(1.9 / 2.65 / 3.3m)로 훑어, **자기 AABB 안에 사람이 설 수 있는 점을 품은 Occluder 524개의 `Occluder Static`을 뗐습니다.** 남은 Occluder 3,497개가 실제 차폐를 맡습니다.
2. 다시 구웠습니다(`smallestOccluder 3` · `smallestHole 0.1` · `backfaceThreshold 100`). 9초쯤 걸립니다.
3. 360각도 재측정 — **차이 1% 넘는 곳이 82건 → 6건, 최대 57% → 11.3%**로 줄었습니다.
4. 그래도 교실 1-3에서 1~3m 앞 책상이 잘리는 각도가 남아, **`FPController/Camera`의 `Occlusion Culling`을 껐습니다.** 구운 데이터는 씬에 남아 있으니 **체크박스 하나로 되돌립니다.**

**성능 실측(에디터, 포스트 프로세싱 켠 채):** 컬링 켜짐 **64 fps** · 꺼짐 **53 fps**. 19% 비용이며, 시험 중 물체가 튀는 쪽이 더 나쁘다고 보고 껐습니다.

> **다시 켜려면** 남은 6각도를 마저 잡아야 합니다. 같은 방법(켜짐/꺼짐 픽셀 비교)으로 어느 렌더러가 잘리는지 찾을 수 있습니다.

### ㉡ 밤인데 바깥이 밝던 것 — 낮 조명 세팅이었습니다

씬이 통째로 **낮**이었습니다. 벤더의 `DemoScene_night`가 쓰는 값을 그대로 옮겼습니다.

| | 바꾸기 전 | 바꾼 뒤 |
|---|---|---|
| 스카이박스 | `Sky_day` (노출 1.6, 주황 틴트) | `Sky_night` (Procedural, 노출 0.03) |
| `Sun` 디렉셔널 | 강도 **10**, 색 (0.90, 1.00, 0.94) | 강도 **1.3**, 색 (0.71, 0.79, 1.00) 달빛 |
| 안개 | Exponential, 주황 (0.54, 0.42, 0.31), 밀도 0.0004 | ExponentialSquared, 청회색 (0.096, 0.108, 0.125), 밀도 0.01 |
| 환경광 | Skybox 1.4 | Skybox 1.5 (밤 스카이박스라 사실상 캄캄) |

**되돌리려면 위 표의 왼쪽 값을 그대로 넣으면 됩니다.**

> **라이트맵은 아직 낮에 구워진 것입니다.** 실시간 직접광만 밤이 됐고 간접광은 낮 바운스가 남아 있습니다. §8의 재베이크 명령을 돌리면 그것까지 맞습니다.

### ㉢ 태블릿에 업무 안내가 붙었습니다

`RuleSO`에 **`_howTo`(조작 안내) 필드를 신설**했습니다 — `RoomCardBuilder`의 S1 주석이 「태블릿 UI를 만들 때 필드를 더한다」고 요구하던 그것입니다. 기획서 §3-3이 **수칙 본문과 구분해** 표시하라고 했으므로 `PlayerText`에 붙이지 마십시오. 지금 채워진 카드는 **S1 한 장**뿐입니다(`DayBriefText.FirstCardHowTo`가 정본).

태블릿(F1)이 이제 `DayBriefText`를 읽습니다 — 1일차 인계 안내 · 2일차부터 그날의 관측 상태 한 줄 · 안전 안내 · 밤이 끝난 동안의 퇴실 안내. 판정도 델타도 없는 문구이므로 카드가 아니라 상수로 둡니다.

---

## 11.3 2026-09-22 씬에 직접 반영한 것 (민 승인)

씬 파일 `Assets/0. Main/01 Scene/PlayScene.unity`와 프리팹 `PlaySystems.prefab`을 고쳤습니다.

| 무엇 | 실측 |
|---|---|
| 라커 머티리얼을 URP 17용으로 교체 | 렌더러 **142개** |
| 계단 구역 미베이크 렌더러의 Contribute GI 해제 | **49개** |
| `SpaceZones.signalZones`에 **`cls11.door.outside`** 추가 | 4개 → **5개**. C1이 열립니다 |
| `HUD_Play`에 **`Txt_Prompt`** 생성 | 글꼴은 `Txt_Time`에서 빌린 `Pretendard-Medium SDF`. 기본 꺼짐 |
| `PlaySystems` 프리팹에 `PlayerInteractor`·`InteractionHud` 부착 | 런타임 자동 설치가 더는 뜨지 않습니다 |
| `Interior/science classroom/ScienceRoom_LightProbes` 신설 | 프로브 **135개**(8개였음). **재베이크 전에는 데이터가 없습니다** |

**`cls11.door.outside` 상자**는 `Center (48.0, 3.4, 46.8) · Extents (1.5, 2.0, 1.1)`입니다 —
교실 1-1 문(48.0, 47.9) 바로 앞 **복도 쪽**입니다. C1의 위반 조건이 `SpaceEntered@1-1`이라
**1-1 존(z ≥ 48)을 절대 물면 안 됩니다.** z 최대를 47.9로 잡은 이유가 그것입니다.

하지 않은 것: **라이트맵 재베이크**(2시간 30분 · §8 참조) · 판정 대상 15종 배치 · 조우 대상 27종 배치 ·
과학실 창 광원(창문 지오메트리가 없어 지어낼 수 없습니다).

---

## 12. 다음 작업과 미해결

### 12.1 다음 작업 (우선순위)

**최종 기획서 「제작 계획」 12단계가 이 목록보다 앞섭니다.** 15차까지 1~3단계, 16차 4·5단계, 17·18차 6단계(카탈로그·편성기·새 수칙 판정), **19차 7단계 핵심(긴장 디렉터·놀람 예산·대본·연출 실행기·대역·소등·디버그 콘솔 F3)**, **20차 1일차 무조우·조우 묶인 수칙·H2 문 자동 개방·하네스 삭제**, **21차 몹 고정 자리·모델 연결·소리 표**, **22차 김진선님 연출 에셋(사람 나무 프리팹·화면 톤·가짜 놀람 캐비닛·벌레 떼)** 완료. **23차 경비실 전화로 근무 일찍 끝내기·상호작용 외곽선.** **24차 조도축 맵 변화(공간별 톤 Volume·기획서 표 소등·옛 SpaceLights 은퇴)로 7단계 마무리** — 7단계에 남은 것은 음원이 필요한 빠진 소리뿐. **25차 옛 24장 카드 폐기로 6단계 완료.** **26차 새 편성에서 안 쓰는 옛 시스템 폐기(옛 판정 책·조건·옛 조우/역설 연출기·옛 단서 큐·옛 조도 리그·시험 리그·근접 발신기) + 결과창 근무일지·위반 시각·점검 칸을 새 수칙·점검표로.** **27차 8단계 붙잡힘 공용 틀·얼굴 컷·재시작 카드(`CaptureDirector`).** 2026-10-03 진행 점검(기획서 「구현 단계」 기준): 1·3·4·5·6 완료(6의 옛 24장 삭제는 25차) · 2 코어 완료 · 2′ 몸 연출(심박·호흡·에코) 미착수 · 7 완료(24차, 빠진 소리만 음원 대기) · 8 거의 완료(K1·K2 신호 연결·CCTV 화면 셰이더·붙잡힘 공용 틀·얼굴 컷·재시작 카드(27차) — 축별 소리·씬 재로딩 남음) · 9 일부(02:16 중간 서명 코어·근무일지 결과창, 태블릿 5탭·포커스 보고 UI·위반 진동 남음) · 10 일부(역설 편성 코어, 안전한 읽기·변조본·검은 줄 남음) · 11 일부(피날레 몹 배역 슬롯·비트·검사(28차) — K4 흐름·G3·창 두드림 순서·결말 2종·CRT 반사 남음) · 12 밝기만. 7단계 남은 것: 공간별 로컬 Volume(색온도·채도), 소녀 모델·빠진 소리(종·분필·물 내림 등). 그다음 8단계(붙잡힘 3종·재시작 카드·CCTV 판정 셰이더). 옛 조우 8장면·옛 단서 큐는 26차에 폐기. 재시작의 씬 재로딩·시계 되돌리기는 김진선님과 맞춘 뒤 붙입니다.

(옛 7단계 목록 — 참고용) ① 코어 수치 **완료**(14차) → ② 붙잡힘 뒤 밤 재시작(감각 −10)·경고 3회 처벌 **완료**(15차) → ③ 점검·보고(16항목, 이상 4틀, 정확 보고 −5/놓침 +8) → ④ 공간·수칙 교체(`SpaceId`에 도서관·경비실 추가, 새 수칙 에셋, 옛 24장·테스트 삭제, 덱 = 공간 5 + 경비실 1(2일차~) + 공통 3, 가장 높은 감각 축 ×2·가장 낮은 축 최소 1) → ⑤ 역설·변조(사용자가 아직 손볼 예정) → ⑥ 연출 카탈로그·긴장 디렉터 → ⑦ 붙잡힘 3종·CCTV 판정(K1/K2/K-1). 교실 사다리 `LibraryLadder (1)`을 Classroom02에 놓는 씬 작업은 LFS 잠금 확인 뒤.

> **수치는 다 맞췄습니다. 이제 남은 것은 그 수치를 화면에 내보내는 일입니다.**
> 2026-09-21 재설계로 데드락과 「잘할수록 아무 일도 안 일어남」이 해소됐습니다(실측: 이상현상 12칸 → 48칸, 전부 위반 4일차 사망, 역설 누적 10쌍).
> **걸어서 시험할 때는 F3 디버그 콘솔을 씁니다**(19차 ⓖ). 옛 런타임 하네스는 20차에 삭제했습니다(§3.4).
> 씬에 진짜 대상을 놓는 일(아래 4번)은 그대로 남아 있습니다.

**(옛 기록 — 2026-09-21 기준 순서.** 옛 24장·옛 단서 큐·옛 조우를 전제로 한 목록이라 2026-10-03 폐기 뒤에는 그대로 따르지 마십시오.)

| | 무엇 | 담당 | 막고 있는 것 |
|---|---|---|---|
| A | **씬 대상 자리잡기 + 센서 부착** (아래 3·4번) | 민 | 판정의 **최종** 배선. 배선이 도는지 여부는 F3 디버그 콘솔로 확인합니다 — 남은 것은 표식을 **있어야 할 자리**에 놓는 일입니다 |
| ~~B~~ | ~~**`EncounterDirector`**~~ | 시스템 | **2026-09-22 완료**(§4.3″) |
| C | **결산 UI** | 미정 | `DutyMark` 3구분을 그릴 화면. 데이터는 이미 있습니다 |
| D | **태블릿 UI** | 미정 | `EventBus.MessageSent` 구독자 0개 — 역설 10쌍이 화면에 안 뜹니다 |
| E | 배속 ×20 → **×30~34** (아래 5번) | 미정 | 하루 12분 중 9~10분이 빈 복도입니다 |

A와 B는 **병렬 가능**합니다 — 씬 파일과 코드가 겹치지 않습니다.

1. **C1 한 장을 실제 플레이로 끝까지 판정시킵니다.** 성공 기준은 테스트 통과가 아니라 **사람이 걸어 다니다 결과창에서 빨간 줄을 받는 것**입니다. 경로: 분필 3획 단서 발신 → `ClueDelivered("cls11.chalk3")` → 1-3을 1-1보다 먼저 점검 → 청각 +12 또는 신뢰 +4 → 이상현상·조명 재방송 → 근무 일지 빨간 줄 → Result 씬. 대상 `cls11.chalk3`는 이미 씬(`Blackboard`)에 있고, 음원은 나중에 꽂아도 됩니다(단, **단발 클립 3연타**로 발주 — §5.4-15).
2. **단서 발신 개통**(뼈대는 2026-09-20에 만들었습니다 — `CueBindingTableSO`·`AnomalyCueDirector` 둘 다 존재. 남은 것은 **바인딩 표 채우기와 씬 부착**입니다). `SpaceAnomalyTable`의 **큐 ID와 카드가 기다리는 판정 ID는 이름 체계가 다릅니다.** 개명해 합치지 마십시오 — 합치는 순간 「분위기·모형 효과음은 카드 단서 ID를 보내지 않는다」(§2.7)가 **구조적으로 불가능**해집니다. 큐(연출 재생 단위)와 판정 ID를 분리한 채 표로 잇습니다. 이걸 넣으면 **16장의 트리거가 개통**됩니다(실제로 열리려면 축 게이트가 따로 필요). 바인딩 15줄과 조도 칸의 큐 부재는 기획 확인 대기(Q13).
3. **플레이어 센서 씬 부착**(코드는 2026-09-20에 만들었습니다 — `PlayerSensors`·`GazeProbe`·`ProximityProbe`·`FlashlightRelay`·`DoorRelay`가 `Flow/Sensors/`에 있고 씬 인스턴스는 0개입니다). 가장 싼 첫 걸음은 `Flashlight(isOn)` 하나 — **H3·S6가 그날로 완전히 돕니다.** `GazeRay`(카메라 중앙 첫 가시 충돌체 ID를 돌려주는 읽기 전용 서비스)는 `ClueIdentified` 0.2초 식별과 `ModelObserved`를 만들려면 **어차피 필요**합니다. 함정은 §5.5.
4. **씬 대상 7종 자리잡기**(민, 2026-09-21 실측). 씬에 `JudgeTarget`이 이미 **17개** 있고 카드가 요구하는 24종 중 **7종이 빕니다**: `cls11.desk.turned`(C2) · `corridor.debris`(H5) · `corridor.tree`(H6) · `science.bench.glass`(S3) · `toilet.stall.light`(T5) · `science.model.sa.face`(S1) · `science.model.sb`(S5). 빈 GameObject + `JudgeTarget`으로 자리만 잡으면 **앞의 5종이 풀리고**, 남는 것은 모형 2종(S1·S5)뿐입니다. 식별·응시 대상에는 **작은 `BoxCollider` 필수**(§5.5-21). 끝나면 `save_scene` → 메뉴의 「씬 대상 검사」로 개수를 확인합니다.
5. **게임 시계 04:00 반영 + 동선 스톱워치.** `PlayScene`·`PlaySystems.prefab`이 0:00~6:00 ×20으로 들어가 있습니다(§8-7). 바꾼 뒤 **한 번 걸어서 실제 소요 초를 재어 기획에 제출**합니다 — 추측으로 회의를 여는 것보다 낫습니다.
6. ~~**`DayDirector`**~~ — **2026-09-21 완료**(§4.3′). 하루 6장, 축 쿼터 2·2·2, 하드 제약 3종. 남은 2건(그날 조우 공간 카드 강제 · S2 활성 중 S-B 금지)은 `EncounterDirector` 대기. **→ 2026-10-03(25차) 옛 24장과 함께 폐기.**
7. ~~**`EncounterDirector`**~~ — **2026-09-22 완료**(§4.3″). 코드·표·테스트 19개. 남은 것은 **씬 대상 27종**과 **G/N1 문구**(기획서 10-3절), 그리고 아트의 선/앉은 모형 프리팹입니다. 이전 서술: G의 주인은 카드가 아니라 **조우 장면**이라(§2.7) `EncounterDirector`와 태블릿 UI가 둘 다 있어야 합니다. 문자 작업 중 의존이 가장 많으므로 **앞으로 당기지 마십시오.**

- **엠비언트 후속(2026-09-30).** ① ~~소리 파일 넣기~~ (완료) ② 걸어 다니며 볼륨 튜닝(`AmbienceConfig`) ③ CCTV 수칙 교체안(K1′·K2′·K5, 별도 문서) 채택 여부 결정.
- **CCTV 후속(2026-09-30).** ① K1·K2 판정을 `CctvSystem.ChannelChanged`/`ViewEntered`에 연결 ② 채널 자리·적외선 세기 튜닝(민, `CctvConfig` 인스펙터) ③ 경비실 조우 등에서 `CctvOnlyVisible` 활용 ④ 성능 계측(렌더는 한 번에 카메라 1대, 모니터 5m 밖이면 0대).
- **다음 주로 미루는 청소:** 결과창 「충돌 처리」 숨김·`ImprintAxis` 제거, 로딩 팁 §0 문구 정리, `Conflicts*` 정리, `Bands.RedThreshold`(값이 옛 Band3 하한 75, 참조 0건 — 지울지 민 결정), 통일 씬이 들어오면 구동기 동작 재확인.
  **끝난 것:** H6 +25 → +15 · 과학실 배치 이상현상 4칸 · `ParadoxDirector` 상한 스냅숏 (전부 2026-09-20).
- **아직 하지 않는 것(옛 기록):** 옛 이상현상 표·면제 API·옛 역설 정책은 2026-10-03(26차) 폐기로 대상이 사라졌습니다.
- (선택) 아키텍처 페이지 갱신.

**이번 주 검증 기준:** 디버그 신호 없이 **실제로 걸어서** ① C1이 준수/위반으로 갈리고 ② 결과창 근무 일지에 빨간 줄이 그어지고 ③ EditMode 테스트가 **전부 통과**(25차 기준 353개)해야 합니다. 그 전까지 코어에 추가되는 모든 줄은 검증되지 않은 가정 위에 쌓입니다.

### 12.2 결정 대기 — 사용자 확인 필요

**김진선님과 맞출 것(15차, 판정 코어는 준비됨):**

- **GameTime 범위·표시**: 프리팹이 02:00~05:00 ×30입니다. 기획서는 00:00~04:00, 실시간 15분. 구동기가 비례 환산·배속 조정을 하므로 판정은 맞지만, 태블릿에 보이는 시각은 GameTime 표시 그대로입니다.
- **재시작 연결**: 27차에 `CaptureDirector`가 맡았습니다(플레이어를 출근 자리로, 시계 `JumpToHour`). 남은 것: 문·소품까지 되돌리는 씬 재로딩 여부, 게임 시계 **분 단위** 이동 API(02:16 체크포인트가 표시상 02시로 감). 근무 씬에서는 `AxisCritical`이 오지 않아 사망 화면(HUD_Death)이 뜨지 않습니다 — 김진선님께 공유.
- **근무일지 UI**: 중간 서명(`SignCheckpoint`)·조퇴(`LeaveVoluntarily`) 버튼, 경고 도장 표시(`EventBus.WarningsChanged`).

**2026-09-20 기획서로 종결 — 다시 열지 마십시오:**

| 종결 항목 | 답 |
|---|---|
| **Q1** 근무 일수 | **5일** (기획서 A절. 코드 `GameSession.FinalDay = 5`와 일치) |
| **Q2** 근무 종료 방식 | **04:00 시계 자동 종료.** 종료 요청·경비실 복귀 없음, 점검·조우 완료와 무관 |
| **Q10** 신뢰 구간별 역설 상한 | **구간 번호가 곧 상한**(0 / 1 / 2 / 3 / 4쌍). 신뢰 전용 경계 **0–14 / 15–29 / 30–44 / 45–64 / 65~**(15차), `DailyQuota`가 `Bands.OfTrust`를 거칩니다. 최종 기획서의 일차별 편성은 역설·변조 단계에서 바뀝니다 |
| 역설 미도달·거절 벌점 | **폐기 확정.** 전 유형 「별도 벌점 없음」 |
| **Q3** 결과창 역설 표시 | **무표시.** 따름일 때 **짝 수칙에 빨간 줄**만 — `DutyLogEntry`가 이미 하는 일 |

**남은 대기:**

| # | 항목 | 누구 |
|---|---|---|
| Q4 | 포획 엔딩 연출(청각·조도·배치). 종료 신호까지만 계약됨 | 기획 |
| Q5 | **응시 원뿔 각도.** 정본은 「중앙 레이 첫 가시 충돌체, 끊기면 0」, 인수인계서는 H2·C3을 위해 약 12° 권고. **센서를 우리가 만들기로 해 더 급해졌습니다** | 기획·진선 |
| Q6 | Tab이 `timeScale = 0`인가 (§5.5-20의 공간 신호 유실이 여기 매달려 있습니다) | 진선 |
| Q8 | GameFlow 코드를 `_Game`으로 옮길지 | 진선 |
| Q9 | H3가 통행 구역 진입 시 방문 몫을 차지하는 것이 의도인가 | 기획 |
| ~~Q11~~ | **2026-09-21 종결(민 승인).** 하루 **6장**을 축 쿼터 2·2·2로 섞어 배정하며 최소 3공간을 포함합니다(§4.3′). 「시간 내에 못 돌았을 때의 대가」도 종결 — **공간 미방문에만 감각축 +9**(§2.5-2′). **2026-10-03(25차) 옛 24장과 함께 배정기(`DayDirector`)를 폐기** — 새 편성은 `ProgramDirector`가 정한다 |
| ~~Q12~~ | **2026-09-22 종결.** 시나리오 기획서 v6 §5-1이 「신뢰는 100이어도 완주할 수 있다. 신뢰 수치에 따른 생존·사망·진엔딩 분기는 만들지 않는다」로, §5-3이 「'신뢰=준수율'로 오해하게 만드는 이전 추가 문구는 삭제한다」로 확정했습니다. 코드는 이미 맞습니다(`FearAxisSystem.IsTerminal`). **남은 것은 태블릿 글자 흔들림 한 문장**뿐이고, §2.8의 금지가 그대로 우선합니다 |
| **Q13** | ~~바인딩 표 채우기~~ — **2026-09-22 해소.** 빌더에 표 본문이 이미 있었고 에셋만 없었습니다(38줄 생성). 남은 기획 확인은 **잠정값 셋**뿐입니다: C4 `SequenceSeconds`(음원 발주 대기) · S3 `glass.touch`의 발동 시점 · 「별도 방문」(`CueVisit.SeparateVisit`)의 정확한 뜻 | 기획 |
| **Q14** | **역설의 회차 한도와 동시 후보 우선순위.** 기획서에 없습니다. 현행 코드는 **덱 순서**, 옛 문서는 P3→P2→P1→P4였습니다(§2.7) | 기획 |
| **Q15** | **역설 발송 시점 3건.** P12(C6 「교실 문을 직접 연 직후」 — **어느 문인가.** C6가 `NightBegan` 트리거라 현 구조로는 밤 시작 즉시 나갑니다) · P13(「그날 일일 조우 종료 후」 — **관찰까지인가, 그 방문의 퇴실까지인가**) · P23(「그날 T-A가 선택됐으면 미발송」 — **선택의 기준 시점**이 언제인가) | 기획 |
| 카드 | C1 장기 유지 / ~~C2·T6 「또는」 자격~~(**2026-09-21 종결** — 기획서 J절 게이트를 되살렸습니다. C2 배치 Band1~, T6 배치 Band3~. 단서 신호는 여전히 필요합니다) / C6 1-1 문 ID(H1 자동 문과 같은가)·봉쇄 면제 / T6 준수는 시작 뒤 점검만 인정 / S3·S4·T5 「점검 없이 퇴실 = 대기」 해석 / C5 보류가 1-1에 걸림 | 기획 |
| 팀 | `com.unity.pipeline` 커밋 여부(현재 stash 복구본, 미커밋) | Lee·진선 |
| 기타 | 진선님 폴더명 ↔ 브랜치명 불일치 정리 / 일정 재작성(4공간·24수칙 기준) | 팀 |
| 수치 | 0.2초 식별, 1.5m 반경, 중립 구역 폭, **일차 연출 하한(Band0/Band1/Band1/Band2/Band3)**, **배속 ×30~34**는 **잠정값** — 플레이테스트로 확정. 하루 배정 장수는 **6장으로 확정**(Q11) | 팀 |
| 자산 | 선/앉은 전신 모형 프리팹, 문 E 스크립트, 등 그룹 수, 닫힌 칸 아래 빛 자산, Tab 일시정지 기능 | 진선·아트 |

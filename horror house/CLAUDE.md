# CLAUDE.md

이 파일은 이 저장소에서 코드를 다루는 Claude 세션을 위한 안내서입니다.
**답변·문서·코드 주석은 모두 한국어로 작성합니다.**

> 2026-11-19 정리: 3차~2026-11-13의 개정 기록(약 520KB)은 `../Docs/Claude outputs/CHANGELOG_야간근무.md`로 옮겼습니다.
> 옛 24장 카드·옛 조우 8장면·옛 역설 연출기 등 2026-10-03에 폐기한 체계의 설명도 이 파일에서 뺐습니다(필요하면 CHANGELOG와 git 이력).
> **이 파일에는 현행 규약만 둡니다. 변경 경위·시뮬 결과·QA 내역은 CHANGELOG에 적습니다(§11.2).**

---

## 1. 프로젝트

**야간근무 (Night Duty).** 폐교를 배경으로 한 1인칭 **규칙 준수 공포게임**입니다. 나폴리탄 괴담을 참조합니다.
플레이어는 야간 경비로서 태블릿에 실리는 근무수칙과 점검·근무 지시를 따르며 학교를 순찰합니다.
**1회차는 근무 5일, 하루는 인게임 00:00~04:00입니다. 04:00이 되면 점검·조우 완료와 무관하게 그 자리에서 근무가 끝납니다.**
**수칙을 어기면 감각축(청각·조도·배치)이 오르고, 그중 하나라도 100에 도달하면 붙잡힘입니다.**
**수칙을 지키면 신뢰가 오르고, 신뢰가 높을수록 역설·변조가 늘어납니다.** 신뢰는 게임오버를 일으키지 않습니다.

- 엔진: Unity **6000.3.17f1** · URP **17.3** (Forward+) · New Input System 1.19
- 인원: 2인 개발 (시스템 / 클라이언트, §10)
- 작업 폴더 `G:\NCAI_BrokenCompass\horror house`가 **Unity 프로젝트 루트**이고, git 저장소 루트는 한 단계 위(`NCAI_BrokenCompass/`)입니다.
  저장소 루트에도 `Assets/` 폴더가 있지만 Unity 프로젝트가 아닙니다. 루트를 Unity로 열지 마십시오.
- 빌드·린트 도구는 없습니다. 검증은 Unity 에디터에서 합니다(§3).

### 1.1 문서 우선순위 — 충돌하면 위가 이깁니다

| 순위 | 문서 | 위치 | 역할 |
|---|---|---|---|
| **0** | **야간근무 최종 기획서 (2026-09-30 확정본)** | Claude Docs `ebc9e369-d2e8-491c-ba8a-7b071e56a19e` (예전 초안 `1c5c811a-…`는 쓰지 않음). 팀 공유용 변경 요약은 「야간근무 기획 변경 공유」 `594ee326-e338-422c-a381-74a9492e3f3c` | **최상위 정본.** 수칙·태블릿 문구, 점검, 구간·델타·경고·재시작, 역설·변조. 이후 사용자(민·김진선님) 결정은 CHANGELOG에 있고 기획서보다 나중 것이 이깁니다 |
| 1 | `HANDOFF_야간근무_인수인계.md` | `../Docs/Claude outputs/` | 결정·함정·진행 상황 인수인계서 |
| 2 | `CHANGELOG_야간근무.md` | `../Docs/Claude outputs/` | 개정 기록(차수별 결정·사유·시뮬·QA). 위가 최신 |
| 3 | 이 파일 | `horror house/CLAUDE.md` | 작업 규약 요약 |
| 4 | `형상관리_매뉴얼.md` | `../Docs/` | git/LFS 운영 규칙 |
| — | `게임플로우_작업내역_설정가이드.md` | `../Docs/Claude outputs/` | 진선님 게임 흐름(Main→Loading→Play→Result) 설정법 |

- 중간기획서(9/20)·`지침록 9.20V`·`지침록_개발명세반영본(9/12)`은 **옛 24장 카드용 정본**이었습니다. 새 체계에서는 참고하지 않습니다.
- **수칙 전문·델타·판정 상세·경계 숫자를 이 파일에 옮겨 적지 마십시오.** 사본이 어긋나면 어느 쪽이 맞는지 알 수 없습니다. 이 파일에는 「정본은 ○○」(기획서 절 또는 코드 위치)만 적습니다.
- 이미 내려진 결정은 다시 논의하지 않습니다. 바꿔야 한다면 **사용자에게 먼저 확인**받으십시오.

---

## 2. 확정 게임 규칙 요약

### 2.1 스코프

| 항목 | 확정값 |
|---|---|
| 회차 | **근무 5일** (`GameSession.FinalDay = 5`) |
| 근무 시간 | 인게임 **00:00~04:00**. 실시간 **1일차 15분 · 2일차 12분 30초 · 3일차부터 10분**(`NightClock.RealSecondsPerNightFor(day)`). 시작 지점은 **경비실**. 04:00 자동 종료, 종료 요청·경비실 복귀 없음. 모든 지시를 마치면 경비실 전화로 조기 퇴근(`CanEndShiftEarly`) |
| 공간 | **6곳**: 복도 · 교실(1-1/1-3) · 과학실 · 화장실 · 도서관 · 경비실 (`SpaceId`) |
| 수칙 | 기획서의 새 수칙(H·C·S·T·L·K·G 계열). 편성은 `ProgramDirector` → `FinalRuleBook`. 1일차에는 가짜 수칙 2줄이 섞임(`FakeRuleCatalog`) |
| 지시 | 점검 지시(`InspectionDispatcher`) + 근무 지시 W계열(`DutyDispatcher`), 태블릿 메시지로 한 통에 한 항목 |
| 조우 | **고정 날짜 편성**(`ProgramDirector.FixedSchedule` → `FixedEncounters`, 축 구간으로 뽑지 않음 · 의도한 날 반드시 목격). 시점 `TensionDirector`(순서대로, 클라이맥스는 창 C) · 실행 `DirectionStage`. 날짜표는 인수인계서 §3 |
| 역설·변조 | `ParadoxPlanner`·`ParadoxRun`·`UnavoidableRun` (10단계) |

### 2.2 네 개의 축

- **생존 수치**(`FearAxisSystem`): 0~100. **붙잡힘에만** 씁니다. 오르는 것은 위반·실패, **내려가는 것은 정확한 보고·지시 완료·밤 재시작**뿐입니다. 감쇠·상시 증가는 없습니다.
- **연출 구간**(`BandResolver`): max(도달 구간, 일차 하한). **절대 내려가지 않습니다**(래칫). 월드 연출·편성·역설 상한은 모두 `BandResolver.Shown`을 읽습니다. 연출 도달값을 내리는 유일한 길은 재시작·결근의 `RestoreReached`입니다.
- 수치는 **`Deltas`(`Scripts/Core/Deltas.cs`) 한 곳**에 있습니다. 정본 표는 기획서.
- **처벌·04:00 정산·판정 정지 중 델타는 95에서 멈춥니다**(`Deltas.SoftCap`).
- **청각·조도·배치 중 하나가 100이면 종료 잠금**입니다. 원인 축·소스·공간을 기록하고 `EventBus.Captured`를 **한 번만** 보냅니다. 그 뒤의 델타·판정·문자·조우·정산은 모두 중단합니다. **신뢰 100은 종료 잠금이 아닙니다**(`FearAxisSystem.IsTerminal`).
- 벌칙 축 = 원인 축: 경고는 축별로 세고(`NightRun._warnByAxis`), 처벌은 가장 많이 쌓인 축(`PunishmentAxis()`)에 붙습니다.
- **신뢰는 월드에 그리지 않습니다.** 역설·변조 편성의 입력으로만 씁니다.

### 2.3 구간(Band)

- 감각 축 경계는 **`Bands.cs`의 `LowerBounds`**, 신뢰 전용 경계는 **`TrustLowerBounds`** 한 곳에만 있습니다. if 사슬로 흩뿌리지 말고 이 파일에도 숫자를 옮겨 적지 마십시오.
- **`value / 20` 같은 균등 분할 계산을 하지 마십시오.** 반드시 `Bands.Of`·`Bands.OfTrust`·`Bands.OfAxis`를 씁니다.
- **Band3·Band4가 좁은 것은 의도입니다.** 붙잡히기 직전 구간이 길면 긴장이 풀립니다. 90~99 표현이 엔딩 직전 경고를 맡고, 별도 경고 시스템은 추가하지 않습니다.
- **연출은 `Bands.Of`가 아니라 `BandResolver`의 연출 구간을 봅니다.** `Bands.Of`는 원시 표일 뿐입니다.

### 2.4 일차 하한 — 「잘할수록 아무 일도 안 일어남」을 막는 장치

- `DayFloor`가 근무일마다 **연출 구간의 바닥**을 정합니다. 곡선은 **`DayFloor.cs`의 `Floors` 배열 한 곳**입니다. **생존 수치는 올리지 않고, 신뢰는 대상이 아닙니다.**
- **반드시 덱을 짜기 전에 겁니다** — `NightRun.BeginNight`이 `SetDayFloor`를 편성보다 먼저 부릅니다.
- **Band4는 하한으로 주지 않습니다.** 그 구간은 「당신이 어겨서 여기까지 왔다」는 뜻으로 남깁니다. 곡선이 줄지 않고 Band4에 닿지 않는지는 `DesignInvariantTests`가 지킵니다.

### 2.5 조도

- 조도 톤은 `IlluminanceToneSO`(`Resources/IlluminanceTone.asset`)가 한 곳에서 관리합니다. 공간별로 복사하지 마십시오.
- 구간 3부터 공간의 켜진 천장 등 일부가 붉어지거나 깜빡입니다(`IlluminanceTint.AccentFrom` · `IlluminanceMap.PlanAccents`). 꺼진 등·조우가 물들인 등·C4 초록 등은 건드리지 않습니다.
- 판정은 축 값과 구간으로만 합니다. 화면 픽셀 색을 재지 않습니다.

### 2.6 판정 입력의 공통 원칙

- **응시**: 카메라 중앙의 **첫 가시 충돌체**. 벽·닫힌 문을 관통해 세지 않습니다. 대상이 바뀌거나 가려지면 연속 시간은 0. 프레임레이트와 무관한 고정 간격 누적(§5.5).
- **근접**: 발밑 기준점과 바닥 기준점의 **수평 거리**.
- **공간 경계**: 문 상태가 아니라 발밑 기준점의 진입·이탈. 경계 위에서는 직전 공간 유지.
- **문**: 명령 수락과 닫힘 완료를 별도 신호로, 조작 출처(`ActionSource.Player`/`Direction`)를 구분합니다(§4.5).
- 수치(시간·거리)는 코드에 박지 말고 `SensingRules`·데이터(SO)에 둡니다.
- **새 조작을 요구하는 콘텐츠는 구현하지 말고 기획에 되돌리십시오.** 조작: 마우스 룩 + WASD, E 상호작용, 좌클릭 손전등, 우클릭 태블릿, Tab 메시지/수칙 전환, R 배터리, Esc 일시정지.

### 2.7 설계 금기 — 선의로 어기기 쉽습니다

- **플레이 중 축·델타·위반 팝업이나 게이지를 표시하지 마십시오.** 플레이어는 자기가 어겼다는 사실을 모른 채 환경이 변하는 것만 봐야 합니다. 결과창에도 수치는 없고 「어김 / 지시를 따름」 같은 **말**만 드러납니다.
- **태블릿에는 수칙 본문만 노출합니다.** 판정 상세는 제작자용입니다.
- **수칙 본문이 요구한 행동만 채점합니다.** 괴담의 이유나 설명은 판정 조건이 아닙니다.
- 보지 않으려는 플레이에 **강제 시선이나 체류 즉사를 추가하지 마십시오.**
- **화장실에 거울·플레이어 반사를 쓰지 마십시오.**
- **수칙을 성립시키려고 연출을 강제하지 마십시오.** 단서가 실제로 제시되지 않았으면 판정을 시작하지 않습니다(미판정, 델타 0).
- 수칙·지시·문자의 문구·목적지를 Claude가 지어내거나 바꾸지 마십시오. 문구는 사용자가 정합니다.
- 태블릿 폰트·디자인을 신뢰에 따라 왜곡하지 마십시오(폐기된 안).
- **새 범용 규칙 엔진이나 대규모 구조 개편을 하지 마십시오.** 팀의 현재 데이터 구조에 연결합니다.

---

## 3. Unity 에디터와 작업하기

### 3.1 연결

이 프로젝트에는 `com.unity.pipeline`이 설치되어 있어, 실행 중인 에디터를 **Unity CLI** 또는 **Unity MCP**로 직접 조작할 수 있습니다.

```powershell
unity status        # 에디터 인스턴스가 "ready"인지 확인
unity command console_status   # 컴파일 실패 여부와 콘솔 카운트
```

- MCP 설정은 `claude_desktop_config.json`의 `--project-path`(G: 경로)입니다.
- **작업 전에 에디터가 `ready`인지 먼저 확인하십시오.** 에디터가 닫혀 있으면 사용자에게 Unity Hub로 열어 달라고 요청합니다.
- **`com.unity.pipeline`은 `Packages/manifest.json`에 있어야 합니다.** 병합 때 빠질 수 있습니다. MCP 서버가 에디터보다 먼저 켜지면 도구가 0개로 보입니다 → 에디터를 띄운 뒤 앱을 재시작합니다.
- 에디터 창이 뒤에 있으면 컴파일·패키지 해석·플레이 틱이 멈춥니다 → `editor_focus`(사용자에게 알리고) 또는 사용자에게 창을 앞으로 가져와 달라고 요청.
- **모달 대화상자가 뜨면 명령이 멈춥니다.** 확인 대화상자를 띄우는 메뉴를 MCP로 실행하지 마십시오.
- **연결이 안 되는데 에디터는 켜져 있다면 Safe Mode(컴파일 에러)를 먼저 의심하십시오.**
- `Library/Pipeline/.unity-pipeline-port`에는 인증 토큰이 들어 있습니다. **내용을 출력하거나 공유하지 마십시오.**
- `eval`, `run_script`는 임의 코드 실행입니다. 읽기 목적 외에는 사용자 확인 후 쓰십시오.

### 3.2 씬·에셋 수정 규칙

- **에디터가 연결되어 있으면 `.unity` / `.prefab` / `.asset` YAML을 직접 편집하지 마십시오.** 에디터 명령(`set_component_properties`, `save_scene` 등)을 씁니다. 에디터가 없을 때만 파일을 직접 고치고, 그 사실을 명시합니다.
- 씬·프리팹 수정 전에는 **LFS 잠금** 확인을 사용자에게 요청합니다(§9).
- `switch_build_target`, `clear_baked_lighting`, `delete_asset`, `package_add/remove`, `set_*_settings`처럼 되돌리기 어려운 명령은 **사용자 확인 후** 실행합니다.

### 3.3 검증

1. 코드 수정 후 `recompile` → `recompile_status` → `console_status`로 **컴파일 에러 0**을 확인합니다.
2. `run_tests`로 EditMode 테스트를 돌립니다. **전체 실행은 `async_tests: true`** — 동기 실행이 에디터를 멈춘 적이 있습니다. 기준 개수는 적어 두지 않습니다. **직전 실행 결과와 비교**하고, 실패가 이번 변경과 무관하면 그렇다고 보고합니다.
3. **테스트 전에 씬을 저장하십시오.** 열린 씬이 수정된 채로 테스트를 돌리면 「씬 저장?」 대화상자로 에디터가 멈춥니다. 저장할 수 없으면 리플렉션으로 해당 시험만 실행합니다.
4. 플레이 모드 확인은 근무 씬 `Assets/0. Main/01 Scene/PlayScene.unity`에서 합니다. **개발자 모드(`-` 키, `DevModePanel`)**로 축·시계·조우·수칙 단서·점검을 조작합니다(④ 조우 탭 [그 시각·자리로]·[흐름 확인], ⑥ 수칙 탭 [이동]·[단서], ⑦ 점검 탭 [이동]·[정상]·[이상], ② 시간 탭 [잠긴 문 무시]·[모든 문 열기]).
5. 플레이 중에는 `set_component_properties`가 안 됩니다 → `eval`. `FindAnyObjectByType`는 DontSave 오브젝트를 찾지 못합니다.
6. 병합 후 빌드 씬 목록이 옛것이면 Unity를 재시작합니다(**재시작 전에 저장하지 마십시오**).
7. 조도 연출은 **Game 뷰**로 확인합니다(Scene 뷰가 초록색이면 디버그 드로우 모드입니다).
8. `capture_game_view save_path`는 `Assets/` 기준 경로입니다 — 검수 캡처를 프로젝트 안에 남기지 마십시오.

### 3.4 경비실 CCTV · 엠비언트 (런타임 자동 설치)

- **CCTV** `Assets/_Game/Flow/Cctv/`(`CctvSystem`·`CctvConfigSO`·`CctvOnlyVisible`), `Resources/CctvConfig.asset`, 셰이더 `Resources/Cctv/CctvScreen.shader`.
  - 320×240 RT 하나, 카메라 5대 중 고른 채널만 한 프레임 켜서 그립니다. 카메라마다 자식 IR 스폿을 그리는 동안만 켭니다. **노이즈는 작게** — 화면이 플레이어 후처리를 한 번 더 거칩니다.
  - `Old CRT Monitor`는 **로컬 Z가 위, −Y가 앞**입니다.
  - `CctvOnlyVisible`을 붙인 물체는 CCTV 카메라에만 보입니다(그림자 끔).
  - 화면 속 사람·얼굴 점프스케어 중에는 채널 잠금(`ChannelLocked`). K1 판정은 그만보기(`PlayerLeftView` → `SignalKind.CctvLeft`) 위반(`KeepWatchingJudge`).
- **엠비언트** `Assets/_Game/Flow/Ambience/`, `Resources/AmbienceConfig.asset`. 바탕·룸톤·불안 레이어·원샷 네 겹, 구간은 `EventBus.BandChanged`(청각)로만 받습니다. 공개 API `AmbiencePlayer.PlayAt` · `PlayStinger` · `Duck`.
- 설치 방식: `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` + `SceneManager.sceneLoaded`(흐름이 Main→Loading→PlayScene이라 첫 씬만 보면 놓칩니다). 런타임 오브젝트에 `DontSave`를 붙이지 않습니다(§5.5).

---

## 4. 코드 구조

### 4.1 어셈블리 — 의존은 단방향

```
Assets/_Game/
├── Scripts/NightDuty.Core.asmdef     references: [InstructionDT]   판정 · 수치 · 편성 (Tests·Editor에 InternalsVisibleTo)
│   ├── Core/          Vocabulary · Bands · EventBus · IFearAxisReader · Deltas · SensingRules · BodyRules
│   ├── Data/          RuleSO(태블릿 표시 전용) · NightDeckTableSO(빈 폴백, 김진선님 TabletDocument가 읽음)
│   ├── Rules/         JudgeSignal (신호 어휘)
│   ├── FinalRules/    FinalRuleBook · FinalJudges (수칙 판정)
│   ├── Program/       NightProgram · ProgramCatalog · ProgramDirector (밤 편성 · 수칙/조우 카탈로그)
│   ├── Paradox/       ParadoxCatalog · ParadoxPlanner · ParadoxRun · UnavoidableCatalog · UnavoidableRun
│   ├── Inspection/    InspectionBoard · InspectionCatalog · InspectionPlan · AnomalyLook · InspectionLedger
│   ├── Duty/          DutyCatalog · DutyDispatcher ([근무 지시])
│   ├── Direction/     NightRun(.Final/.Program/.Direction/.Tablet/.Ending/.Paradox/.Unavoidable/.Orders/.Duties/.Battery/.Camp/.Cctv/.Lead/.Guide/…) · NightClock · FinaleWatch · JudgeTarget(+Registry) · ParadoxMessage · ModelProgress · GuardRoomLoiter · IlluminanceTint
│   │   └── Tension/   TensionDirector · EncounterScripts · SurpriseBudget · StagePoints · DirectionEvent
│   ├── Stats/         FearAxisSystem · BandResolver · DayFloor · DaySummary · DutyLogEntry · NightSnapshot · RestartPolicy · WarningLedger
│   └── Editor/        NightDuty.Editor.asmdef
├── Tests/EditMode/    NightDuty.Tests.EditMode.asmdef
├── Resources/         CaptureCast · FinaleCast · DoorPolicy · CctvConfig · AmbienceConfig · DirectionSounds · IlluminanceTone · InspectionAnomalyProps · StandIns/ …
└── Flow/              asmdef 없음 → Assembly-CSharp. 씬과 코어를 잇는 구동기
    │                  NightRunDriver · NightDutyResultMapper · SpaceZones · TabletBridge · FlowAutoInstall · MainMenuOptions
    ├── Sensors/       PlayerSensors · GazeProbe · FlashlightRelay · DoorRelay · FinalRuleRelay · InspectionSensor (자동 설치)
    ├── Finale/ · Capture/ · Audio/ · Direction/ · Presentation/ · Interaction/ · Player/ · Cctv/ · Ambience/
    └── Editor/        DirectionSoundTableBuilder(소리 표 정본) · DoorPolicyBuilder · …
```

- **`NightDuty.Core`는 연출을 참조하지 않습니다.** 축이 바뀌면 Core는 이벤트만 올리고, 무엇을 그릴지는 Flow가 정합니다. **asmdef에 참조를 추가해 우회하지 마십시오.**
- 새 파일의 자리는 「고르는 것(Direction·Program) / 판단하는 것(FinalRules·Inspection) / 숫자를 올리는 것(Stats) / 그리는 것(Flow)」으로 정합니다.
- 진선님 코드(GameFlow·Result·HUD)는 개인 폴더의 `Assembly-CSharp`에 있습니다.
- 지금 메뉴는 `야간근무 ▸`입니다.

### 4.2 판정 흐름

```
센서·문·연출 ──JudgeSignal──▶ NightRun.Send / Tick
                                   ├▶ FinalRuleBook (그날 수칙 판정) ── 위반/위협/준수 ─▶ FearAxisSystem.Apply
                                   ├▶ TensionDirector (조우·수칙 단서·가짜 놀람 시점) ─▶ EventBus.DirectionEmitted ─▶ DirectionStage
                                   └▶ InspectionBoard (점검 보고·정산)
FearAxisSystem ─▶ BandResolver (래칫 · 일차 하한) ─▶ EventBus.BandChanged / BandProgress
NightRun.RequestEndNight ─▶ 수칙·점검 정산 ─▶ DaySummary ─▶ EventBus.DayEnded ─▶ PlayResultRouter ─▶ Result 씬
포획 ─▶ EventBus.Captured(axis) 1회 ─▶ CaptureDirector ─▶ RestartAfterCapture (결근이면 DayEnded)
```

- 역설 문자는 `NightRun.Paradox`가 보냅니다. 통로는 `EventBus.MessageSent` → `TabletBridge`.
- **`RequestEndNight` 수락 조건은 만들지 않습니다.** 04:00 무조건 종료(조기 퇴근은 `CanEndShiftEarly`).
- 구동기(`NightRunDriver`)는 씬에 놓지 않아도 자동 생성됩니다. 판정 시간은 `Tick(Time.deltaTime)` 실제 초 — 배속을 곱하지 않습니다. 실시간↔게임 분 환산은 **그날 배속**(`NightClock.…For(day)`)으로 합니다.
- 재시작으로 되돌릴 상태는 `ISnapshotable`로 스냅샷에 넣습니다(밤 시작 / 체크포인트 02:16).

### 4.3 핵심 API (요지)

- `NightRun`: `StartNewRun` · `BeginNight(day, clockMinutes)` · `Tick` · `Send(in JudgeSignal)` · `RequestEndNight` · `RestartAfterCapture` · `SignCheckpoint` · `BuildSummary` · `Day`·`Axes`·`IsCaptured`·`Cause` · 디버그 `DebugForceCapture`·`DebugAddAxis`·`DebugForceEncounter`·`DebugFireRuleCue` …
- `EventBus`: `BandChanged`·`BandProgress`·`DayEnded`·`Captured`·`MessageSent`·`DirectionEmitted`·`FinalRuleSettled`·`NightRestarted` … `ClearAll()`.
- `BandResolver`: `SetDayFloor` · `Shown` · `Peak` · `RestoreReached`. `FearAxisSystem.Lower(axis, amount, sourceId)`.
- `NightClock`: `PhaseAt` · `IsJudging` · `RealSecondsAt(분, day)` · `MinuteAtRealSeconds(초, day)`. 추적기 이벤트 `Call1`·`Call2`·`ShiftEnded`.
- `RestartPolicy`·`WarningLedger`·`NightSnapshot`: 재시작·경고·스냅샷 규칙 수치는 `RestartPolicy` 한 곳.
- 세부 시그니처는 코드를 직접 읽으십시오. 이 절에 새 API를 길게 옮겨 적지 않습니다.

### 4.4 신호 규칙

- 응시·근접 샘플은 **0.1초 고정 간격**(반드시 **누산기** — §5.5), 응시는 대상이 없어도 빈 ID로 보냅니다. 판정 시간은 `Tick`으로만. `NightBegan`·`NightEndAccepted`는 NightRun이 만들므로 보내지 않습니다.
- **같은 순간 순서:** `Tick` → `PassageCompleted` → `ZoneExited` → `InspectionCompleted` → `SpaceExited`.
- **대상 ID**는 소문자·숫자·점이며 데이터와 글자까지 같아야 합니다(예: `toilet.stall.inner`, `inspect.K-3`).

### 4.5 문 상호작용 (`Assets/_Game/Flow/Interaction/`)

- `PlayerInteractor`(조준·게이트·판정 출처) · `DoorHandle`(벤더 `DoorScript` 여닫기) · `InteractionHud`(조준선·안내). 사거리 1.8m, `SphereCast` 반경 6cm, **트리거 무시**(벤더 트리거 상자가 커서 서 있기만 해도 잡힘, 반경 0이면 양문 틈으로 빠짐).
- **판정 출처가 핵심입니다.** 상호작용기가 **조준한 그 문에만** `DoorRelay.BeginPlayerMove()`를 걸어 `ActionSource.Player`를 못 박습니다. 그래서 첫 프레임에 벤더 문의 `controls.openButton`을 `KeyCode.None`으로 거둡니다 — 빼면 한 프레임에 두 번 열리고 출처를 잃습니다.
- **`Assets/NOT_Lonely/`를 타입으로 참조하지 않습니다.** `DoorHandle`이 리플렉션으로 씁니다.
- 열리는 문은 `Resources/DoorPolicy.asset`(`Storage` / `Openable` / `Sealed`)이 정합니다. 잠긴 문은 움직이지 않으므로 판정 신호도 나가지 않습니다.

### 4.6 코드에 남은 호환 흔적 — 새 코드에서 쓰지 마십시오

| 위치 | 현재 | 조치 |
|---|---|---|
| `DaySummary.ImprintAxis`, `Conflicts*` | 호환용(null/0), 진선님 `ResultController`가 읽음 | 연결 작업 때 함께 제거 |
| `AxisCritical(FearAxis)` | 옛 사망 통로 — `Captured` 구독자가 없을 때만 | 새 코드는 `Captured`를 구독 |
| 진선님 `FakeDayData` 신뢰 100 표시 | 신뢰 포획 경로 없음 | 연결 작업 때 정리 |

- 2026-10-03 폐기한 것(옛 24장 카드·카드 빌더·`DayDirector`·`RuleBook`/`RuleWatcher`/`JudgeWorld`·옛 `EncounterDirector`·옛 `ParadoxDirector`·`AnomalyCueDirector`·`ProximityProbe`·`BandTableSO`·`DayBriefText`·런타임 하네스·F3 콘솔)는 **되살리지 마십시오.**

### 4.7 기획 데이터 테이블(DT) — 옮긴 값의 정본은 xlsx

- 원본 `DesignData/INSTRUCTION_DT_v6.xlsx`(기획 담당이 고침) → 메뉴 `Tools/DT/검사만` → `Tools/DT/내보내기` → `Assets/Resources/DT/*.csv` + `Assets/DT/Generated/*.g.cs`. **CSV·`*.g.cs`는 손으로 고치지 않습니다**(다음 내보내기가 덮어씀). 사용법은 `../Docs/INSTRUCTION_DT_v6_개발전달/docs/`(README·연결법·변경내역 — 크니 검색해서 읽기).
- 어셈블리: `Assets/DT/InstructionDT.asmdef`(런타임) · `Assets/DT/Editor/InstructionDT.Editor.asmdef`(내보내기). Core·Tests가 `InstructionDT`를 참조합니다(데이터 층 단방향 — Core→Flow 금지와 별개).
- 코드 진입점 `NightDuty.DesignData`(`Scripts/Data/DesignData.cs`): `Setting`·`SettingInt`·`SettingBool(id)`, `SegmentMinute`, `DayInts`. 플레이마다 다시 읽고, 에디터에서는 파일에서 바로 읽습니다. 없는 ID·빈칸은 예외입니다.
- 옮긴 값은 `static X => DesignData.Setting(390xxx);   // DT 390xxx 이름` 형태입니다. **숫자를 코드에 다시 적지 말고 xlsx를 고칩니다.** 상수가 필요한 자리(switch case·기본 인자·attribute)에는 DT 값을 못 쓰니 오버로드·if로 풉니다.
- 옮긴 범위·남은 범위는 인수인계서 §6. 아직 옮기지 않은 값은 코드가 정본입니다. DT 값과 최종 기획서가 다르면 사용자에게 확인합니다.
- `DesignDataTests`: 코드가 가리키는 설정 ID가 DT에 모두 있는지, DT를 바꾸면 코드가 따라가는지 확인합니다. 내보내기 뒤 EditMode를 돌리십시오.

---

## 5. 구현 규칙과 함정

### 5.1 C#과 어셈블리

1. **C# 9까지만** 씁니다. file-scoped namespace, `record`, `global using`은 **금지**입니다.
2. **`Assets/NOT_Lonely/`의 스크립트를 `_Game` 코드에서 참조하지 마십시오.** 벤더 폴더는 gitignore라 없는 팀원의 빌드가 깨집니다. 프리팹·모델·머티리얼은 GUID 참조 가능.
3. `Editor` 어셈블리는 `Assembly-CSharp`을 참조할 수 없습니다. 필요하면 `Type.GetType("이름, Assembly-CSharp")`.
4. `.csproj`는 `.cs`가 하나 이상 있는 어셈블리에만 생성됩니다.

### 5.2 이벤트와 컴포넌트

5. **`from == to`인 `BandChanged`가 옵니다**(전체 재방송). **연출은 멱등해야 합니다.**
6. **`OnDisable`에서 `EventBus` 구독을 반드시 해제**하십시오. static이라 씬을 바꿔도 살아 있습니다.
7. **`EventBus.ClearAll()` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`은 죽은 코드가 아닙니다.** 도메인 리로드를 끄면 구독이 살아남아 중복 발화합니다.
8. `AddComponent` 직후 `OnEnable`이 즉시 실행됩니다. 필드를 채우기 **전**이므로 초기화를 `OnEnable`에만 의존하지 마십시오.
9. 조명 intensity에 배수를 곱할 때는 **원본 값을 따로 보관**하십시오(설치 때 값 기준). 현재 값에 곱하면 지수적으로 어두워집니다.
10. 플레이 직후에는 `NightRunDriver.OnDestroy`가 정적 스위치를 되돌렸는지(`NightRun.ProgramEnabled == false`) 확인합니다.
11. 선형 색 공간입니다. 눈에 보일 UI 불투명도를 CanvasGroup 알파에 그대로 넣지 말고 `Mathf.GammaToLinearSpace`로 바꿉니다.

### 5.3 판정·연출 구현

12. **시야 밖 판정에 `OnBecameInvisible()`을 쓰지 마십시오.** 씬·그림자 카메라에도 반응합니다. 프러스텀(`GeometryUtility.TestPlanesAABB`) + `Physics.Linecast` 가림 + N프레임 연속 비가시를 조합합니다.
13. 식별 타이머와 금지 응시 타이머는 분리합니다.
14. 연출 구간은 내려가지 않습니다(래칫). 히스테리시스는 없습니다.

### 5.4 오디오

15. 횟수 단서는 반드시 단발 클립으로 발주합니다(루프는 횟수를 셀 수 없음).
16. 분위기 효과음은 수칙 단서 신호를 보내지 않습니다.
17. **소리는 키로 찾습니다.** 코드는 `DirectionSoundTableSO.Find/FindExact(키)`, 키→파일은 `DirectionSoundTableBuilder.Map` 한 곳. 새 소리는 Map에 줄을 더하고 메뉴 「야간근무/연출/소리 표만 다시 만들기」. 코드에 키를 더하면 `SoundWiringTests.RequiredKeys`에도 더하십시오.
18. **Resources 아래에 같은 이름(확장자만 다른) 소리를 두지 마십시오.** `Resources.Load`가 아무거나 고릅니다.
19. 외부에서 나는 소리(문·단서)는 3D로 실제 위치에서 냅니다. 2D는 태블릿·몸·스팅어·앰비언스 바닥만.

### 5.5 신호·센서 함정

20. **0.1초 고정 간격은 반드시 누산기로 구현하십시오**(`_acc += dt; while (_acc >= 0.1f) { _acc -= 0.1f; … }`). `Time.time` 게이트는 드리프트해 3초 응시가 3.4초가 됩니다.
21. **`JudgeTarget.IdOf`는 `GetComponentInParent`로 ID를 찾습니다.** 콜라이더 없는 빈 오브젝트는 레이가 못 맞힙니다. 대상에는 작은 `BoxCollider`를 붙이고 Renderer는 끕니다.
22. 손전등 상태는 바뀔 때마다 **그리고 밤 시작 직후 현재 상태 1회** 보내야 합니다. 유예 타이머는 코어가 셉니다.
23. **런타임에 만든 오브젝트에 `HideFlags.DontSave`를 붙이지 마십시오.** 씬이 내려갈 때 파괴를 면해 에디터에 쌓입니다. 잔재는 `Resources.FindObjectsOfTypeAll<GameObject>()`로 찾아 지웁니다.
24. 재시작 뒤 같은 공간에서 다시 시작하면 `SpaceZones`가 「바뀔 때만」 보내므로 `EventBus.NightRestarted`에서 기억을 비웁니다 — 공간 신호를 새로 만들 때도 같은 함정을 확인하십시오.

---

## 6. 폴더 구조

- `Assets/_Game/` — **팀의 실제 게임 콘텐츠.** 전부 git 추적. 벤더 프리팹을 고칠 때는 원본을 수정하지 말고 `Assets/_Game/Prefabs/`에 **Prefab Variant**를 만듭니다.
- `Assets/0. Main/` — **공통 작업 폴더.** 게임 흐름 씬 `01 Scene/{Main,Loading,Play,Result,Contract}Scene`과 `06 Data/`.
- `Assets/3.1. Programmer_lee/` — Lee 개인 폴더(`PlayScene_Lee.unity` 등).
- `Assets/3.2 Programmer_Kim/` — 진선님 개인 폴더(SceneFlow·GameTime·GameSession·ResultController·PlayResultRouter·`PlaySystems.prefab` 등).
  - 폴더명 표기가 불일치합니다(`3.1.` 뒤에는 점, `3.2` 뒤에는 없음). **경로를 하드코딩하지 마십시오.** `3.2 Programmer_Kim`은 브랜치명(`Programmer_Jinsun`)과 다릅니다.
- `Assets/1. Design/`, `Assets/2. Art/` — 기획·아트 팀 작업 폴더.
- `Assets/NOT_Lonely/` — 서드파티 패키지. **gitignore 대상**이며 팀원마다 같은 버전을 로컬에 설치합니다.
- 개인 폴더는 일회성 실험 전용입니다. 공유 코드와 출시 콘텐츠는 처음부터 `_Game`에 둡니다(나중에 옮기면 참조가 깨집니다).
- `../Docs/` — `형상관리_매뉴얼.md`, `Claude outputs/`(인수인계서·CHANGELOG·QA 리포트).

---

## 7. 씬과 라이팅

- 조도 연출용 라이트는 **Point/Spot + Realtime**입니다. Mixed는 간접광이 남습니다.
- **벤더 데모씬의 조명은 전부 Area Light(베이크 전용)** 이라 실시간으로 켜고 꺼도 반영되지 않습니다.
- Additional Light Shadow 아틀라스가 2048이라 그림자를 던지는 등이 많으면 부족합니다. 대부분 `Shadows: None`.
- **벤더 데모씬 사본(`DemoScene_LeeTest.unity`)은 부품 창고로만 씁니다.** 그 안에서 작업하거나 커밋하지 마십시오.
- **라이트맵 재베이크는 반드시 메뉴 「NightDuty/아트/라이트맵 재베이크 (창 광원 켜고 굽고 되돌림)」로.** 그냥 Generate Lighting을 누르면 평소 꺼 둔 창 광원(`WindowLights_<방>`) 없이 구워져 씬이 어두워집니다. 오래 걸리니 자리를 비울 때 돌립니다.
- 베이크 품질은 시험값(해상도 10 · 간접 128 · 직접 16 · 바운스 2)입니다. 룩을 확정할 때 원래 값(28 · 512 · 32 · 3)으로 되돌려 다시 굽습니다(`Assets/2. Art/01 Scene/Art_DemoSceneSettings.lighting`).
- 플레이어 카메라의 오클루전 컬링은 꺼 두었습니다(벤더 벽 키트의 빈 L자 Occluder가 화면을 지움). 구운 데이터는 남아 있어 체크박스로 되돌릴 수 있습니다.
- 벤더 셰이더를 URP 17로 옮길 때: `InputData inputData = (InputData)0;` + `positionCS`·`normalizedScreenSpaceUV` 채우기, `_CLUSTER_LIGHT_LOOP` 키워드 추가. 섀도마스크 키워드는 넣지 마십시오(`mixedBakeMode = IndirectOnly`). 예: `Assets/2. Art/Shaders/NL_MaskedPBR_URP17.shader`.

---

## 8. 환경 필수 수정과 알려진 문제

1. **벤더 셰이더 수정 — 모든 PC에서 각자 해야 하며 커밋할 수 없습니다.**
   `Assets/NOT_Lonely/HQ_AbandonedSchool/Shaders/NOT_Lonely_LightRays.shader`에서 `uniform float4 _CameraDepthTexture_TexelSize;` **두 줄을 삭제**합니다.
   **증상:** `redefinition of '_CameraDepthTexture_TexelSize'`. `URP.unitypackage`를 다시 임포트하면 수정이 되돌아갑니다.
2. `HQ_AbandonedSchool`은 함께 들어 있는 `URP.unitypackage`를 반드시 임포트해야 합니다.
3. 콘솔의 `Account API did not become accessible...` 경고는 무시해도 됩니다.
4. 압축 해제 시 백신이 `.cs`/`.unity` 파일을 격리한 사례가 있습니다. 벤더 폴더에 파일이 실제로 있는지 확인하십시오.
5. 결과창→PlayScene 전환마다 「referenced script missing」 경고 1, `1. Design` `SuccessEndingSequence` 컴파일 경고 2는 알려진 것입니다.

---

## 9. Git / LFS

자세한 내용은 `../Docs/형상관리_매뉴얼.md`를 따릅니다.

- **git은 사용자가 직접 합니다. Claude는 git 명령(status·log·커밋·병합·stash·lfs lock 등)을 실행하지 않습니다.** git 상태가 필요하면 사용자에게 묻습니다. **커밋 메시지는 사용자가 「내가 커밋할게」라고 할 때만, 지난 커밋 메시지 이후의 변경 전체를 하나로 묶어 제공합니다.** 작업 보고 끝에 매번 붙이지 않습니다.
- **씬·프리팹 수정 전에 잠금 확인을 사용자에게 요청합니다.** `.unity`와 `.prefab`은 LFS 잠금 대상입니다.
- **`Assets/NOT_Lonely/`는 gitignore입니다.** Git 클라이언트의 "Discard All"이 이 폴더를 통째로 삭제할 수 있습니다.
- 대용량 바이너리는 `.gitattributes`에 따라 LFS로 추적합니다. LFS를 우회하거나 `.gitattributes`를 개인적으로 수정하지 마십시오.
- `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, `UserSettings/`, 프로젝트 루트의 `Claude outputs/`는 커밋 대상이 아닙니다.
- 커밋 프리픽스: `feat:` · `fix:` · `art:` · `chore:` · `docs:`. 메시지는 **Summary(50자 내외) + Description**, 파일 목록보다 **왜 이렇게 했는지**와 **실수하기 쉬운 점**을 적습니다.

---

## 10. 사람과 브랜치

> **이 세션에서 말을 거는 사람은 「이성현(Lee)」이고, 「민」은 같은 사람의 호칭입니다.**
> 문서·보고에서 **「민이 ○○한다」처럼 3인칭으로 쓰지 마십시오.** 씬 작업도 본인이 직접 합니다.
> 김진선님 요청으로 작업하는 경우도 있습니다 — 요청자를 CHANGELOG에 적습니다.

| 사람 | 역할 | 브랜치 | 개인 폴더 |
|---|---|---|---|
| 이성현 (Lee, 「민」) | **시스템** — 판정 · 수치 · 편성 · 시스템 통합 · 플레이어 센서 · `PlayScene` 판정 관련 씬 작업 | `Programmer_Lee` | `Assets/3.1. Programmer_lee/` |
| 김진선 (Kim) | **클라이언트** — 통일 씬, 태블릿 UI · HUD · 결과창 · 공간 연출 · 오디오, 기획 가이드. 씬 잠금을 가장 자주 잡습니다 | `Programmer_Jinsun` | `Assets/3.2 Programmer_Kim/` |

- 원격 브랜치: `main`, `Programmer_Lee`, `Programmer_Jinsun`, `hyunuung`, `Art`. 개인 브랜치에서 작업한 뒤 `main`에 병합합니다.
- 공간 레이아웃이나 대상 서수(「세 번째 칸」 등)를 바꾸면 수칙의 의미가 바뀝니다. 시스템 담당에게 알리십시오.

---

## 11. 작업 방식

### 11.1 세션 시작 절차

1. 이 파일 → 필요하면 인수인계서 · CHANGELOG 맨 위 몇 항목 · 최종 기획서의 해당 절.
2. Unity 연결 확인: `editor_status`(ready) → `console_status`(에러 0) → EditMode 테스트(직전 결과와 비교).
3. git 상태는 **사용자에게 묻습니다.**
4. 다음 작업은 사용자 확인 후 착수합니다.

### 11.2 원칙

- 작업을 맡기면 **끝까지 해 주기를 기대**합니다. 단, 파괴적이거나 되돌릴 수 없는 작업은 먼저 확인합니다.
- **재지 않은 것을 적지 마십시오.** 수치·상태·개수를 적기 전에 에디터·코드에서 읽어 확인합니다(`eval`로 상수·에셋·씬 조회). 「있다/없다/끝났다」는 특히 위험하니 실측 날짜를 함께 적습니다. `enumValueIndex`는 열거자 값이 아니라 순번입니다.
- **문서 갱신 규칙(2026-11-19 개정):**
  1. **변경 경위·시뮬 결과·QA 내역·사용자 요청 원문은 `../Docs/Claude outputs/CHANGELOG_야간근무.md` 맨 위에 추가합니다.** 이 파일 머리말에 개정 블록을 쌓지 않습니다.
  2. **이 파일은 규약·함정·정본 위치가 바뀔 때만 해당 절을 고칩니다.** 폐기한 것은 지우고, 되살리면 안 되는 이름만 §4.6에 한 줄로 남깁니다.
  3. 코드 상수(경계·델타·시간)는 옮겨 적지 말고 정본 위치만 적습니다.
- 작업이 끝나면 인수인계서의 진행 상황·다음 작업을 갱신합니다.
- 설계 산출물은 `../Docs/Claude outputs/`에 markdown으로 둡니다.
- 병렬 에이전트를 쓸 때는 **공유 타입 시그니처를 모든 프롬프트에 똑같이 넣고** 「자기 것만 정의하라」고 지시합니다. 에디터·디바이스 반영은 **메인 세션 한 곳에서만** 합니다.
- 경로는 세션마다 다를 수 있습니다. 절대경로를 하드코딩하지 말고 연결된 폴더를 먼저 확인하십시오.
- **기기 파일 전송(원격 세션):** 기기 셸이 없으면 스테이징/커밋으로 옮깁니다. 기기로 올릴 때는 매번 **새 `/mnt/user-data/outputs/<새 폴더>`** 에 준비하고, `expectedMtimeMs`를 쓰고, 올린 뒤 크기를 확인합니다.
- **IMGUI(디버그 패널):** 버튼 동작은 `Later(...)` 큐에 넣어 Update에서 실행, `GUI.matrix`는 finally에서 복구, 지원 안 되는 기호(✔✖⚠■) 금지.

---

## 12. 다음 작업과 미해결

- 진행 상황·다음 작업·결정 대기는 **인수인계서 §6**(2026-11-19 새로 씀)과 CHANGELOG 맨 위 항목을 봅니다. 이 파일에는 목록을 두지 않습니다(낡기 쉬움).
- 작업이 끝나면 인수인계서의 해당 절(현황·남은 일)도 고칩니다.

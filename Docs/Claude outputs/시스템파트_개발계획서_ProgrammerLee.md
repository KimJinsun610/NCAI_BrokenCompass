# 야간근무 — 시스템 파트 개발 계획서

> 담당: 게임 시스템 / 규칙·스탯 설계 / 알고리즘 (Programmer_Lee)
> 브랜치: `Programmer_Lee` · 개인 워크폴더: `Assets/3.1. Programmer_lee/`
> 근거: `근무수칙_구현난이도_판정서.md` · `8주_스코프_절단_제안서.md`
> 작성: 2026-09-09 · 대상 기간: W1~W8

---

## 0. 확정 전제

절단 제안서 §6 결정 3건을 아래로 확정하고 진행합니다.

| 결정 | 채택 |
|---|---|
| 절단 범위 | **4공간** — 복도 / 화장실 / 교실(1-1, 1-3) / 과학실. 5순찰지점 |
| 걸음 동조 소리 | **복도 청각 75~89로 이관** |
| W4 컷 라인 | **채택.** W4 종료 시 3공간 완결 빌드 확보 |

아키텍처·알고리즘은 앞서 권장한 안으로 전부 확정합니다.

| 영역 | 채택 모델 | 근거 |
|---|---|---|
| 전체 구조 | **ScriptableObject 데이터 + Watcher 추상화 + 이벤트 버스** (B안) | 카드 42장을 코드 변경 없이 데이터로 |
| 연출 시퀀스 | **Unity Timeline + Signal** (C안 부분 채택) | 툴 제작 비용 0으로 상위 구간 순차 연출 |
| 축→연출 매핑 | **히스테리시스 밴드 + 밴드 내 보간** | 경계 진동 방지 |
| 카드 드로우 | **3-tier (Scripted / Weighted / Conflict-inject) + CSP 백트래킹** | 신뢰 축의 모순 밀도 보장 |
| 응시 판정 | **각도 12° + LOS + 2배속 감쇠 타이머** | 과학실 1초/2초/3초 윈도우 성립 |
| 시야밖 이동 | **`GeometryUtility.TestPlanesAABB` + occlusion + N프레임** | `OnBecameInvisible` 금지 |
| 프로파일링 | GDD 방식 + **신호 오염 보정** | 재진입 신호에 선행 응시 조건 |

### 절단 제안서 수치 정정

제안서의 「카드 38장」은 개략 추정치였습니다. 이관·폐기를 반영한 확정 수치는 아래입니다.

| 공간 | 카드 | 판정 있음 |
|---|---:|---:|
| 화장실 | 10 | 9 |
| 교실 | 10 | 8 |
| 복도 | 11 + 이관 1 = **12** | 11 |
| 과학실 | 11 | 9 |
| **계** | **43** | **37** |

교실 9번(「문이 세 개라면」)은 **폐기 철회 — 존치합니다.** 아래 §1에서 이유를 설명합니다.

---

## 1. 에셋 인벤토리 결과 (W1 리스크 사전 해소)

`HQ_AbandonedSchool` 프리팹 351개를 전수 확인했습니다. 절단 제안서 §7의 에셋 리스크 판정을 갱신합니다.

### 해소된 리스크

| 기획 요구 | 에셋 | 상태 |
|---|---|---|
| 복도 배치 90~100 「나무 그루터기와 잔디」 | `TreeTrunk` · `Grass_patch` · `Grass_single` · `Grass_weed` | ✅ **있음.** 리스크 해소 |
| 과학실 청각 90~100 창 스왑 | `Glass_WindowSingle_BrokenA` | ✅ **기획서 표기명과 정확히 일치** |
| 화장실 칸 개폐 | `ToiletCabin_openable` (+ `_static`, `ToiletCabinWall`) | ✅ 개폐 전용 프리팹 존재 |
| 화장실 세면대·거울 | `Toilet_Sink` · `Toilet_MirrorA/B/C` | ✅ |
| 조도 축 — 형광등 | `LampFluoBuiltinA` / `_ON` / **`_flicker`** | ✅ **ON·OFF·깜빡임 3종이 이미 있음.** 조도 축 구현 비용 대폭 절감 |
| 손전등 | `Flashlight` · `Flashlight_ON` · `Flashlight_ON_FirstPerson` | ✅ 1인칭 전용까지 |
| 교실 책상 25개 | `StudentDesk` · `StudentDeskB` · `StudentDeskGrid` | ✅ |
| 복도 상자 | `CardboardBoxA/B` (+ `_open` 변형) | ✅ |
| 복도 천장 타일 낙하 | `CeilingTilePieceA~D` · `_Small_A~C` | ✅ |
| 전화기 | `Telephone01/02/03` | ✅ |
| 문 (개폐 스크립트 부착용) | `DoorNarrow_door` / `DoorNarrow_frame` **분리 프리팹** | ✅ 힌지 구현에 이상적 |

### 🔴 신규 블로커 — 인체 모형 없음

**과학실의 주인공 오브젝트가 에셋에 없습니다.** 해부 모형·마네킹·해골 계열 프리팹이 351개 중 0개입니다. 과학실은 1차 핵심 3공간에 포함되므로 **W1 착수 전에 해결해야 합니다.**

| 대안 | 비용 | 판정 |
|---|---|---|
| Mixamo 무료 인체 모델 + T포즈 고정, URP 머티리얼 변환 | 반나절 | **권장.** 「받침 위에 서 있다」 포즈에 부합. 의자 착석 포즈(90~100)도 Mixamo에 있음 |
| 에셋스토어 anatomy/mannequin 구매 | 비용 발생 | 차선 |
| 아트팀 제작 요청 | 아트 일정 점유 | 아트팀 여유 확인 필요 |
| 대체 설계 — 모형 대신 `Rostrum`(연단) + 천 덮개 | 0 | 최후 수단. 공포 강도 크게 하락 |

→ **아트팀에 즉시 문의**하고, 회신 전까지는 Mixamo 모델로 화이트박스 진행합니다. 시스템 구현은 모델이 뭐든 `OffscreenTransform` + 웨이포인트라 **에셋 확정 전에도 병행 가능**합니다.

### 교실 9번 존치 결정

판정서에서 「대응 연출이 어느 테이블에도 없어 조건 영구 미발생」으로 폐기 권고했습니다. **에셋 확인 결과 철회합니다.**

`WalledDoor` · `WalledWindow` 프리팹이 존재합니다. 즉 「문이 있던 자리가 벽」과 그 역방향이 모두 프리팹 스왑으로 가능합니다. `DoorNarrow` 복제로 세 번째 문을 추가하는 것도 비용 0입니다.

→ **복도 배치 75~89 연출에 「1층 교실 문이 세 개로 보인다」를 신설**하고 교실 9번을 살립니다. 마침 복도 배치 축이 「나와 있는 물건 개수」로 자기 신고를 하는데, 문 개수 변화는 그 원리와 정확히 같은 계열입니다. 기획팀에 연출 1건 추가 요청.

부가 발견 — `WalledDoor`는 GDD 원안의 「404가 있던 자리는 벽이다」(AN-A01)를 폐교 버전으로 되살릴 수 있는 에셋입니다. 2차 확장 후보로 기록해 둡니다.

---

## 2. 담당 범위 경계

인터페이스를 먼저 못 박아야 다른 파트와 병렬로 갑니다.

### 내가 만드는 것

| 영역 | 산출물 |
|---|---|
| 판정 프레임워크 | `Condition` 7종 · `RuleWatcher` · `ComplianceMode` |
| 스탯 시스템 | `FearAxisSystem` · 히스테리시스 밴드 · 감쇠 · 임계 판정 |
| 편성 알고리즘 | `DayDirector` · 카드 드로우 3-tier · 모순 CSP |
| 프로파일링 | `PlayerProfiler` · P 가중치 · 각인축 |
| 데이터 스키마 | 전 SO 정의 + 에디터 검증 툴 |
| 연출 브리지 | `SpacePresenter` 인터페이스 · `OffscreenTransform` · `GazeTracker` |
| 결산·엔딩 | 일일 결산 로직 · 엔딩 분기 |

### 내가 만들지 않는 것 (인터페이스만 제공)

| 영역 | 담당 | 내가 제공하는 계약 |
|---|---|---|
| 씬 배치 · 라이팅 | 아트 | `SpaceRegistry` 컴포넌트 + 명명 규약 |
| 오디오 에셋 | 아트/사운드 | `AudioSequenceSO` 스키마 (간격 배열 + 소스 위치) |
| 근무수칙 문구 | 기획 | `RuleSO` 필드 정의 + 작성 가이드 |
| 태블릿 UI 비주얼 | UI | `IDocumentView` 인터페이스 + 서체 변조 훅 |
| 1인칭 이동 | (SimpleFPController 유용) | 입력 이벤트 구독만 |

> **`SimpleFPController`가 이미 프로젝트에 있습니다.** W1 이동 구현을 여기서 시작하고, 손전등·상호작용만 확장합니다. 자체 제작하지 않습니다.

---

## 3. 폴더 · 브랜치 · 커밋 규약

### 3.1 코드 배치 — 개인 폴더에 두지 않습니다

CLAUDE.md의 규칙은 「원본 게임 콘텐츠는 `Assets/_Game/`, 개인 폴더는 병합 전 WIP」입니다. 시스템 프레임워크는 **다른 프로그래머가 의존하는 코드**이므로 처음부터 `_Game`에 둡니다.

> 개인 폴더에 프레임워크를 두면, 나중에 `_Game`으로 옮길 때 **모든 SO·프리팹의 스크립트 GUID 참조가 끊어집니다.** 코드는 처음부터 최종 위치에 두고, 실험은 씬에서 합니다.

```
Assets/_Game/Scripts/
├── NightDuty.Core.asmdef          ← 어셈블리 분리 (컴파일 시간 · 의존 명시)
├── Core/
│   ├── GameClock.cs               근무 시각 · 배속
│   ├── EventBus.cs                축·위반·구간 변화 이벤트
│   └── ServiceLocator.cs
├── Rules/
│   ├── Conditions/
│   │   ├── ICondition.cs
│   │   ├── GazeAtCondition.cs         응시
│   │   ├── ProximityCondition.cs      근접
│   │   ├── DoorStateCondition.cs      문 E
│   │   ├── FlashlightCondition.cs     손전등
│   │   ├── DwellExitCondition.cs      체류·이탈
│   │   ├── ClockWindowCondition.cs    시각
│   │   └── EnterVolumeCondition.cs    진입
│   ├── RuleWatcher.cs             상태머신
│   ├── ComplianceMode.cs
│   └── ViolationLog.cs
├── Stats/
│   ├── FearAxisSystem.cs          4축
│   ├── BandResolver.cs            히스테리시스 밴드
│   ├── DecayRules.cs              감쇠
│   └── PlayerProfiler.cs          P 가중치 · 각인축
├── Direction/
│   ├── DayDirector.cs             일일 편성 · 진행
│   ├── CardDrawer.cs              3-tier 드로우
│   ├── ContradictionSolver.cs     CSP 백트래킹
│   └── PhoneDirector.cs           전화 이벤트
├── Presentation/
│   ├── ISpacePresenter.cs
│   ├── SpacePresenter.cs          구간→프리셋 적용
│   ├── LightingBandTable.cs       조도 공통 공식
│   ├── AudioSequencer.cs          카운트 기반 시퀀스
│   ├── OffscreenTransform.cs      시야밖 이동·스왑
│   └── GazeTracker.cs
├── Data/                          ← SO 정의 (클래스)
│   ├── RuleSO.cs · SpaceProfileSO.cs · AnomalySO.cs
│   ├── PhoneSO.cs · BandTableSO.cs · AudioSequenceSO.cs
│   └── SpaceRegistry.cs
└── Editor/
    ├── NightDuty.Editor.asmdef
    ├── RuleValidator.cs           데드 카드 · 상호배제 검증
    └── ReachabilityChecker.cs     NavMesh 회피 가능성 검증

Assets/_Game/ScriptableObjects/    ← SO 에셋 (데이터)
├── Rules/Corridor/ · Toilet/ · Classroom/ · ScienceRoom/
├── Spaces/ · Phone/ · Bands/

Assets/3.1. Programmer_lee/        ← 개인: 실험만
├── 01 Scene/
│   ├── DemoScene.unity            (기존 — 에셋 데모, 참조용)
│   ├── _Test_GazeTracker.unity    단독 검증씬
│   ├── _Test_Offscreen.unity
│   └── _Test_BandCurve.unity
└── 02 Scripts/                    폐기 예정 스파이크만
```

**`_Test_` 접두사 씬은 커밋하되 빌드 대상에서 제외합니다.** 알고리즘 단독 검증을 본 씬에서 하면 아트팀과 씬 락 충돌이 계속 납니다.

### 3.2 어셈블리 분리

`NightDuty.Core.asmdef`로 분리하는 이유:

- 시스템 코드 수정 시 **씬·아트 스크립트가 재컴파일되지 않음** → 반복 속도 확보
- 의존 방향을 컴파일러가 강제 → 시스템이 씬 특정 코드를 참조하는 사고 방지
- W8 밸런싱 단계에서 EditMode 테스트를 붙이기 쉬움

참조 설정: `Core` → Unity 기본만. `Editor` → `Core`. 아트/씬 스크립트 → `Core` (단방향).

### 3.3 Git 규약 — 이 프로젝트 고유 주의점

```bash
# 씬·프리팹 편집 전 반드시
git lfs locks                                   # 누가 잡고 있는지 확인
git lfs lock "Assets/_Game/Scenes/Corridor.unity"
# ... 작업 및 push 후
git lfs unlock "Assets/_Game/Scenes/Corridor.unity"
```

- **`.unity` / `.prefab`은 lock 대상**입니다. 특히 `DemoScene.unity`는 **19MB**라 충돌 시 수동 병합이 사실상 불가능합니다
- `Assets/NOT_Lonely/`는 gitignore. `git add` 시도 금지, Git 클라이언트의 "Discard All"은 이 폴더를 통째로 날립니다 — **절대 쓰지 마세요**
- 커밋 프리픽스: `feat:` `fix:` `chore:` `docs:` (art:는 아트팀)
- 브랜치: `Programmer_Lee`에서 작업 → 주 단위로 `main` 머지

**시스템 파트의 이점** — 제 산출물은 대부분 `.cs`와 `.asset`(SO)이라 **lock이 거의 필요 없습니다.** 텍스트 머지가 되니까요. 씬을 만지는 건 W1 화이트박스와 각 공간 배치 때뿐이고, 그때만 lock을 잡습니다.

### 3.4 커밋 단위

주차별로 아래 단위를 지킵니다. 리뷰 가능한 크기이고, 문제 시 되돌릴 지점이 명확합니다.

```
feat: Condition 7종 인터페이스 및 GazeAt/Proximity 구현
feat: RuleWatcher 상태머신 + ComplianceMode 3종
feat: FearAxisSystem 및 히스테리시스 밴드 리졸버
feat: 조도 공통 밴드 테이블 (전 공간 색온도 공식)
chore: NightDuty.Core asmdef 분리
docs: RuleSO 작성 가이드 (기획팀 전달용)
```

---

## 4. 시스템 설계 확정안

### 4.1 데이터 흐름

```
        [DayDirector]  ← 일차 · 축 현재값
              │
              │ ① 편성
              ▼
        [CardDrawer] ──> [ContradictionSolver]
              │              모순 N개 만족 조합 탐색
              ▼
        ActiveRuleSet (당일 카드 N장)
              │
              │ ② Watcher 인스턴스화 (미탑재 카드는 생성 안 함)
              ▼
        [RuleWatcher] × N
              │  ICondition 평가
              │  Dormant → Armed → Grace → Complied/Violated
              ▼
        [FearAxisSystem]  axis += base × P × dayMul
              │
              │ ③ BandChanged(space, axis, band) 발행
              ▼
   ┌──────────┼──────────────┬────────────────┐
   ▼          ▼              ▼                ▼
[SpacePresenter]  [AnomalySpawner]  [DocumentView]  [PhoneDirector]
 조명·오디오·배치    가중치 덱          지침록 서체      전화 편성
                                    ↑ 신뢰 축 전용
```

**신뢰 축만 경로가 다릅니다.** 다른 3축은 `SpacePresenter`(월드)로 가는데, 신뢰는 `DocumentView`(UI)와 `CardDrawer`(편성)로 갑니다. 즉 **신뢰 축은 월드에 아무것도 그리지 않고, 그날의 지침록이 얼마나 모순되는지만 결정**합니다. 연출 비용이 0인 이유이자, 이 게임의 차별점인 이유입니다.

### 4.2 핵심 인터페이스

```csharp
// ── 판정 조건 : 폴리모픽 SO 필드 ──────────────────────
public interface ICondition {
    void   Bind(RuleContext ctx);      // 필요한 이벤트만 구독
    bool   Evaluate(RuleContext ctx);  // 조건 충족?
    void   Unbind();
}

[Serializable] public sealed class GazeAtCondition : ICondition {
    [SerializeField] SpaceId space;
    [SerializeField] ObjectKind kind;     // ToiletCabin, Mannequin, Blackboard...
    [SerializeField] int ordinal;         // "세 번째 칸"
    [SerializeField] float seconds;       // 누적 응시
    [SerializeField] bool  invert;        // true면 "보면 위반"
}

// ── 규칙 카드 ─────────────────────────────────────────
[CreateAssetMenu(menuName = "NightDuty/Rule")]
public sealed class RuleSO : ScriptableObject {
    [TextArea] public string text;             // 지침록 원문 (그대로 출력)
    public SpaceId space;
    [SerializeReference] public ICondition condition;

    public ComplianceMode mode;                // ★ P0-1
    public float graceSeconds = 3f;            // 유예 (UI 피드백 없음)

    public FearAxis violationAxis;  public int violationBase;
    public int trustOnComply;                  // 신뢰 가산
    public FearAxis complyBonusAxis; public int complyBonusValue;  // "신뢰+3(배치+8)"

    [Header("드로우 제약")]
    public RuleTag[] tags;                     // 태그 중복 방지
    public BandRequirement requiredBand;       // ★ 트리거 가능 구간
    public RuleSO[] mutuallyExclusive;         // ★ 동시 편성 금지
    public RuleSO[] conflictsWith;             // ★ 의도된 상충 (모순 카운트에 산입)
    public ContradictionType contradiction;    // 상충/안심의침묵/감각문서불일치/문서현실지연
    public bool  isGuaranteed;                 // ★ 조건부 필수 카드
    public int   lockdownSeconds;              // ★ 구속 카드
}

public enum ComplianceMode {          // ★ P0-1 — A등급 카드 절반의 해법
    Attempt,       // 유효 상호작용 1회로 확정. 이후 월드 변화 무시
    StateAtExit,   // 공간 이탈 시점 상태로 판정
    Continuous     // 체류 내내 유지
}
```

### 4.3 RuleWatcher 상태머신

```
        ┌──────────┐  requiredBand 밖 / 미편성
        │ Dormant  │◄──────────────────────────┐
        └────┬─────┘                           │
             │ 조건절 성립 (트리거 발생)          │ Attempt 확정 시
             ▼                                 │ 즉시 복귀
        ┌──────────┐                           │
        │  Armed   │                           │
        └────┬─────┘                           │
             │ graceSeconds 경과              │
             │ ※ 이 구간 UI 피드백 전무        │
             ▼                                 │
      ┌──────┴──────┐                          │
      ▼             ▼                          │
 ┌─────────┐  ┌──────────┐                     │
 │Complied │  │ Violated │                     │
 └────┬────┘  └────┬─────┘                     │
      └────────────┴────────────────────────────┘
```

`Attempt` 모드에서 준수 확정 시 **즉시 `Dormant`로 내려 Watcher를 끕니다.** 이것이 화장실 1번(닫아도 다시 열림)과 과학실 1↔2(확인하려면 봐야 하는데 3초 보면 위반)를 동시에 해결하는 지점입니다.

### 4.4 히스테리시스 밴드

```csharp
public struct BandResolver {
    const int DEADZONE = 5;
    // 상승: 임계에서 전환 / 하강: 임계−5에서 전환
    public Band Resolve(int value, Band current) {
        int up   = Thresholds[(int)current + 1];        // 25/50/75/90
        int down = Thresholds[(int)current] - DEADZONE;
        if (value >= up)   return current + 1;
        if (value <  down) return current - 1;
        return current;                                  // 유지
    }
    // 밴드 내 연속량(색온도)은 별도 보간
    public float BandProgress(int value, Band b) => ...;
}
```

**등 개수는 밴드로 스냅, 색온도는 밴드 안에서 lerp.** 49↔50 진동 시 등이 껐다 켜졌다 하는 사고를 막습니다.

### 4.5 조도 공통 공식 (전 공간 1개 테이블)

| 밴드 | 색온도 | 4공간 등 개수 (화장실 4 / 교실 8 / 복도 8 / 과학실 4) |
|---|---|---|
| 0~24 | 6500K 백색 | 4 / 8 / 8 / 4 |
| 25~49 | 4500K 옅은 노랑 | 3 / 6 / 6 / 3 |
| 50~74 | 3200K 주황 | 2 / 4 / 4 / 2 |
| 75~89 | 2000K 적갈 | 1 / 2 / 2 / 1 |
| 90~100 | 진한 빨강, 대부분 소등 | 0 / 0 / 0 / 0 |

`LampFluoBuiltinA` / `_ON` / `_flicker` 3종이 이미 있으므로 **프리팹 스왑 + Light 컴포넌트 색온도 설정**으로 끝납니다. 공간별 작업은 등 개수 파라미터 4개 입력뿐.

### 4.6 P0 3건 확정 명세

| # | 항목 | 확정값 |
|---|---|---|
| **P0-1** | 준수 판정 종료 조건 | `ComplianceMode` 3종. 「닫으십시오」 계열 전부 **`Attempt`**. 확정 시 Watcher `Dormant` |
| **P0-2** | "붉게 보인다면" | **`RED_THRESHOLD = 75`** (조도 축 기준, 손전등 무관). 50~74는 의도된 회색지대로 존치 |
| **P0-3** | 미방문 페널티 | 수칙 개별 합산 금지. **「미방문 위반」 단일 건 = 배치 +22**. 구속 카드 중 경과 시간은 순찰률 분모에서 제외 |

P0-3의 +22는 복도 상자 딜레마(치움 +12 vs 안 치움)를 12:22로 맞춘 값입니다. 원안은 12:84로 선택지가 아니었습니다. W8에 재조정 대상.

### 4.7 모순 편성 CSP

```
목표: 신뢰 구간이 요구하는 모순 개수 N을 정확히 만족하는 카드 조합

제약:
  C1  같은 태그(RuleTag) 하루 1장
  C2  mutuallyExclusive 쌍 동시 편성 금지
  C3  requiredBand ∌ 현재 축 값 → 후보 제외
  C4  isGuaranteed 카드는 조건 성립 시 무조건 포함
  C5  lockdown 태그 하루 최대 1장
  C6  conflictsWith 쌍의 개수 == N

알고리즘:
  1. C4 확정 슬롯 배치
  2. Scripted 슬롯 배치 (안심의 침묵 — 1~3일차 포함, 4일차 제외)
  3. 남은 슬롯을 계층 가중치(최고 구간 3 / 그 아래 2 / 그 아래 1)로 정렬
  4. 백트래킹으로 C1~C6 만족 조합 탐색 (카드 43장 → 밀리초)
  5. 실패 시 사전 제작 폴백 덱
```

**폴백 덱은 반드시 만듭니다.** 알고리즘이 조합을 못 찾는 날 게임이 멈추면 안 됩니다. 일차×신뢰구간 조합 중 위험한 것만 5~6세트.

---

## 5. 주차별 실행 계획

각 주 끝에 **검증 조건**을 달았습니다. 못 넘기면 다음 주로 넘어가지 않고 그 주에서 해결합니다.

### W1 — 기반 · 화이트박스

| 작업 | 산출물 |
|---|---|
| `NightDuty.Core.asmdef` · 폴더 구조 · EventBus | 컴파일 통과 |
| `SimpleFPController` 확장 — 손전등 On/Off, 문 E 상호작용 | 걸어다니며 문 여닫기 |
| `GameClock` — 근무 시각(00:00~04:00) · 배속 | 태블릿에 시각 표시 |
| 복도 + 화장실 화이트박스 씬 | 순찰 동선 성립 |
| `ICondition` 7종 인터페이스 + `GazeAt`/`Proximity`/`DoorState` 구현 | — |
| 🔴 인체 모형 조달 (Mixamo 또는 아트 요청) | 과학실 블로커 해소 |

> **검증** — 화장실 칸 4개를 열고 닫을 수 있고, 손전등이 켜지고, 시각이 흐른다.

### W2 — 판정 · 축 · 조도

| 작업 | 산출물 |
|---|---|
| `RuleWatcher` 상태머신 + `ComplianceMode` 3종 | **P0-1 반영** |
| `FearAxisSystem` + `BandResolver` 히스테리시스 | 4축 동작 |
| `LightingBandTable` — 조도 공통 공식 | 축 올리면 등이 꺼짐 |
| `RED_THRESHOLD = 75` 상수 반영 | **P0-2** |
| 미방문 페널티 단일화 | **P0-3** |
| 화장실 5장 + 복도 3장 데이터화 (**태깅 포함**) | SO 8개 |
| 일일 결산 화면 (수치 표시) | — |

> **카드 태깅을 W2에 미리 합니다.** `conflictsWith` · `contradictionType`은 CSP가 W5지만, 카드를 작성하는 지금 붙여두지 않으면 W5에 43장을 다시 훑어야 합니다. 절단 제안서 §7 리스크 대응.

> **검증** — Day 1을 완주하고 결산 화면에 축 수치가 나온다. 규칙을 어기면 조도가 오르고 실제로 등이 꺼진다.

### W3 — 🔴 검증 게이트

| 작업 | 산출물 |
|---|---|
| 청각·배치 0~74 구간 (3밴드) | `AudioSequencer` · `OffscreenTransform` |
| `GazeTracker` 단독 검증씬 (`_Test_GazeTracker.unity`) | 1초/2초/3초 윈도우 성립 확인 |
| 위반 로그 (규칙 ID 없이 「지침 미준수 · 02:41」) | — |
| **외부 플레이테스트 4인** | 판정 결과 |

> **게이트 판정** (절단 제안서 §6 기준)
>
> | 지표 | 통과선 |
> |---|---|
> | 지침록을 순찰 중 2회 이상 재열람 | 3/4 |
> | 위반 건수 1~3건 | — |
> | 축 상승 원인에 대해 가설을 말함 | 2/4 |
> | **「한 판 더」 의사** | **3/4** |
>
> 마지막 줄 미달 시 **No-Go** — W4~5를 코어 루프 재설계에 쓰고 3공간으로 추가 절단.

### W4 — 🟡 컷 라인

| 작업 | 산출물 |
|---|---|
| 과학실 추가 (인체 모형 웨이포인트) | 3공간 완성 |
| 상위 구간 75~100 — **Timeline + Signal** 순차 연출 | 화장실 순차 닫힘 등 |
| 과학실 최소 조도 보장 (§1.6(2) 해결) | 암흑 불능 케이스 제거 |
| `CardDrawer` 3-tier + 제약 C1~C5 | 매일 다른 손패 |
| `RuleValidator` 에디터 툴 — 데드 카드·상호배제 자동 검증 | — |
| `ReachabilityChecker` — NavMesh 회피 가능성 검증 | 접근금지 반경 안전성 |
| 과학실 5번 반경 3m → **2m** 반영 | — |

> **컷 라인 도달 상태** — 복도·화장실·과학실 3공간, 4순찰지점, 5구간 전부, 카드 약 30장, 5일 커브. 신뢰 축·전화·§0 없음. **여기서 멈춰도 게임으로 완결됩니다.**

### W5 — 교실 · 신뢰 축

| 작업 | 산출물 |
|---|---|
| 교실 1-1 / 1-3 (동일 프리팹, 상태만 다름) | 4공간 5지점 완성 |
| **신뢰 축** — `DocumentView` 서체 변조 (TMP Font Asset 교체) | 구간별 모순 밀도 표현 |
| `ContradictionSolver` CSP + 폴백 덱 | — |
| `PlayerProfiler` — P 가중치 · 각인축 · **신호 오염 보정** | Day 3 각인축 확정 |
| 복도 배치 75~89 「교실 문 세 개」 연출 (기획 요청분) | 교실 9번 활성화 |

> **검증** — 신뢰 75 구간에서 지침록에 모순이 정확히 3개 실린다. 서체가 다른 항목이 2~3개 보인다.

### W6 — 전화 · §0 충돌

| 작업 | 산출물 |
|---|---|
| `PhoneDirector` — 공간별 전화 이벤트 | 내선 0 수신 |
| §0 조항 A형/B형 · Day 5 미인쇄 | 충돌 판정 |
| 무응답 카운트 · 엔딩 3분기 | — |
| 축별 사망 연출 4종 (연출은 간소, 로직 우선) | — |

> **검증** — Day 5까지 완주 가능하고 엔딩이 갈린다.

### W7 — 폴리시

라이팅 · 사운드 믹싱 · 폐교 에셋 배치 폴리시 · 지침록 조판(§0 가독성). **시스템 파트는 이 주에 버그 수정과 아트 지원**, 신규 기능 없음.

### W8 — 밸런싱 · QA

| 작업 | 목표 |
|---|---|
| `base` · 구간 임계 · 감쇠 재조정 | 표만 수정 (코드 무변경) |
| **P0-3의 +22 재검증** | 복도 상자 딜레마 실측 |
| 외부 테스트 8인 | 첫 플레이 Day 5 도달률 25~35% |
| EditMode 테스트 (밴드·CSP·감쇠) | 회귀 방지 |
| 빌드 · QA | 제출 |

---

## 6. 리스크 관리

| 리스크 | 등급 | 대응 | 시점 |
|---|---|---|---|
| **인체 모형 부재** | 🔴 높음 | Mixamo 화이트박스 병행 + 아트 요청. 시스템 구현은 에셋과 무관하게 선행 | **W1 즉시** |
| **W3 게이트 No-Go** | 🔴 높음 | W4~5를 재설계 예비로. 3공간 추가 절단 시나리오 사전 준비 | W3 |
| 응시 윈도우 1초 정밀도 | 🟡 중 | `_Test_GazeTracker.unity` 단독 검증. 실패 시 과학실 1·2·5의 초 수치 재조정 요청 | W3 |
| CSP가 조합 못 찾음 | 🟡 중 | 폴백 덱 5~6세트 필수 제작. 알고리즘 실패가 게임 정지로 이어지지 않게 | W5 |
| 카드 태깅 누락 | 🟡 중 | W2에 선행 태깅. `RuleValidator`가 누락 카드를 에디터에서 경고 | W2·W4 |
| 씬 lock 충돌 (아트팀) | 🟢 낮 | 시스템 산출물은 `.cs`/`.asset` 위주. 씬 작업은 W1·W4·W5로 한정하고 사전 공지 | 상시 |
| 4공간 반복감 | 🟢 낮 | 교실 2방을 「같은 방인데 다르다」로 의도적 활용. 복도 연출 밀도 최우선 | W5·W7 |

---

## 7. 타 파트 요청 사항

계획 확정과 동시에 전달합니다.

### 아트팀

1. **인체 모형 조달 가부 회신** (최우선) — 제작 가능 여부와 소요. 불가 시 Mixamo로 확정
2. 복도 배치 75~89 「교실 문이 세 개로 보인다」 배치 — `DoorNarrow` 복제 + `WalledDoor` 활용
3. `SpaceRegistry` 명명 규약 준수 — 「세 번째 칸」의 기준 방향을 **입구에서 봤을 때 좌→우**로 통일. 배치 변경 시 시스템에 통지 필요

### 기획팀

1. 절단 확정 반영 — 도서관·탈의실 삭제, 탈의실 7번(면제 카드)을 과학실 계열로 이관, 도서관 8번을 복도로 개작
2. 복도 배치 75~89에 「교실 문 세 개」 연출 신설 (교실 9번 살리기 위함)
3. 문구 수정 4건 — 판정서 §3 「기획 재작성이 필요한 카드」 표 참조 (교실 3·6, 과학실 9 등)
4. **카드 태깅 협조** — 43장에 `contradictionType` 4종 중 하나를 지정. 「이 카드가 어떤 종류의 모순인가」는 기획 판단이라 제가 임의로 못 정합니다

### 사운드

`AudioSequenceSO` 스키마에 맞춰 납품 — **「정확히 3회」처럼 카운트가 판정에 쓰이는 소리는 단발 클립으로** 주세요. 루프로 받으면 시스템이 횟수를 셀 수 없습니다. 필요 목록은 PDF 「필요한 오디오」 표에서 4공간분만.

---

## 8. 즉시 착수 항목

계획 승인 후 순서대로:

1. `Programmer_Lee` 브랜치 최신화 · `NightDuty.Core.asmdef` 및 폴더 구조 커밋
2. `ICondition` 7종 + `RuleWatcher` + `ComplianceMode` 코드
3. `FearAxisSystem` + `BandResolver`
4. `LightingBandTable` (조도 공통 공식 — 4공간 등 개수 입력)
5. 화장실 5장 · 복도 3장 `RuleSO` 작성 (태깅 포함)

2~4번은 씬을 건드리지 않으므로 **lock 없이 병렬 진행 가능**합니다. 1번 커밋 직후 바로 들어갈 수 있습니다.

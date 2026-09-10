# NightDuty.Core — 현재 상태와 사용법

> 대상: Programmer_Kim · 이후 합류자 · 미래의 나
> 기준 커밋: W1 1순위 완료 시점 (2026-09-09)
> 코드 위치: `Assets/_Game/Scripts/` · 어셈블리 `NightDuty.Core`

---

## 0. 세 줄 요약

- 공포 축 4개를 **읽는 계약**(`IFearAxisReader`)과 **알리는 통로**(`EventBus`)가 생겼습니다.
- 그 통로에 **가짜 축 공급원**(`DebugAxisDriver`)을 물려두어서, 판정 시스템이 없어도 연출 작업을 시작할 수 있습니다.
- 클라이언트가 구현할 계약 두 개(`ISpacePresenter` / `IDocumentView`)가 확정됐습니다. **여기에 맞춰 짜면 나중에 갈아끼울 게 없습니다.**

아직 규칙 판정도, 축 계산도, 카드 편성도 없습니다. **그건 W2입니다.**

---

## 1. 지금까지 한 일

| 커밋 | 내용 |
|---|---|
| `chore:` | `NightDuty.Core` / `NightDuty.Editor` 어셈블리 정의, 폴더 8개, CLAUDE.md 규칙 수립 |
| `docs:` | 기획 검토 및 개발 계획 문서 5종 (`Docs/Claude outputs/`) |
| `feat:` | 축 어휘 · 구간 표 · EventBus · 연출 계약 · DebugAxisDriver (파일 9개, 964줄) |

부수적으로 벤더 셰이더 버그 하나를 고쳤습니다 — `NOT_Lonely_LightRays.shader`의 `_CameraDepthTexture_TexelSize` 중복 선언. **gitignore 폴더라 커밋이 안 되니 각자 적용해야 합니다** (CLAUDE.md에 절차 있음).

---

## 2. 생긴 파일

```
Assets/_Game/Scripts/
├── NightDuty.Core.asmdef          references: []
├── Core/
│   ├── Vocabulary.cs              SpaceId · FearAxis · Band
│   ├── Bands.cs                   구간 표 · Progress · RedThreshold
│   ├── IFearAxisReader.cs         축을 읽는 계약
│   ├── EventBus.cs                판정 → 연출로 나가는 유일한 통로
│   └── DebugAxisDriver.cs         임시 축 공급원 (에디터 전용)
├── Data/DocumentTypes.cs          ClauseZeroType · FontVariant · RuleEntry
├── Stats/DaySummary.cs            일일 결산 페이로드
├── Presentation/
│   ├── ISpacePresenter.cs         ← Kim이 공간마다 구현
│   └── IDocumentView.cs           ← Kim이 지침록 UI로 구현
└── Editor/NightDuty.Editor.asmdef
```

아직 빈 폴더 — `Rules/`, `Rules/Conditions/`, `Direction/`. W2에 채워집니다.

---

## 3. 알아야 할 개념 3개

### 3.1 축과 구간

공포 축은 4개, 각각 **0~100 정수**입니다.

| 축 | enum | 바꾸는 것 |
|---|---|---|
| 청각 | `FearAxis.Auditory` | 소리 |
| 조도 | `FearAxis.Illuminance` | 형광등 개수 · 색온도 |
| 배치 | `FearAxis.Layout` | 오브젝트 위치 · 개수 |
| **신뢰** | `FearAxis.Trust` | **지침록.** 월드는 건드리지 않음 |

값은 5개 **구간(Band)** 으로 나뉩니다. **폭이 균일하지 않습니다** — 이게 함정입니다.

| Band | 범위 | 폭 |
|---|---|---|
| `Band0` | 0 – 24 | 25 |
| `Band1` | 25 – 49 | 25 |
| `Band2` | 50 – 74 | 25 |
| `Band3` | 75 – 89 | **15** |
| `Band4` | 90 – 100 | **11** |

직접 `value / 20` 같은 계산을 하지 마세요. **`Bands.Of(value)`를 쓰십시오.**

```csharp
Band b = Bands.Of(82);              // Band3
int  lo = Bands.LowerBound(b);      // 75
int  hi = Bands.UpperBound(b);      // 89
float t = Bands.Progress(82, b);    // 0.5 — 구간 안에서의 진행도
```

`Bands.RedThreshold`(=75)는 「조명이 붉게 보인다면」으로 시작하는 근무수칙들이 참조하는 임계입니다. 손전등과 무관하게 **조도 축 값**으로만 판정합니다.

> `Bands`에는 **히스테리시스가 없습니다.** 49↔50을 오갈 때 등이 깜빡이는 걸 막는 처리는 W2의 `BandResolver`가 얹습니다. 연출 코드는 `Bands.Of`를 직접 부르지 말고 **이벤트로 받은 `Band` 값을 그대로 쓰세요.**

### 3.2 EventBus — 단방향

판정·수치 쪽에서 연출 쪽으로 나가는 통로는 **이것 하나뿐**입니다.

```csharp
EventBus.BandChanged   // (SpaceId, FearAxis, Band from, Band to)
EventBus.BandProgress  // (SpaceId, FearAxis, float 0..1)
EventBus.DayStarted    // (int day, ClauseZeroType §0조항)
EventBus.DayEnded      // (DaySummary)
EventBus.AxisCritical  // (FearAxis) — 축 100 도달
```

`NightDuty.Core`는 `NightDuty.Client`를 참조하지 않습니다. 그래서 `FearAxisSystem`에서 `SpacePresenter`를 직접 부르는 건 **컴파일 단계에서 막힙니다.** 이건 실수가 아니라 설계입니다.

**구독자 하나가 예외를 던져도 나머지는 정상 호출됩니다.** 연출 하나가 터져서 다른 연출이 멈추면 원인 추적이 불가능해지니까요. 예외는 `Debug.LogException`으로 찍힙니다.

### 3.3 DebugAxisDriver — 이게 왜 먼저 나왔나

Kim의 연출 상태 40개는 전부 `BandChanged` 하나로 굴러갑니다. 그 이벤트를 낼 수 있는 게 완성된 판정 시스템뿐이라면, **조명 하나 확인하려고 실제로 근무수칙을 어겨야 하고**, 판정 시스템이 끝날 때까지 아예 시작을 못 합니다.

`DebugAxisDriver`는 같은 이벤트를 내는 **가짜 공급원**입니다. 실제 `FearAxisSystem`과 똑같이 `IFearAxisReader`를 구현하므로, 이걸 상대로 짠 코드는 나중에 **공급원만 바꾸면 그대로 돕니다.**

---

## 4. 사용법

### 4.1 씬에 붙이기

1. 빈 GameObject 생성 (이름 예: `__DebugAxis`)
2. Add Component → **`NightDuty/Debug/Debug Axis Driver`**
3. 슬라이더 4개가 보입니다

인스펙터 항목:

| 항목 | 의미 |
|---|---|
| 청각 / 조도 / 배치 / 신뢰 | 0~100 슬라이더 |
| `_broadcastTo` | 이벤트를 쏠 공간 목록. **컴포넌트 추가 시 5개(복도·화장실·교실1-1·교실1-3·과학실)로 자동 채워집니다** |
| `_emitProgress` | `BandProgress`도 낼지 (색온도 보간용). 기본 켜짐 |
| `_verbose` | 활성화 시 로그 1줄. 기본 꺼짐 |

**플레이 모드에 들어가지 않아도 됩니다.** `[ExecuteAlways]`라 에디트 모드에서 슬라이더를 끌면 바로 이벤트가 나갑니다.

우클릭 메뉴:
- **전체 다시 방송** — 현재 값을 조건 없이 다시 쏨
- **전 축 0으로** — 초기화

코드에서:
```csharp
driver.SetAxis(FearAxis.Illuminance, 82);   // 슬라이더를 끄는 것과 완전히 동일
int  v = driver.GetValue(FearAxis.Illuminance);
Band b = driver.GetBand(FearAxis.Illuminance);
```

### 4.2 공간 연출 구현하기 — `ISpacePresenter`

공간마다 하나씩 만듭니다. **핵심은 자기 공간만 걸러내는 것**입니다.

```csharp
using UnityEngine;
using NightDuty;

public sealed class CorridorPresenter : MonoBehaviour, ISpacePresenter
{
    [SerializeField] private Light[] _fluorescents;   // 복도는 8개
    [SerializeField] private SpaceId _space = SpaceId.Corridor;

    public SpaceId Space => _space;

    private void OnEnable()
    {
        EventBus.BandChanged  += HandleBandChanged;
        EventBus.BandProgress += HandleBandProgress;
    }

    private void OnDisable()   // 반드시 해제할 것
    {
        EventBus.BandChanged  -= HandleBandChanged;
        EventBus.BandProgress -= HandleBandProgress;
    }

    // EventBus는 전 공간에 방송하므로 여기서 자기 것만 걸러낸다
    private void HandleBandChanged(SpaceId space, FearAxis axis, Band from, Band to)
    {
        if (space != Space) return;
        OnBandChanged(axis, from, to);
    }

    private void HandleBandProgress(SpaceId space, FearAxis axis, float t01)
    {
        if (space != Space) return;
        OnBandProgress(axis, t01);
    }

    public void OnBandChanged(FearAxis axis, Band from, Band to)
    {
        if (axis != FearAxis.Illuminance) return;

        // 등 개수는 구간으로 스냅
        int lit = LitCountFor(to);
        for (int i = 0; i < _fluorescents.Length; i++)
            _fluorescents[i].enabled = i < lit;
    }

    public void OnBandProgress(FearAxis axis, float t01)
    {
        if (axis != FearAxis.Illuminance) return;
        // 색온도는 구간 안에서 보간
        // ...
    }

    public void ResetForNewDay() { /* 하루 시작 시 초기 상태로 */ }

    private static int LitCountFor(Band band)
    {
        switch (band)
        {
            case Band.Band0: return 8;
            case Band.Band1: return 6;
            case Band.Band2: return 4;
            case Band.Band3: return 2;
            default:         return 0;
        }
    }
}
```

**⚠️ `from == to`인 `BandChanged`가 올 수 있습니다.**
`DebugAxisDriver`의 「전체 다시 방송」과 `OnEnable` 기준값 송출이 구간이 안 바뀌었어도 이벤트를 쏩니다. 막 켜진 연출이 현재 상태를 받으려면 필요하거든요. **`from != to`를 전제로 짜지 마세요.** 같은 상태를 다시 적용해도 문제없게 만들면 됩니다(멱등).

**⚠️ `OnDisable`에서 반드시 구독 해제.**
`EventBus`는 static이라 씬을 바꿔도 살아 있습니다. 해제를 빼먹으면 파괴된 오브젝트를 향해 이벤트가 날아가고 `MissingReferenceException`이 납니다.

### 4.3 조도 표 — 공간별로 이 숫자만 다릅니다

| 구간 | 색온도 | 복도 | 교실 | 화장실 | 과학실 |
|---|---|---:|---:|---:|---:|
| `Band0` | 6500K 백색 | 8 | 8 | 4 | 4 |
| `Band1` | 4500K 옅은 노랑 | 6 | 6 | 3 | 3 |
| `Band2` | 3200K 주황 | 4 | 4 | 2 | 2 |
| `Band3` | 2000K 적갈 | 2 | 2 | 1 | 1 |
| `Band4` | 진한 빨강 | 0 | 0 | 0 | 0* |

**\* 과학실 `Band4`는 완전 암흑으로 만들지 마세요.** 근무수칙 7번이 「손전등을 끄고 점검하십시오」, 1번이 「모형이 제자리에 있는지 확인하십시오」(응시 1초)입니다. 둘 다 지키면 아무것도 안 보이는데 판정은 통과해버립니다. **모형 실루엣만 겨우 보이는 붉은 잔광**을 남기세요.

**보간이 연속적으로 이어집니다.** 각 구간이 자기 색온도에서 다음 구간 색온도로 lerp하면, 74(Band2 끝, 2000K에 도달)와 75(Band3 시작, 2000K)가 이어져 끊기지 않습니다.

### 4.4 지침록 구현하기 — `IDocumentView`

```csharp
bool IsOpen { get; }
void Render(IReadOnlyList<RuleEntry> rules, ClauseZeroType clause, int trustValue);
void Open();
void Close();
void MarkAttemptSatisfied(int ruleIndex);
```

`RuleEntry` 한 줄이 지침록 한 항목입니다.

```csharp
public readonly string      Text;      // 원문 그대로 출력
public readonly FontVariant Font;      // Normal / Variant1 / Variant2
public readonly bool        IsErased;  // 인쇄되지 않음 (판정은 살아 있음)
```

지켜야 할 규칙 넷:

**① 서체 차이는 미세해야 합니다.** `FontVariant`는 신뢰 축의 유일한 가시 신호입니다. 명조↔고딕처럼 확 다르면 안 되고, 자간이 살짝 넓거나 획 두께가 미묘하게 다른 정도 — **「다른 폰트다」가 아니라 「뭔가 이상하다」** 수준.

| 신뢰 구간 | 다른 서체 항목 수 |
|---|---|
| Band0~1 | 0 |
| Band2 | 1 |
| Band3 | 2~3 |
| Band4 | 3~4 |

**② `IsErased`는 줄을 지우되 번호는 유지합니다.** 「3번 다음이 5번」이 되어야 플레이어가 눈치챕니다. 번호를 다시 매기면 이 연출이 통째로 죽습니다.

**③ §0은 본문보다 확연히 작게, 스크롤 맨 아래에.** 대부분의 플레이어는 Day 2의 첫 충돌을 겪은 뒤에야 이 줄을 발견합니다. **너무 잘 보이면 게임이 망가지고, 너무 안 보이면 부당해집니다.** 폰트 크기를 인스펙터 값으로 빼두세요 — W6 테스트에서 발견율이 60% 미만이면 조정해야 합니다.

**④ 열어도 시간은 흐릅니다.** 일시정지 금지.

**⚠️ `MarkAttemptSatisfied`는 확인 메시지가 아닙니다.** 준수가 확정된 항목에 **아주 약한** 시각 변화(자간 미세 축소 정도)만 줍니다. 「됐습니다」라고 알려주면 이 게임의 설계가 무너집니다 — 플레이어는 자기가 규칙을 지켰는지 어겼는지 끝까지 몰라야 합니다. 반복 플레이어가 겨우 눈치챌 층위로 만드세요.

### 4.5 HUD에 넣으면 안 되는 것

순찰 HUD 요소는 **4개뿐**입니다: 잔여 시간, 체크리스트, 지침록 단축키, 손전등 상태.

**스탯 게이지도, 위반 알림도 만들지 마세요.** 유예 시간(2~4초) 동안 아무 피드백이 없는 것도 같은 이유입니다. 수치가 보이는 유일한 화면은 일일 결산(`DaySummary`)이고, 거기서도 위반 로그는 규칙 ID 없이 `지침 미준수 · 02:41`로만 표기합니다.

---

## 5. 빌드 심볼 주의

`DebugAxisDriver`는 파일 전체가 `#if UNITY_EDITOR || NIGHTDUTY_DEBUG`로 감싸여 있습니다. **플레이어 빌드에는 이 타입이 존재하지 않습니다.**

따라서 클라이언트 코드에서 이 클래스를 **직접 하드 참조하면 안 됩니다.** 필요하면 `IFearAxisReader`를 통해서만 쓰세요.

```csharp
// ✗ 빌드에서 컴파일 에러
[SerializeField] private DebugAxisDriver _driver;

// ✓
[SerializeField] private MonoBehaviour _axisSource;   // IFearAxisReader 구현체
private IFearAxisReader Axis => _axisSource as IFearAxisReader;
```

---

## 6. 검증 방법

컨테이너에서 mono + UnityEngine 최소 스텁으로 실제 컴파일했습니다.

| 빌드 구성 | 결과 |
|---|---|
| `UNITY_EDITOR` | 통과 |
| 심볼 없음 (플레이어 빌드) | 통과 |
| `NIGHTDUTY_DEBUG`만 | 통과 |

두 번째가 중요합니다 — `DebugAxisDriver`가 통째로 빠져도 나머지가 컴파일된다는 확인입니다.

동작 테스트: 구간 경계 14케이스(−5 / 0 / 24 / 25 / 49 / 50 / 74 / 75 / 89 / 90 / 100 / 105 등), 상하한 10개, `Progress` 클램프와 0 나눗셈, EventBus 예외 격리, `ClearAll`, `DaySummary` 축 매핑 — **전부 통과.**

Unity 쪽에서는 `NightDuty.Core.csproj` 생성으로 어셈블리 인식을 확인했습니다.

---

## 7. 아직 없는 것

| 없는 것 | 언제 |
|---|---|
| `RuleWatcher` · `ComplianceMode` · `ICondition` 7종 | W1 2순위 |
| `RuleSO` (근무수칙 카드 스키마) | W1 2순위 |
| `FearAxisSystem` · `BandResolver`(히스테리시스) · 감쇠 | W2 |
| `GameClock` (근무 시각) | W2 |
| 카드 편성 · 모순 CSP · 프로파일링 | W4~W5 |
| 전화 · §0 충돌 · 엔딩 | W6 |

**W3 검증 게이트를 통과하기 전까지 편성 알고리즘과 프로파일링은 만들지 않습니다.** 게이트가 묻는 건 「규칙을 읽고 지키는 게 재미있는가」 하나뿐이고, No-Go가 나오면 그 알고리즘들은 통째로 버려집니다.

---

## 8. 막히면

- 축을 올려도 아무 일이 없다 → `_broadcastTo`에 그 공간이 있는지, `OnEnable`에서 구독했는지
- 이벤트가 두 번 온다 → `OnDisable` 구독 해제 누락. 또는 Enter Play Mode Options로 도메인 리로드를 껐는데 `EventBus.ClearAll`이 안 불린 경우
- `MissingReferenceException` → 파괴된 오브젝트가 구독을 남긴 것. 같은 원인
- 빌드에서만 컴파일 에러 → `DebugAxisDriver`를 하드 참조한 곳이 있음 (§5)
- 셰이더 redefinition 에러 → CLAUDE.md의 벤더 패키지 절

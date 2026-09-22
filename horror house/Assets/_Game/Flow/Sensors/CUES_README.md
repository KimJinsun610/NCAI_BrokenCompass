# 단서 발신 뼈대 설치 안내 (`AnomalyCueDirector` · `CueBindingTable`)

이 세 파일이 하는 일은 **소리 없는 단서**입니다. 이상현상 표(60칸)를 읽어 이번 방문에 재생할 큐를 정하고,
바인딩 표가 지정한 시점에 `ClueDelivered` / `ClueIdentified` / `SequenceEnded`를 **판정 코어로만** 보냅니다.
음원은 나중에 같은 자리에 꽂습니다 — **판정은 그대로, 소리만 추가됩니다.**

| 파일 | 놓을 자리 |
|---|---|
| `CueBindingTableSO.cs` | `Assets/_Game/Scripts/Data/` (NightDuty.Core) |
| `AnomalyCueDirector.cs` | `Assets/_Game/Flow/Sensors/` (Assembly-CSharp) |
| `CueBindingTableBuilder.cs` | **`Assets/_Game/Flow/Editor/`** — `Scripts/Editor/`가 아닙니다 |

> `Scripts/Editor/`는 `NightDuty.Editor` asmdef 안이라 **`Assembly-CSharp`을 못 봅니다**(CLAUDE.md §5.1-3).
> 빌더의 「검사」 메뉴는 씬의 `AnomalyCueDirector`·`SpaceZones`를 직접 들여다보므로 거기 두면 컴파일되지 않습니다.
> `Flow/Editor/`는 asmdef가 없어 `Assembly-CSharp-Editor`에 들어가고, 이 어셈블리는 `Assembly-CSharp`과 `NightDuty.Core`를 **둘 다** 봅니다.

---

## 1. 먼저 — 코드 두 군데 배선 (Lee, 각 2줄)

이 뼈대는 **혼자서는 돌지 않습니다.** 아래 둘이 먼저 들어가야 합니다.

### ① `PlayerSensors`에 틱 이벤트 (필수 — 없으면 컴파일이 안 됩니다)

`AnomalyCueDirector`는 **자기 `Update`로 0.1초를 세지 않습니다.** 허브의 고정 간격을 받아 씁니다
(같은 순간의 신호 순서를 보장하기 위해서입니다 — CLAUDE.md §4.4.1).

`PlayerSensors.cs`에 두 줄:

```csharp
    /// <summary>0.1초 샘플이 끝날 때마다. 인자는 이 샘플이 대표하는 시간(초).</summary>
    public static event System.Action<float> Sampled;
```

그리고 `Sample(...)`의 **맨 끝**에:

```csharp
        Sampled?.Invoke(step);   // 응시·근접을 보낸 뒤에 부른다(식별은 그 프레임의 CurrentId를 읽는다)
```

> `Sampled`는 static이므로 **구독자는 `OnDisable`에서 반드시 풉니다.** `AnomalyCueDirector`는 이미 그렇게 합니다.
> `PlayerSensors` 쪽에서 `OnDisable`에 `Sampled = null;`을 넣지 마십시오 — 다른 구독자까지 끊깁니다.

### ② `SpaceZones`에 신호 탭 (없으면 큐 절반이 죽습니다)

`AnomalyCueDirector`는 **공간 진입·구역 진입·통행·점검 완료** 시점을 알아야 합니다.
`NightRun`은 그 신호를 되돌려 주지 않으므로, 보내는 쪽에서 한 번 더 알려 줘야 합니다.

`SpaceZones.cs`의 `NightRun.Send(...)` 다섯 군데를 아래 헬퍼로 바꾸고,

```csharp
    private static void Send(in JudgeSignal s)
    {
        NightRun.Send(s);
        AnomalyCueDirector.Observe(s);   // 큐 발신기에게도 같은 순간을 알린다
    }
```

`NightRun.Send(...)` → `Send(...)`로 고칩니다. **누산기 패치(`SpaceZones_누산기_패치.md`)와 충돌하지 않습니다** —
그쪽은 `Update` 본문만 바꾸고, 이쪽은 `UpdateSpace`/`UpdateSignalZones` 안의 호출만 바꿉니다.

배선 전에도 **공간 진입·퇴실만은** 코어의 `World.CurrentSpace` 폴링으로 대신 잡습니다(경고를 한 번 남깁니다).
`OnInspectionCompleted`·`OnZoneEntered`·`OnPassageEntered` 시점의 큐는 **전부 죽습니다** — C1·C3·H2·S2가 여기 걸립니다.

---

## 2. 씬 작업 (민, 10분)

1. `PlaySystems`(또는 `JudgeZones`와 같은 자리)에 빈 GameObject **`AnomalyCues`**를 만들고 `AnomalyCueDirector`를 붙입니다.

| 필드 | 넣을 값 |
|---|---|
| Anomaly Table | `Assets/_Game/ScriptableObjects/SpaceAnomalyTable.asset` — **반드시 직접 넣으십시오.** 이 에셋은 `Resources/`에 없어 자동 탐색이 실패합니다 |
| Binding Table | 비워 두면 `Resources/CueBindingTable`을 찾습니다(3절에서 만듭니다) |
| Band Table | 비워 두면 `Resources/BandTable`을 찾습니다 |
| Cue Audio | **비워 두십시오.** 음원이 발주되면 여기 꽂습니다 |
| Log Cues | 시험 중에는 켜 두십시오. 플레이 화면에는 아무것도 표시되지 않습니다(§2.8) |

2. 각 공간의 `SpaceLights`가 붙어 있는지 확인합니다. 조도 단서(C5·S4·T5)는 **실제로 켜진 등 개수**를 검사하므로,
   `SpaceLights`가 없는 공간에서는 조도 단서를 **시작하지 않고 경고만 남깁니다**(CLAUDE.md §2.4).

3. `SpaceZones`의 신호 구역에 **`cls11.door.outside`**를 추가합니다(3절 참조).

---

## 3. 표 만들기 (에디터 메뉴)

- `NightDuty ▸ 큐 바인딩 표 에셋 생성` → `Assets/_Game/Resources/CueBindingTable.asset` (38줄)
- `NightDuty ▸ 검수 ▸ 큐 바인딩 표 검사` → 아래를 콘솔로 보고합니다.
  - 표 자체의 모순(중복·빈 ID·시점 없는 발신 줄 등)
  - 이상현상 표에는 있는데 바인딩 줄이 없는 큐
  - **카드가 기다리는데 아무도 보내 주지 않는 판정 ID**
  - 씬에 `AnomalyCueDirector`·`SpaceZones`가 있는지

### 씬에 아직 없는 것 두 가지

| 필요한 것 | 왜 | 지금 상태 |
|---|---|---|
| 청취 구역 **`cls11.door.outside`** | C1의 분필 3획은 기획서 H절이 「**1-1 문밖에서**」라고 못박았습니다. C1의 **위반 조건이 `SpaceEntered@1-1`** 이라, 1-1 안에서 전달하면 카드가 시작되자마자 위반 판정의 의미가 사라집니다 | `SpaceZones`의 신호 구역에 없음. **ID는 제안값**이며 기획 문구가 아닙니다 |
| `JudgeTarget` 13건 | 식별 대상 5건(`cls11.desk.turned`·`corridor.debris`·`corridor.tree`·`science.bench.glass`·`toilet.stall.light`)에는 **작은 BoxCollider가 필요**합니다. 콜라이더가 없으면 응시 레이가 영원히 못 맞힙니다 | 다음작업_결정 §2-④ 작업 |

> `toilet.stalls.bothopen`에는 콜라이더가 **필요 없습니다.** T6은 두 칸 문(`toilet.stall.outer`·`toilet.stall.inner`)을
> 식별하고 판정 ID만 `toilet.stalls.bothopen`으로 보냅니다 — 식별 대상과 판정 ID가 다른 유일한 줄이며, 바인딩 표가 있어야 표현됩니다.

---

## 4. 기획에 확인받아야 하는 것 (질문 문장 그대로)

1. **「별도 방문」이란 정확히 무엇입니까?** 교실 청각 75~89의 「별도 방문의 입구에서」와 화장실 청각 50~74의 「별도 방문에서」가 그것입니다.
   지금은 **「그날 그 공간에서 일반 점검을 한 번 이상 마치고 나온 뒤 다시 들어온 방문」**으로 구현했습니다. 맞습니까?
   그리고 교실 1-1과 1-3은 같은 이상현상 표를 씁니다 — **1-1을 점검하고 1-3에 들어가는 것도 「별도 방문」입니까,
   아니면 같은 교실에 두 번째로 들어가는 것만 「별도 방문」입니까?**
2. **C5의 「등이 줄어든 것을 식별했다」는 무엇을 보면 성립입니까?** 남은 등입니까, 꺼진 등입니까, 창밖으로 새는 빛입니까?
   S4는 P15에 「남은 등을 식별한 직후」, T5는 P22에 「새는 빛을 식별한 직후」라고 적혀 있는데 **C5만 그 문장이 없습니다.**
   지금은 「그 교실의 천장등 묶음을 0.2초 응시」로 잠정 구현했습니다.
3. **T6의 「칸 두 곳이 모두 열린 상태」는 칸 두 개를 각각 0.2초씩 봐야 성립입니까, 한 화면에 둘이 함께 들어오면 성립입니까?**
   지금은 각각 0.2초로 구현했습니다. 「한 화면에 함께」로 하려면 지금까지 없던 판정(다중 대상 동시 가시)이 필요합니다.
4. **과학실 청각 25~49의 「지정 유리 기구 접촉음 추가」는 언제 납니까?** 다른 칸들은 「점검 후 퇴실할 때」·「입장 직후」처럼
   시점이 적혀 있는데 이 칸만 없습니다. 지금은 「입장 직후」로 잠정 구현했습니다(S3의 성공이 점검 완료라 점검 **전**이어야 한다는 것만 확실합니다).
5. **복도 청각 50~74의 「앞쪽 닫힘 뒤 지나온 문 닫힘」에서 두 소리 사이 간격은 몇 초입니까?** 음원 발주값과 같아야 합니다.
6. **교실 청각 75~89의 「칠판 긁기 + 교탁 의자 마찰」 두 음원의 전체 길이는 몇 초입니까?**
   C4의 성공 조건이 「두 음원 종료」라 이 숫자가 없으면 C4는 **퇴실로만** 성공합니다(90~99에서 한 구절이 더 붙으면 더 길어집니다).
7. **C1의 분필 3획, 획 사이 간격은 몇 초입니까?** 지금은 0.6초 임시값입니다.
   **반드시 단발 클립 3연타로 발주해야 합니다** — 루프 클립이면 횟수를 셀 수 없어 C1을 영원히 판정할 수 없습니다(CLAUDE.md §5.4-15).
8. **복도 청각의 「앞쪽 지정 문 닫힘」(25~49)에 연결되는 수칙이 정말 없습니까?** H2는 「뒤쪽 문」입니다.
   지금은 연출 전용(신호 없음)으로 두었습니다.

---

## 5. 설계 메모 — 나중에 고치려는 사람에게

- **큐와 판정 ID를 합치지 마십시오.** 합치는 순간 「모형·분위기 효과음은 카드 단서 ID를 보내지 않는다」(§2.7)를
  구조적으로 표현할 수 없게 됩니다. 표에서 `Send = None`인 줄은 「소리는 나되 카드는 건드리지 않는다」는 **선언**입니다.
  `board.scratch`(칠판 긁기)가 그 예입니다 — C4를 여는 것은 교탁 의자 마찰이지 칠판음이 아닙니다.
- **이번 방문의 큐 목록은 입장 때 얼립니다**(§2.5-6). 방문 중에 구간이 바뀌어도 목록을 바꾸지 않습니다.
  구간은 `EventBus.BandChanged`로만 읽습니다 — `BandResolver`의 **보류**가 이미 반영된 값이라 §2.5-6이 요구하는 값과 같습니다.
  (`from == to` 재방송이 오므로 처리는 멱등합니다. §5.2-5)
- **같은 순간에 단서를 둘 이상 내보내지 않습니다.**
  - 지연이 있는 줄(`DelaySeconds`·단발 N회)은 대기열로 가고, 대기열은 표의 `Cues` 배열 순서
    (= 기획서가 적은 재생 순서)대로 **한 틱에 하나씩** 나갑니다.
  - 지연이 0인 줄은 **그 자리에서** 나갑니다. 「점검 후 퇴실할 때」는 점검 완료와 공간 이탈이 같은 프레임에 연달아 오므로
    (§4.4.1의 신호 순서), 한 틱이라도 미루면 단서가 `SpaceExited` **뒤**에 도착해 C3·S2의 성공 조건이 이미 지나가 버립니다.
  - 그래서 같은 순간에 즉시 발신이 두 건 이상이면 **경고를 남깁니다.** 그때는 「한 방문에 신규 단기 사건 하나」(§2.5-7)의
    주인을 플레이어가 들은 순서가 아니라 **그날 덱 순서**가 정하게 됩니다. 한쪽에 `DelaySeconds`를 주어 순서를 명시하십시오.
    **이 뼈대에서 가장 조심해야 할 지점입니다.**
- **레이를 다시 쏘지 마십시오.** 식별 0.2초는 `PlayerSensors.Active.Gaze.CurrentId`를 읽어서 셉니다.
- **문을 직접 움직이지 마십시오.** `door.auto_open`·`stall.entry.auto_open`의 `DoorAutoOpenObserved`는 `DoorRelay`가 보냅니다.
  연출로 문을 열 때는 **반드시 `DoorRelay.BeginDirectionMove()`를 먼저** 불러 출처가 `Direction`으로 기록되게 하십시오.
  섞이면 H1·C6·T3가 통째로 오판합니다.
- **T2에 `SequenceEnded`는 필요 없습니다.** T2의 실패는 `ElapsedCondition(12)`이고 코어의 `Tick`이 직접 셉니다(에셋 실측).
  `SequenceEnded`를 요구하는 카드는 **C4 하나뿐**입니다(`cls11.lectern.noise`).
  다만 T2의 12초는 **판정값과 클립 길이가 같은 수**여야 합니다(§5.4-16) — 바인딩의 `SequenceSeconds`에 12를 적어 두었습니다.
- **음원을 꽂는 자리**는 `AnomalyCueDirector.TryFire`의 표시된 주석 한 곳입니다.
  `PlayOneShot`을 `ShotCount`회 `ShotIntervalSeconds` 간격으로 예약하고, 마지막 획이 끝난 뒤 지금과 같은 `Send`가 일어나게 두면 됩니다.
- **포획 이후·Tab 중에는 아무것도 보내지 않습니다**(`CanSend()`). Tab 중에는 대기열과 식별 누적도 비웁니다 —
  버려진 신호만큼 발신기 상태가 코어와 어긋나기 때문입니다.

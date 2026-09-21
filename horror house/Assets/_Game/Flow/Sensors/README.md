# 플레이어 센서 설치 안내 (`Assets/_Game/Flow/Sensors/`)

민이 씬에서 할 일만 적었습니다. 코드는 건드리지 않아도 됩니다.
**작업 전에 씬을 한 번 커밋해 두십시오.** 아래 작업은 전부 컴포넌트 추가 + 필드 채우기입니다.

---

## 0. 파일과 자리

| 파일 | 붙이는 곳 | 개수 |
|---|---|---|
| `PlayerSensors.cs` | `FPController` (플레이어 루트) | 1 |
| `FlashlightRelay.cs` | `FPController` (같은 오브젝트) | 1 |
| `DoorRelay.cs` | **문마다** — `Animation`과 벤더 `DoorScript`가 붙어 있는 오브젝트 | 아래 표 |
| `GazeProbe.cs` · `ProximityProbe.cs` | 붙이지 않습니다. `PlayerSensors` 안에 들어 있습니다 | — |

`SpaceZones`는 그대로 둡니다. 공간·구역·점검 신호는 지금처럼 `JudgeZones` 오브젝트가 보냅니다.
(다만 `SpaceZones`의 0.1초 간격에 드리프트가 있습니다 — 같은 폴더의 `SpaceZones_누산기_패치.md`를 Lee가 따로 적용합니다. 민은 신경 쓰지 않아도 됩니다.)

---

## 1. `FPController`에 `PlayerSensors` 추가

| 필드 | 넣을 값 |
|---|---|
| Gaze Camera | `FPController/Camera` |
| Player Root | `FPController` (자기 자신) |
| Step Seconds | **0.1 그대로 두십시오.** 기획 정본이 정한 판정 해상도입니다 |
| Max Steps Per Frame | 50 그대로 |
| Gaze ▸ Max Distance | 30 |
| Gaze ▸ Blocking Mask | **Everything 그대로.** 벽·문이 빠지면 응시 레이가 벽을 뚫습니다 |
| Proximity ▸ Cull Radius | 4 (3 미만으로 내리지 마십시오 — H6 반경이 2m입니다) |
| Proximity ▸ Rebuild Seconds | 2 |
| Tab Root To Watch | 태블릿 UI 루트가 생기면 넣습니다. 지금은 비워 두십시오 |

> 응시·근접 샘플은 밤이 진행 중일 때만 나갑니다. 포획 뒤·Tab이 열린 동안에는 멈춥니다.

## 2. `FPController`에 `FlashlightRelay` 추가

| 필드 | 넣을 값 |
|---|---|
| Flashlight Root | `FPController/Camera/Flashlight_ON_FirstPerson` |
| Start On | **체크 해제(Off).** 경비실에서 꺼진 채로 출발합니다 |
| Toggle Key | `F` (기획 미정값의 기본. 바꾸려면 여기서) |
| Also Right Mouse | 해제 |

> **중요:** 지금 씬의 `Flashlight_ON_FirstPerson`은 **켜진 채로 저장돼 있습니다.**
> `Start On`을 해제하면 플레이 시작 때 이 컴포넌트가 꺼 줍니다. 씬 저장 상태를 따로 고치지 않아도 됩니다.

## 3. 문에 `DoorRelay` 추가

붙이는 곳은 `JudgeTarget`이 붙은 메시가 아니라 **그 부모**(= `Animation` + `DoorScript`가 있는 오브젝트)입니다.

| 판정 ID | 붙일 오브젝트 (하이어라키 경로) | Report Auto Open Observed |
|---|---|---|
| `corridor.door.auto` | `Interior/Corridors/DoorNarrowSolid (8)` | **켬** (H1) |
| `corridor.door.back` | `Interior/Corridors/DoorNarrowSolid (3)` | 끔 |
| `corridor.door.11` | `Interior/Classroom01/Bookcase` | 끔 (C6) |
| `science.door` | `Interior/Classroom01/Bookcase (2)` | 끔 |
| `toilet.door` | `Interior/Toilet02/DoorNarrowSolid (4)` | 끔 |
| `toilet.stall.outer` | `Interior/Toilet02/ToiletCabin_openable (2)` | **켬** (T1) |
| `toilet.stall.inner` | `Interior/Toilet02/ToiletCabin_openable (4)` | 끔 (T3) |

필드는 전부 기본값 그대로 둡니다(`Door Id Override` 비움 · `Interact Key` = E · `Player Claim Seconds` = 0.4).
붙인 뒤 인스펙터가 아니라 **플레이 중 콘솔**에서 확인하십시오 — ID를 못 찾으면 경고가 뜹니다.

### `corridor.door.13`은 붙일 수 없습니다

`corridor.door.13`의 `JudgeTarget`은 `Interior/Classroom02/WallInterior_DoorwayNarrow (8)`에 붙어 있는데,
이 오브젝트에는 **`Animation`도 `DoorScript`도 없습니다. 문짝이 아니라 벽에 뚫린 문틀입니다.**
그래서 1-3 교실 문은 열 수도 닫을 수도 없고, **C6의 「직접 연 문을 닫아라」가 절반만 판정됩니다**(1-1만).
여는 문짝이 필요합니다 — 5절을 보십시오.

---

## 4. 붙인 뒤 확인 (5분)

1. 플레이 → 판정 디버그 패널(F2)을 열고 신호 로그를 봅니다.
2. 가만히 서 있어도 `GazeSample`이 **0.1초마다 계속** 나와야 합니다(대상이 없으면 빈 ID로).
   안 나오면 H2·C3의 유예 2초가 영원히 끝나지 않습니다.
3. `corridor.passage` 구역에 들어가서 **F로 손전등을 켜면** H3가 위반으로 넘어가야 합니다(2초 유예 뒤).
   반대로 끈 채로 통과하면 준수입니다. **지금 실제 플레이로 판정되는 첫 카드입니다.**
4. 문 앞에서 E → 콘솔에 `DoorCommandAccepted(..., 'corridor.door.11', Player, False, 0)`.
   다시 E로 닫으면 `DoorCommandAccepted(..., True, ...)` → 약 1초 뒤 `DoorCloseCompleted`.
   **사거리 밖에서 E를 눌렀을 때는 아무것도 나오지 않아야 정상입니다.**
5. 상자(`corridor.box`) 쪽으로 걸어가면 4m 안쪽부터 `ProximitySample`이 나옵니다. 멀어지면 한 번 더 나오고 멈춥니다.

## 5. 아직 남은 씬 작업 (민 몫, 이 센서들과 별개)

- `다음작업_결정_2026-09-20.md` ④의 **대상 13건 자리잡기**. 그게 끝나야 21장의 「대상 참조 누락 → 미판정」이 풀립니다.
  응시·식별 대상에는 **반드시 작은 `BoxCollider`**를 붙이고 Renderer는 꺼 두십시오 — 콜라이더가 없으면 응시 레이가 영원히 못 맞힙니다.
- **1-3 교실 문짝**: `corridor.door.13` 자리에 여닫히는 문 프리팹(`DoorNarrowSolid` 등)을 놓고, 그 문짝 메시에 `JudgeTarget(corridor.door.13)`을 옮긴 뒤 부모에 `DoorRelay`를 붙입니다.
- **달리기·점프 제거**(`FPController`의 `runSpeed`·`jumpForce`): 기획서 9절은 걷기만입니다. 달리기가 남아 있으면 근접 1.5m와 유예 2초 시험값이 전부 의미를 잃습니다.

---

## 6. 개발자용 메모

- `PlayerSensors.SetTabOpen(bool)` — 태블릿 UI가 열고 닫을 때 불러 주십시오.
  이 메서드는 **`JudgeSignal.Tab`을 보내지 않습니다.** Tab 신호와 게임 시계 정지는 태블릿 쪽 한 창구에서 같이 처리해야 합니다(Q6).
- `DoorRelay.BeginDirectionMove(seconds)` — 연출이 문을 움직이기 **직전**에 부르십시오.
  이 예약 없이 움직인 문은 「키 입력이 없었으니 연출」로 분류됩니다. 예약을 거는 쪽이 정확합니다.
- `PlayerSensors.Active.Gaze.CurrentId` — 앞으로 만들 식별 0.2초(`ClueIdentified`)·`ModelObserved` 발신기가 그대로 읽으면 됩니다.
  응시 레이를 한 번 더 쏘지 마십시오. 두 벌이 되면 0.1초 해상도가 둘로 갈라집니다.
- `FlashlightRelay.Active.IsOn` — 조명 연출이 읽을 수 있습니다.

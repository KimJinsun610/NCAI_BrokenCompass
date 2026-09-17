# 야간근무 (Night Duty) — 인수인계 (단일 정본)

> **새 세션은 이 문서 하나만 읽고 시작한다.** 이미 내려진 결정·발견된 함정이 담겨 있다. 재논의하지 말고 따르고, 바꿔야 하면 사용자에게 먼저 확인한다.
> 최종 개정: 2026-09-17 (Lee 세션) — 판정 코어·카드 24장·판정 패널 구현, 진선님 브랜치 병합 반영.
> **추가 개정 2026-09-17 — 신뢰 축 용도 변경(기획 결정, 정본 HTML 미반영):** 신뢰는 게임오버를 일으키지 않고, 태블릿 문자↔근무수칙 충돌(역설)을 늘리는 데에만 쓴다. 충돌 문자는 기획자가 추가한다. 코드 반영 완료(`FearAxisSystem.IsTerminal`, EditMode 272/272). 이 결정은 정본의 「신뢰 100 게임오버」 문장보다 우선한다.
> 이 문서가 대체한 것: 이전 인수인계(9/16), 판정코어 연결약속, NightDuty.Core 현황과 사용법, 시스템/클라이언트 개발계획서, 4주 스케줄, 8주 절단 제안서, 절단 확정 v2, 근무수칙 구현난이도 판정서, 진행기록·카드분석.
> 판정 규칙의 기획 정본: `야간근무_공간별_지침록_개발명세반영본.html`(2026-09-12). 카드 구현 정본: `Editor/CorridorCardBuilder.cs`, `Editor/RoomCardBuilder.cs`.
> 함께 남긴 문서: `Docs/형상관리_매뉴얼.md`(git·LFS 규칙), `Docs/Claude outputs/게임플로우_작업내역_설정가이드.md`(진선님 게임 흐름 설정법).

---

## 0. 작업 원칙 (반드시 지킬 것)

- **git은 사용자가 직접 한다.** Claude는 git 명령(커밋·병합·stash 등)을 실행하지 않는다. 커밋 메시지는 요청 시 작성만.
- 답변·문서·주석은 **한국어**.
- `Library/Pipeline/.unity-pipeline-port` 내용은 출력·공유 금지(토큰 포함).
- 되돌릴 수 없는 작업은 먼저 확인받는다.
- C# 9만 사용(파일 범위 namespace·record·global using 금지).

---

## 1. 프로젝트 개요

- **게임:** 나폴리탄 괴담(규칙 괴담) 기반 1인칭 공포. 학교 야간 경비원이 밤마다 근무 지침(규칙 카드)을 지키거나 어긴다.
- **엔진:** Unity 6000.3.17f1, URP 17.3. 경로 `C:\Users\이성현\NCAI_BrokenCompass\horror house`.
- **팀:** 이성현(Lee, 판정 시스템) · 김진선(Kim, 게임 흐름·UI).
- **근무:** 기본 5일(`GameSession.FinalDay`, 기획 미정), 1회 근무는 게임 시계 0:00→1:00(×20 배속).
- **공간:** 복도 · 교실(1-1, 1-3) · 과학실 · 화장실. 카드 24장(H1~H6, C1~C6, S1~S6, T1~T6).
- **공포 4축:** 청각 · 조도 · 배치 · 신뢰. 감소 없음, 회차 내내 누적. **청각·조도·배치 100 = 포획(게임 오버)**, AxisCritical 1회. **신뢰는 100에서 멈출 뿐 포획 없음** — 신뢰가 높을수록 태블릿 문자와 수칙의 충돌이 늘어난다.
- **밴드:** 0–24 / 25–49 / 50–74 / 75–89 / 90–99 → 공간 연출(조명 개수·소리·배치) 변화. 신뢰는 월드에 그리지 않음. Band4 = 전 공간 등 0개 + 붉은 잔광.
- **판정 규칙 요지**
  - 한 방문에 **새 단기 사건 1개**. 장기 카드는 이 몫을 쓰지 않음.
  - 같은 신호에서 준수·위반이 함께 성립하면 **위반 우선**.
  - 결과: 준수(신뢰 +) / 위반(해당 축 +) / 미판정(0).
  - 진행 중 사건이 있는 공간은 사건 종료까지 밴드 변화 보류(히스테리시스 −5).
  - 취소 조건 성립 시 카드는 대기로 복귀(방문 몫 반환).
- **폐기된 설정:** 프로파일링·각인축(`ImprintAxis`), §0 조항·전화 이벤트(→ 역설 문자 P1~P4로 대체), `ClauseZeroType`.

### 1.1 확정 기획서로 폐기된 이전 결정

새 기획서가 정본입니다. 아래는 **더 이상 유효하지 않습니다.**

| 폐기된 것 | 대체 |
|---|---|
| 3공간 절단(복도·화장실·과학실) | **4공간 확정** — 복도 · 교실 · 과학실 · 화장실 |
| 수칙 30장 | **24장** (공간당 6개) |
| **§0 조항** (전화 우선/문서 우선) | **역설 문자 P1~P4**가 그 역할을 대신함 |
| **전화 이벤트** | **태블릿 문자 12개** |
| **감쇠(Decay) 규칙** | **감쇠 없음.** 누적만 하고 줄지 않는다 |
| 프로파일링 P 가중치·각인축 | **없음.** 델타는 고정값 |
| `RED_THRESHOLD = 75` | 없음. 카드마다 `eligibleBand`를 가짐 |
| 미방문 페널티 배치 +22 | 없음. 미판정은 델타 0 |
| 「지침록을 열어도 시간이 흐른다」 | **반대.** Tab(태블릿) 중 시간·판정·음원 모두 정지 |
| 인체모형 = 과학실 전용 | **8개 장면이 4공간 전체에** 분포 |
| `DaySummary.ImprintAxis` | 불필요 (프로파일링 폐기) |
| **신뢰 100 = 게임오버** (2026-09-17 폐기) | **신뢰는 포획 조건이 아님.** 역설 문자 확장의 입력으로만 사용 |
| 신뢰에 따라 태블릿 폰트·디자인 왜곡 (2026-09-17 폐기) | 신뢰 구간에 따라 **태블릿 문자↔근무수칙 충돌이 늘어남** |
| Band4 = 90~100 | **90~99.** 100은 게임오버 트리거 |

**유효하게 남은 것:** 4축 체계 · 5구간 · 조도 공통 색온도표와 등 개수 ·

**코드에 남은 폐기 흔적:** `Bands.RedThreshold`(=75), `ClauseZeroType`/`EventBus.DayStarted` 2번째 인자, `DaySummary.ImprintAxis`(호환용). 정리 대상.

## 1A. 게임 규칙 상세 (기획서 요약 — 카드 전문·델타는 기획서 HTML이 정본)

### 1A.1 네 개의 축

| 축 | enum | 오르는 조건 | 델타 |
|---|---|---|---|
| 청각 | `Auditory` | 청각 관련 수칙 **위반** | +12 ~ +15 |
| 조도 | `Illuminance` | 조도 관련 수칙 **위반** | +12 |
| 배치 | `Layout` | 배치 관련 수칙 **위반** | +12 ~ +25 |
| **신뢰** | `Trust` | 수칙 **준수** | +2 ~ +4 (100에서 멈춤, 포획 없음) |

- **0~100 누적. 감쇠 없음, 상시 증가 없음.** 결과 한 건마다 `새 값 = min(100, 기존 + 델타)`.
- **청각·조도·배치 중 하나가 100 도달 → 포획 엔딩(게임오버).** 신뢰 100은 포획이 아니다(2026-09-17 결정).
  종료 원인 축·카드/문자 ID·현재 공간을 기록하고 **종료 요청을 한 번만** 보낸다.
  그 뒤의 미적용 델타는 중단.
- 위반은 **즉시** 큰 델타 하나. 준수는 **카드마다 지정된 완료 시점에만.**
- 준수·위반은 **배타적.** 같은 카드에 하룻밤 결과는 **하나만.** 대상 개수나 왕복 횟수로 곱하지 않는다.

### 1A.2 구간 (Band) — 폭이 균일하지 않음 ⚠️

```
Band0 = 0–24   (25칸)
Band1 = 25–49  (25칸)
Band2 = 50–74  (25칸)
Band3 = 75–89  (15칸)   ← 좁아짐
Band4 = 90–99  (10칸)   ← 더 좁아짐. 100은 게임오버
```

**`value / 20` 같은 균등 분할 계산을 절대 하지 마십시오.** `Bands.Of(value)`를 쓰십시오.
90~99 표현이 **엔딩 이전의 경고**를 담당합니다. 별도 경고 시스템을 추가하지 않습니다.

> 코드 반영 완료: `Bands`의 Band4 = 90~99, 100은 종료 잠금.

### 1A.3 조도 — 전 공간 공통 색온도표, 공간마다 다른 건 등 개수뿐

| 구간 | 색온도 | 복도 | 교실 | 과학실 | 화장실 |
|---|---|---:|---:|---:|---:|
| Band0 | 6500K 백색 | 8 | 8 | 4 | 4 |
| Band1 | 4500K 옅은 노랑 | 6 | 6 | 3 | 3 |
| Band2 | 3200K 주황 | 4 | 4 | 2 | 2 |
| Band3 | 2000K 적갈 | 2 | 2 | 1 | 1 |
| Band4 | 붉은 잔광 | 0 | 0 | 0 | 0 |

**이 표는 `BandTableSO`에 들어 있습니다.**

일부 카드는 **수치가 아니라 실제 켜진 등 개수**를 조건으로 씁니다
(C5 「4개 이하」, S4 「1개만 남았다면」). **조도값과 실제 라이트 그룹 상태를 둘 다 검사**하고,
둘이 어긋나면 사건을 시작하지 않고 개발 로그에 남깁니다.

### 1A.4 24개 근무수칙 — ID 체계

| 공간 | ID | 자기 신고 장치 |
|---|---|---|
| 복도 | **H1~H6** | 청각=앞/뒤 음원 차이 · 조도=등 8개 · 배치=우회할 물건 위치 |
| 교실 1-1·1-3 | **C1~C6** | 청각=분필 획수 · 조도=등 8개 · 배치=1-1 지정 책상·의자 방향 |
| 과학실 | **S1~S6** | 청각=유리 접촉·긁힘 · 조도=등 4개 · 배치=모형 위치 |
| 화장실 | **T1~T6** | 청각=물 내림·호칭 · 조도=등 4개 + 닫힌 칸 아래 빛 · 배치=열린 점검칸 개수 |

**전문·델타·판정 상세는 기획서 HTML이 정본입니다.** 이 문서에 옮겨 적지 마십시오 — 사본이
어긋나면 어느 쪽이 맞는지 알 수 없게 됩니다. 카드를 구현할 때 해당 카드의
「개발 판정 상세」(발생 자격·시작 / 준수 / 위반 / 종료·반복 / 예외·충돌 / 씬 연결·설정값 /
검증 절차·기대 결과) 7항목을 그대로 읽으십시오.

### 1A.5 인체모형 8개 장면

| 공간 | 연결형 (카드와 연결) | 발견형 (델타 0) |
|---|---|---|
| 복도 | **H-A** 문 앞에서 기다리는 것 → H1 | **H-B** 상자 뒤의 자리 |
| 교실 | **C-A** 문을 향한 좌석 → C2 | **C-B** 교탁의 인솔자 |
| 과학실 | **S-A** 인계품: 최초 조우 → S1 · **S-B** 돌아온 인계품 → S5 | — |
| 화장실 | **T-A** 안쪽 칸의 이용자 → T3 | **T-B** 세면대의 호출자 |

- **인체모형은 시야 밖에 있을 때만 위치가 바뀌는 유일한 존재입니다.** 배치축의 정점.
- **S-A만 첫날 과학실에 고정.** 나머지 7개는 회차 시작에 순서를 한 번 섞어 저장.
- 둘째 날부터 첫 일반 점검 완료 후 **미관찰 · 도달 가능 · 비가시 준비 가능** 후보를 고른다.
  오늘 미방문 공간과 직전 조우와 다른 공간을 우선. 후보가 없으면 이 두 선호만 차례로 완화한다.
  **실제 금지·봉쇄 조건은 완화하지 않는다.**
- 선정한 장면은 잠그고 새로 뽑지 않는다. **준비 위치를 보고 있으면 대기.**
  시야 안에서 생성하거나 움직이지 않는다.
- 모형 관찰은 **가림 없는 1초**. 그 방문의 퇴실까지 마쳐야 당일 조우 업무가 끝난다.
- **모형 효과음은 카드 단서 ID를 보내지 않습니다.** C-A의 타격음이 C3를 시작하지 않고,
  C-B의 칠판음이 C4를 시작하지 않습니다. 소리가 비슷해도 별개입니다.

### 1A.6 태블릿 문자 12개 (충돌 문자는 기획자가 추가 예정)

| 종류 | ID | 역할 |
|---|---|---|
| 발견 유도 7 | G-H1 · G-H2 · G-C1 · G-C2 · G-S1 · G-T1 · G-T2 | 미관찰 모형 쪽으로 유도. **델타 0** |
| 재방문 1 | N1 | 회차 1회. 1-3 문 앞 재방문 |
| **역설 4** | **P1 · P2 · P3 · P4** | **수칙이 금지한 바로 그곳으로 부른다** |

**문자는 이동만 요구합니다.** 발송 / 전문 노출 / 목적지 도착 / 모형 관찰은 **서로 다른 기록**입니다.
전문 노출은 본문이 태블릿 화면에 실제 표시된 상태를 말하며, 추가 확인 버튼이나 열람 시간 벌점은 없습니다.

#### 역설 4쌍 — 이 게임의 중심 장치

| 역설 | 짝 카드 | 충돌 내용 | 따르면 | 거절하면 |
|---|---|---|---|---|
| **P1** | S2 | 나온 과학실로 다시 들어오라고 함 | 청각 +12 | 신뢰 +2 + 배치 +6 |
| **P2** | H4 | 목적지가 상자 금지 반경 **안쪽** | 배치 +12 | 신뢰 +2 + 배치 +6 |
| **P3** | C1 | **유일하게 반경이 아니라 순서를 뒤집음** | 청각 +12 | 신뢰 +2 + 배치 +6 |
| **P4** | T6 | 들어가지 말라던 칸 **안으로** 부름. 델타 최대 | 배치 +15 | 신뢰 +2 + 배치 +6 |

- **신뢰 → 역설 확장 (2026-09-17 결정):** 신뢰가 높을수록 태블릿 문자와 수칙의 충돌이 늘어난다. 기획자가 P1~P4 외 충돌 문자를 추가한다. 새 문자마다 짝 카드·목적지·따름/거절 결과·충돌표 행이 정본에 있어야 구현한다. 신뢰 구간별 발송 한도·구간 판정 시점·짝 카드 편성 보강은 **기획 대기(Q10)** — 확정 전 하드코딩 금지, 데이터(SO)로 둔다. 권장: 밤 시작(또는 발송 순간)에 신뢰 구간을 읽고 그날 밤 고정.
- **주의할 순환:** 준수 → 신뢰↑ → 역설↑ → 거절 시 짝 카드 준수(신뢰↑) + 미도달 배치 +6. 규칙을 잘 지킬수록 배치 압박이 커진다. 하룻밤 최대 배치 상승량을 기획과 계산할 것.
- P형 도착 보상은 **0**, 유효 미도달은 **배치 +6** (시험 밸런스).
- 하루 최대 1쌍 · 회차 최대 2쌍 · 각 P ID 회차 1회 (현행값. 신뢰 구간에 따라 늘어날 예정).
- **전문 노출 전에 짝 카드가 정산되면 P형을 미판정으로 취소.** 소급 벌점 없음.
- 동시 후보 시 고정 순서: **P3 → P2 → P1 → P4**.
- 수칙 결과를 먼저 처리하고, 게임이 진행 중일 때 문자 결과를 적용.
  예: P4 오진입으로 배치 100이면 뒤의 +6은 적용하지 않음.

### 1A.7 설계 금기 — 선의로 어기기 쉬움 ⚠️

- **플레이 중 축·델타·위반 팝업을 표시하지 마십시오.** 플레이어는 자기가 어겼다는 사실
  자체를 모른 채 환경이 변해가는 것만 봐야 합니다.
- **공지 카드나 별도 인수 목록 UI를 추가하지 마십시오.**
- **수칙 본문에서 요구한 행동만 채점합니다.** 괴담의 이유·기관 설명은 판정 조건이 아닙니다.
  「목록에 없던 것」은 목록 UI나 생성물을 뜻하지 않고, 「이름」은 개인정보나 음성 인식을,
  「손이 네 개」는 추가 모델을, 「모형도 확인합니다」는 모형 시선 추적을 요구하지 않습니다.
- **각 카드의 「개발 판정 상세」는 제작자용입니다.** 태블릿에는 **본문만** 노출합니다.
- 의도적 미관찰에 **강제 시선·체류 즉사를 추가하지 마십시오.**


### 1A.8 판정 입력 공통 정의·구현 주의

#### 판정 입력의 공통 정의 — 기획서 §2 그대로

| 입력 | 기준 |
|---|---|
| **문 E** | **명령 수락**과 **닫힘 완료**를 구분. 사거리 밖 무시된 입력은 조작이 아님. 조작 출처를 플레이어/연출로 구분. **T3만 닫힘 완료를 요구**, 나머지 조작 금지는 수락 시점에 실패 |
| **응시** | 카메라 중앙 레이의 **첫 가시 충돌체**를 대상 ID로. 벽·닫힌 문 뒤 대상을 관통해 세지 않음. 대상이 바뀌거나 가려지면 **연속 시간 0** |
| **시각 단서 식별** | 안전 관찰 지점에서 대상을 중앙으로 **0.2초** 확인 (시험값). 모형 관찰 1초와 구분. H1·T1은 **실제 자동 움직임 중**에 확인해야 함 |
| **청각 단서 전달** | 지정 청취 영역 안 + 해당 이벤트 ID 음원 정상 재생 = 전달. **사용자가 실제로 들었는지·OS 음소거를 추측하지 않음** |
| **근접** | 플레이어 **발밑 기준점**과 등록된 **바닥 기준점**의 **수평 거리**. 「1.5m 미만」이 금지, **정확히 경계는 바깥** |
| **공간 경계** | 문 상태가 아닌 **발밑 기준점**의 진입·이탈. **화장실 칸 밖은 여전히 화장실 실내.** 경계 위는 직전 공간 유지 |
| **일반 점검 완료** | 기존 신호가 있으면 연결, 없으면 **지정 점검 구역 1초 체류 후 공간 이탈**. 복도 단순 통행은 완료가 아님 |
| **Tab (태블릿)** | **이동·문/손전등 조작·게임 시계·판정 시간·음원/문 동작을 함께 정지.** 닫으면 이어서 재개. 대기 중 신규 사건은 시작하지 않음 |

**시간 상수:** 관찰 1초 · H2/C3 금지 응시 3초 · S4 금지 응시 2초 · 식별 0.2초 ·
손전등 유예 2초 · T2 물 내림 시퀀스 12초 · 근접 1.5m (H6만 2m)

#### 응시 판정 구현 ⚠️

```
누적 조건 : 카메라 중앙 레이 첫 가시 충돌체 == 대상 ID
이탈 시   : 연속 시간 0 (기획서 명시)
해상도    : 0.1초. Update가 아닌 고정 간격 누적 (프레임레이트 독립)
```

**단순 화면 중앙 단일 레이캐스트로 만들면 안 됩니다.** 「고개를 돌린 각도·짧게 본 행동은
판정하지 않는다」(H2), 「짧은 시선 확인을 위반으로 세지 않는다」(C3)를 만족하려면
**중앙 판정 원뿔에 여유가 필요**합니다. 각도 임계 약 12°를 권합니다.

> S4는 식별 0.2초를 금지 응시 시간에 **포함하지 않습니다.** 식별과 위반 타이머를 분리하십시오.

#### 시야 밖 이동·스왑 ⚠️

```
GeometryUtility.TestPlanesAABB(카메라 프러스텀)
  AND Physics.Linecast occlusion
  AND N프레임 연속 비가시
```

**`OnBecameInvisible()`을 쓰지 마십시오.** 씬 카메라·그림자 카메라에도 반응해서
에디터에서만 오작동합니다. 원인 추적이 매우 어렵습니다.

**인체모형은 시야 밖에 있을 때만 위치가 바뀌는 유일한 존재**이고, 8개 장면이 전부 이
컴포넌트를 씁니다. 「준비 위치를 보고 있으면 대기」도 여기서 처리합니다.

> **이 게임에 추격 AI는 없습니다.** 전부 시야 밖 준비·배치입니다.

#### 오디오

- **「정확히 세 번」(C1)은 세 획 전체를 전달**해야 활성. 반드시 **단발 클립**으로 발주.
  루프로 받으면 횟수를 셀 수 없어 해당 카드가 판정 불능이 됩니다.
- **분위기 효과음은 카드 단서 ID를 보내지 않습니다.** 모형 효과음도 마찬가지 (1A.5).
- T2의 12초 시퀀스는 **판정 데이터와 클립 길이를 동일하게** 사용.
- 볼륨·감쇠·마스킹은 플레이테스트 항목.


## 1B. 확정 규약 — 되돌리지 말 것

| # | 규약 |
|---|---|
| 1 | **카드 상태 5종** — 대기 / 진행 중 / 준수·위반 / 미판정 / 종료 잠금. 미시작을 성공으로 계산하지 않는다 |
| 2 | **미판정은 델타 0.** 카드 미배정 · 단서 미발생 · 대상 참조 누락 · 실제 선택 불가능이 모두 미판정 |
| 3 | **소리가 났다는 사실만으로 임의의 카드를 채점하지 않는다.** 이상현상 발생 ≠ 카드 활성 |
| 4 | **진행 중인 사건은 대상·구간·연출을 고정.** 델타로 구간이 바뀌면 관련 월드 변화는 사건 종료 이후 안전한 시점에 적용. **단 100 도달은 지연하지 않는다** |
| 5 | **한 공간 방문에서 신규 단기 판단 사건은 하나만.** 여러 후보는 그날 덱 순서의 첫 유효 카드 우선. 이미 켜진 장기 의무는 계속 감시 |
| 6 | **동시 성립 시 실패 우선.** 같은 시각 여러 카드는 그날 덱 표시 순서로 처리. T2의 정확히 12초 퇴실은 실패 |
| 7 | **이미 정산한 결과는 되돌리지 않는다.** 준수를 취소하거나 두 번째로 채점하지 않는다 |
| 8 | **밤 종료 순서** — 종료 요청은 필수 점검과 오늘 조우의 관찰·방문 종료가 완료되어야 수락. 수락 후 남은 수칙을 **덱 순서로** 정산 → **P형 미도달 정산** → 다음 날 |
| 9 | **저장 시** 진행 중 상태·남은 시간·적용한 델타·문자 노출·조우 예약을 함께 복원. **로드 직후 접촉 중인 트리거를 신규 진입으로 세지 않는다.** 저장 기능이 없다면 새로 만들지 않는다 |
| 10 | 개발 로그와 근무 종료 리뷰에 **어느 카드·문자의 결과인지 분리 저장** |

#### 데이터 스키마 — 기획서 제시안

```
수칙 데이터   cardId, playerText, eligibleBand, triggerId, targetIds,
             successCondition, failureCondition, settleAt,
             successDelta, failureAxis, failureDelta,
             radius, graceSeconds, gazeSeconds        // 안 쓰는 값은 비워 둠

하룻밤 기록   assignedCardIds + 덱 순서, 카드별 상태/시작 시각/정산 여부,
             전달한 단서, 공간별 점검 완료, 문별 수동 닫기 의무,
             활성 장기 금지, 오늘 조우와 관찰·퇴실 상태,
             발송/노출/도착 문자 ID

회차 기록     누적 4축, 최초 S-A 관찰 여부, 섞어 둔 조우 후보 순서,
             이미 관찰한 장면, 직전 조우 공간, 사용한 N/P ID와 P 횟수,
             최초 게임오버 원인
             ※ 다음 날에 축·조우 소비 이력을 초기화하지 않는다

씬 참조       문·모형·책상·실험대·세면대·라이트 그룹, 관찰 대상,
             청취 구역, 공간/점검/통행/칸 내부 영역
             ※ 모든 카드에 대상 참조가 연결됐는지 시작 전 검사

기존 이벤트   문 명령 수락 / 닫힘 완료, 공간 진입·이탈, 점검 완료,
             단서 재생·식별, 모형 관찰, Tab 상태, 손전등 상태, 밤 종료 요청
```

> **이름은 예시이며 기존 클래스·프리팹 이름이 아닙니다.** 팀의 현재 데이터 구조에 연결하십시오.

## 2. 폴더·어셈블리 구성

| 위치 | 내용 |
|---|---|
| `Assets/_Game/Scripts/Core` | `NightDuty.Core` asmdef(참조 없음, Tests·Editor에 InternalsVisibleTo). Bands, Vocabulary, EventBus, DebugAxisDriver |
| `Assets/_Game/Scripts/Rules` | JudgeSignal, RuleBook, RuleWatcher, JudgeWorld, CardState, RuleReferenceCheck, `Conditions/` |
| `Assets/_Game/Scripts/Data` | RuleSO, NightDeckTableSO, BandTableSO, SpaceAnomalyTableSO |
| `Assets/_Game/Scripts/Direction` | NightRun, JudgeTarget, JudgeTargetRegistry, CardScenarios |
| `Assets/_Game/Scripts/Stats` | FearAxisSystem, BandResolver, DaySummary |
| `Assets/_Game/Scripts/Editor` | `NightDuty.Editor`. 카드·표 빌더, 검사기, 드로어, 테스트 씬 도구 |
| `Assets/_Game/Tests/EditMode` | `NightDuty.Tests.EditMode` — **272개** |
| `Assets/_Game/ScriptableObjects` | `Rules/{Corridor,Classroom,Science,Toilet}` 24장, SpaceAnomalyTable, BandTable |
| `Assets/_Game/Resources/NightDeckTable.asset` | 임시 편성: 1일 복도 · 2일 교실 · 3일 과학실 · 4일 화장실 |
| `Assets/3.1. Programmer_lee/02 Scripts` | AxisTestLightRig, AxisTestAnomalyRig, NightRunDebugPanel (Assembly-CSharp) |
| `Assets/3.2 Programmer_Kim` | 진선님 GameFlow·Result·HUD 등 (Assembly-CSharp) |
| `Docs/` | 현재 비어 있음(`Claude outputs/`만 존재) |

---

## 3. 핵심 코드 구조

```
클라이언트 센서/문/재생기 ──JudgeSignal──▶ NightRun.Send
                                            │
NightRun (정적 회차 창구) ──▶ RuleBook (카드 배분·방문 몫·보류)
                                  └▶ RuleWatcher ×카드  (Waiting/Active/Complied/Violated/Undetermined/Locked)
                                        └ ICondition(무상태) + ConditionState(ms 타이머·Latched·Child·Ids)
                                  └▶ FearAxisSystem.Apply(axis, delta, sourceId, space)  → 100이면 LockAll
                                  └▶ BandResolver (보류·히스테리시스) → EventBus.BandChanged/BandProgress
NightRun.RequestEndNight ─▶ EndNight(진행 카드에 NightEndAccepted → 밤 종료 정산) ─▶ DaySummary ─▶ EventBus.DayEnded
포획 시 EventBus.AxisCritical(axis) 1회
```

- **NightRun API:** `StartNewRun()`, `BeginNight(day, Func<int> clockMinutes)`(내부에서 `_book.BeginNight()`), `Tick(sec)`, `Send(in JudgeSignal)`, `RequestEndNight()`(현재 항상 수락), `BuildSummary()`, `Day`, `Axes`, `IsCaptured`, `Cause`, 디버그용 `DebugForceCapture`, `DebugAddAxis`, `DebugRebroadcast`, `RegisteredTargets`, `TargetsInUse`, `DeckOverride`.
- **대상 검사 순서:** `RegisteredTargets`(복사) → 없으면 `JudgeTargetRegistry` 스냅숏(비어 있지 않을 때) → 둘 다 없으면 검사 생략. 등록 안 된 ID를 쓰는 카드는 미판정 + 경고.
- **조건:** SignalCondition(`UsesTarget` public), GazeCondition(target, seconds, grace), FlashlightCondition(whenOn, grace), ProximityCondition(anchor, radius), AllOf/AnyOf/Elapsed, **BeforeCondition(happened, guard)**, **DoorObligationCondition(doorId, checkAt)**. 대상 특수값 `""`(카드 대상 목록)·`*`·`@trigger`.
- **코어 확장(이번 세션):** `SignalKind.NightBegan = 71`(밤 시작 장기 카드, C6) · EndNight가 진행 카드에 `NightEndAccepted` 먼저 전달(Tab 무관, 외부 전송분은 무시) · 준수 전용 카드(Failure=null, FailureDelta=0, S1) · NightBegan 카드는 장기·자격 구간 없음·취소 없음 검사 · 잠금 시 보류 해제 · `RuleBook.Abandon()` · `NightDeckTableSO.RawCardsOf(day)`.
- **DaySummary:** 기존 11인자 생성자 유지 + `Outcome`, `Cause`, `ViolationMinutes`, `Results`, `FormatMinutes`. `ImprintAxis`·`Conflicts*`는 폐기 예정(null/0).
- **JudgeTarget:** MonoBehaviour, `_ids[]`, `PrimaryId`, `IdOf(Component)`, `SetIds`, OnEnable 등록. Registry는 ID별 개수·소유자 목록, SubsystemRegistration에서 초기화.
- **CardScenarios:** 24장 × 지키기/어기기 행동 정의. `CardScenarioTests` 49개가 실제 에셋으로 기대 결과 확인.

---

## 4. 진행 상황 (2026-09-17 병합 후 검증: 에디터 ready · 컴파일 에러 0 · 빌드 씬 7개 · EditMode 264/264 → 신뢰 변경 후 272/272)

### 완료·병합됨 (Lee)
- 판정 코어 1차 + NightRun·DaySummary 확장 + `@trigger` + H3 방문 몫 반환.
- 복도 H1~H6 (`CorridorCardBuilder`, 메뉴 「복도 카드 에셋 생성」).
- 교실·과학실·화장실 18장 (`RoomCardBuilder`, 메뉴 「모든 공간 카드 에셋 생성」 — 없는 카드만 생성, 편성표 빈 2~4일만 채움).
- `SpaceAnomalyTableSO`(60칸) + 메뉴 「이상현상 표 에셋 생성」, `AxisTestAnomalyRig`(합성음·임시 소품, F1).
- `JudgeTarget`/`Registry` + 메뉴 `NightDuty ▸ 씬 대상 검사 (열린 씬 × 편성표)`(대상 0개면 조용히 종료).
- **판정 디버그 패널** `NightRunDebugPanel` (`_Test_AxisRig` 플레이 시 자동 생성, F2 숨김)
  - ① 카드 시험(기본): 공간 H/C/S/T → 카드별 [지키기 ▶]/[어기기 ▶]/[?]. 새 회차 + 그 카드만 넣은 밤 → 자격 축 세팅 → 행동 재생 → 결과(예: 「[지킴] 준수 · 신뢰 +2」, 예상과 다르면 주의 표시). 「천천히 보기」 토글. 48개 시나리오 플레이 모드 검증 완료.
  - ② 직접 조작: 일차·밤 시작/종료, 축 +/포획, 시간, 응시, 공간, 손전등/Tab/점검, 임의 신호, 카드 목록, 로그.
  - ③ 도움말.
  - 코어 모드 토글 시 DebugAxisDriver 끄고 `DebugRebroadcast`로 조명·이상현상 리그에 코어 구간 전달.
- **신뢰 비포획화(2026-09-17):** `FearAxisSystem.IsTerminal(axis)` 추가 — 청각·조도·배치만 100에서 종료 잠금, 신뢰는 100에서 멈춤(Restore도 동일). 판정 패널 「포획 시험」에서 신뢰 버튼 제거. 테스트: 신뢰100 비포획·신뢰100 뒤 감각축 포획·복원·포획축 목록, 정산순서 테스트를 신뢰(계속 정산)/배치88(잠금 후 중단)로 분리. 진선님 `FakeDayData`의 `criticalAxis == Trust` 분기는 연결 작업 때 정리.
### 병합된 진선님 작업
- SceneFlow(Main → Loading → testScene → Result, 빌드 목록 확인), SceneFlowConfig, GameTime(ShiftEnded·SetRunning·SkipToEnd), GameSession(FinalDay 5), DayResult, DayIntro, ResultController(`summary.ImprintAxis`, `AxisValue` 사용), AxisBarView·DutyLogView·ViolationLogView, HUDActions, PlaySystems.prefab.
- `PlayResultRouter`: ShiftEnded → `FakeDayData`, DayEnded → DayResult(위반 시각 비어 있음·가짜 로그), AxisCritical → 가짜 데이터, `SceneFlow.GoTo(GameScene.Result, false)`.
- **진선님 코드는 아직 NightRun을 호출하지 않는다.**

### 아직 없음 / 미연결
- NightRun ↔ 게임 흐름 연결 (§7의 1번).
- 실제 씬의 `JudgeTarget` 배치, 플레이어 센서(응시·근접·구역·공간·문·손전등·Tab) — **담당 미정**.
- DayDirector(편성 규칙: S1 1일차 고정, T1·T3 같은 날 금지, S2 활성 중 S-B 금지), EncounterDirector(조우 8장면), MessageDirector/ParadoxResolver(P1~P4).
- SpaceAnomalyTable을 읽는 실제 공간 연출(`ISpacePresenter`).
- 면제 API(`RuleBook.Waive` — C6 동선 봉쇄, S4 대상 소실).
- `RequestEndNight` 수락 조건(필수 점검 + 오늘 조우 완료).

---

## 5. 연결 약속 요약 (Lee ↔ Kim, 합의 대기)

### 5.1 호출 흐름
- 메인 시작 → `NightRun.StartNewRun()`
- Play 시작 → `NightRun.BeginNight(GameSession.CurrentDay, () => 현재 게임 분)`
- 매 프레임(Tab·일시정지 아닐 때) → `NightRun.Tick(Time.deltaTime)`
- `GameTime.ShiftEnded` → `NightRun.RequestEndNight()` → `EventBus.DayEnded(DaySummary)` → 결과 저장 → Result 씬
- `EventBus.AxisCritical` → `NightRun.BuildSummary()`로 사망 결과
- 연출은 `EventBus.BandChanged/BandProgress` 구독(여러 번 받아도 같은 결과, `from==to` 기준 방송 옴, OnDisable 해제).

### 5.2 신호 규칙
- 응시·근접 샘플은 **0.1초 고정 간격**, 응시는 **대상 없어도 빈 ID로** 보냄. 근접 값은 수평 거리(m).
- 판정 시간은 `NightRun.Tick`로만. `JudgeSignal.Tick`을 Send로 보내지 않음.
- Tab 중에는 신호를 보내지 않고 `JudgeSignal.Tab(bool)`만 보냄. Tab 중 단서 음원도 정지.
- 출처 구분: 플레이어 `ActionSource.Player`, 연출 `ActionSource.Direction`.
- 모형·분위기 효과음은 단서 신호 아님.
- **같은 순간 순서:** `Tick` → `PassageCompleted` → `ZoneExited` → `InspectionCompleted` → `SpaceExited`.
- `NightBegan`·`NightEndAccepted`는 보내지 않음(NightRun이 생성).
- `InspectionCompleted` = 점검 구역 1초 체류 후 공간 이탈 시(이탈보다 먼저).
- `ClueDelivered` = 카드 사건 시작 시점: 일반은 클립 정상 재생 완료, **C1 세 번째 획 끝, C4 두 음원 겹침 시작, T2 물 내림 시작**.
- `ClueIdentified` = 안전 관찰 지점에서 0.2초 중앙 확인. `ModelObserved` = 가림 없이 1초. `DoorAutoOpenObserved` = 자동 개방 동작 중 0.2초 식별.
- 응시 대상 = 카메라 중앙 첫 가시 충돌체(`JudgeTarget.IdOf(hit.collider)`). `OnBecameInvisible` 사용 금지.

### 5.3 보내기 전 조건 (코어가 모름)
| 카드 | 조건 |
|---|---|
| C1 | 그날 1-3 점검 전, 1-1 문밖, 하루 1회 |
| C2 | 회전 좌석 실제 존재(배치 25+ 또는 C-A), 반경 밖 |
| C3 | 점검 체류 후 퇴실 준비 구역 |
| C4 | 교탁 반경 밖 |
| C5 | 실제 켜진 등 ≤ 4 |
| S2 | S1 완료·조우 선정·과학실 점검 후 퇴실·미완료 조우 없음 |
| S3 | 그 방문에 간격 식별 선행, 반경 밖 |
| S4 | 실제 켜진 등 = 1 |
| T2 | 12초 안 출구 도달 가능 |
| T4 | 세면대 반경 밖 |
| T5 | 닫힌 칸 아래 빛 실제 존재 |
| T6 | 두 칸 실제 개방, T1 활성 날 아님 |

모형·역설 방문 억제: C-A(C3·C4·C5) · C-B(C3·C4·C5) · 일반 S-B(S2·S4·S6) · P1 경유 S-B(장면 ID `scene.sb.p1`, S4·S6) · T-A(T2·T4·T5·T6) · T-B(T2·T4·T5).

### 5.4 대상 ID (소문자·숫자·점, 카드 데이터와 글자까지 동일)
```
corridor.door.11(1-1 문, 신규) · corridor.door.13 · corridor.box · corridor.passage · corridor.door.auto
cls11.chalk3 · cls11.desk.turned · cls11.desk.back · cls11.lectern · cls11.lectern.noise · cls11.lights · cls13.lights
science.model.sa.face · science.shelf.sa · science.glass.break · science.door · science.bench.glass · science.bench.glass.clink
science.light.last · science.model.sb · science.zone.glass
toilet.door · toilet.stall.outer · toilet.stall.inner · toilet.stall.outer.inside · toilet.stall.inner.inside
toilet.flush · toilet.sink · toilet.sink.call · toilet.stall.light · toilet.stalls.bothopen
scene.ha(복도 장면 형식) · scene.ca · scene.cb · scene.sa · scene.sb · scene.sb.p1 · scene.ta · scene.tb
```

### 5.5 진선님 쪽에서 고칠 것
- `FakeDayData`·`ImprintAxis` 경로 제거, 결과창은 `ViolationMinutes`(시각만) 표시. 카드 ID 표시 금지.
- 「충돌 처리」 표시 숨김(Q3), 로딩 팁의 §0 문구 정리.
- 일차가 바뀌어도 축 유지(NightRun이 보관).

---

## 6. 카드 요약 (구현값)

| 카드 | 단/장 | 준수 | 위반 | 자격 | 시작 신호 | 메모 |
|---|---|---|---|---|---|---|
| C1 | **장기** | 신뢰+2 | 청각+12 | 청각 B0~1 | ClueDelivered `cls11.chalk3` | 1-1 진입=위반, 1-3 점검=준수 |
| C2 | 단 | +2 | 배치+12 | 없음(단서 존재로 대신) | ClueIdentified | 근접 1.5m |
| C3 | 단 | +2 | 청각+15 | 청각 B2~4 | ClueDelivered | 유예2·응시3 |
| C4 | 단 | +2 | 청각+15 | 청각 B3~4 | ClueDelivered(겹침 시작) | 교탁 근접 |
| C5 | 단 | +3 | 조도+12 | 조도 B2~4 | ClueIdentified | 손전등 Off=위반, 대표 공간 1-1 |
| C6 | 장기 | +3 | 배치+12 | 없음 | **NightBegan** | 두 교실 점검 + 직접 연 문 닫기 |
| S1 | 단 | +3 | **없음** | 없음(1일차) | SpaceEntered | 얼굴 1초 응시, 준수 전용 |
| S2 | 장기 | +2 | 청각+12 | 없음 | ClueDelivered | 재진입 금지, 준수는 NightEndAccepted |
| S3 | 단 | +2 | 청각+12 | 청각 B1~4 | ClueDelivered | 근접 |
| S4 | 단 | +2 | 조도+12 | 조도 B3 | ClueIdentified | 응시 2초 |
| S5 | 단 | +3 | 배치+15 | 없음 | ModelObserved `scene.sb` | 근접, 퇴실=준수 |
| S6 | 단 | +2 | 조도+12 | 없음 | ZoneEntered | 손전등 On=위반, 체류 3초+이탈 |
| T1 | 장기 | +2 | 배치+12 | 배치 B0~2 | DoorAutoOpenObserved | 칸 문 조작 금지, 준수는 NightEndAccepted |
| T2 | 단 | +3 | 청각+15 | 청각 B1~4 | ClueDelivered(물 내림 시작) | 12초 안 퇴실 |
| T3 | 단 | +3 | 배치+15 | 없음 | ModelObserved `scene.ta` | 닫힘 완료 전 퇴실/재개방=위반(Before) |
| T4 | 단 | +2 | 청각+12 | 청각 B2~4 | ClueDelivered | 세면대 근접 |
| T5 | 단 | +2 | 조도+12 | 조도 B2~4 | ClueIdentified | 손전등 On=위반 |
| T6 | 장기 | +2 | 배치+15 | 없음(단서 존재로 대신) | ClueIdentified | 칸 내부 진입=위반 |
| H1~H6 | — | — | — | — | — | 복도. H6 위반은 포획까지 이어짐(패널 표시는 클램프된 값) |

---

## 7. 다음 작업 (우선순위)

1. **NightRun ↔ 진선님 흐름 연결** (§5.1). 결과창을 실제 `DaySummary`로, `ImprintAxis` 사용 제거, 위반 시각 포맷.
2. 플레이어 센서 담당 확정 → 실제 씬에 `JudgeTarget` 배치(LFS 잠금 확인) → `씬 대상 검사`.
3. SpaceAnomalyTable을 읽는 실제 공간 연출.
4. DayDirector · EncounterDirector · MessageDirector/ParadoxResolver(P1~P4).
5. 면제 API(`Waive`)·`RequestEndNight` 수락 조건.
6. (선택) 아키텍처 페이지 갱신 — https://claude.ai/artifact/5qgPXsVt7thj32VdMHFgPu (v3, 패널 개편 미반영). H6 표시 문구 「+25 (100에서 멈춤)」.

## 8. 결정 대기

| # | 질문 | 누구 |
|---|---|---|
| Q1 | 근무 일수(조우 8장면 연동) | 기획 |
| Q2 | 근무 종료 방식(시계 자동 / 종료 요청) | 기획 |
| Q3 | 결과창 역설 문자 결과 표시 여부 | 기획 |
| Q4 | 포획 엔딩 연출(청각·조도·배치 — 신뢰는 포획 없음) | 기획 |
| Q10 | 신뢰 → 충돌 확장 규칙: 구간별 역설 발송 한도, 추가 충돌 문자 목록(짝 카드·목적지·결과·충돌표), 구간 판정 시점, 짝 카드 편성 보강 여부 | 기획 |
| Q5 | 응시 여유 각도(권고 약 12°) | 기획·진선 |
| Q6 | Tab이 `timeScale = 0`인가 | 진선 |
| Q8 | GameFlow 코드를 `_Game`으로 옮길지 | 진선 |
| Q9 | H3가 통행 구역 진입 시 방문 몫을 차지하는 것이 의도인가 | 기획 |
| 카드 | C1 장기 유지 / C2·T6 「또는」 자격을 단서 존재로 대신 / C6 1-1 문 ID(H1 자동 문과 같은가)·봉쇄 면제 / T6 준수는 시작 뒤 점검만 인정 / S3·S4·T5 「점검 없이 퇴실=대기」 해석 / C5 보류가 1-1에 걸림 | 기획 |
| 팀 | `com.unity.pipeline` 커밋 여부(현재 stash 복구본, 미커밋) | Lee·진선 |

## 9. 발견된 함정

### 코드

1. **C# 9만 가능.** Unity 6000.x는 C# 9입니다. **file-scoped namespace(`namespace X;`) 금지, record 금지, global using 금지.** 최신 문법에 익숙할수록 무심코 씁니다.
2. **`NightDuty.Core`는 연출을 참조하면 안 됩니다.** asmdef가 컴파일 단계에서 막습니다 — **참조를 추가해 우회하지 마십시오.**
3. **`Assets/NOT_Lonely/` 스크립트를 참조하지 마십시오.** (a) 벤더 스크립트는 asmdef가 없어 `Assembly-CSharp`에 들어가고, asmdef 어셈블리는 그걸 참조할 수 없습니다. (b) 폴더가 gitignore라 패키지 미설치 팀원의 빌드가 깨집니다. 프리팹·머티리얼은 씬에서 참조 가능, **스크립트만 불가.**
4. **`from == to`인 `BandChanged`가 옵니다.** 「전체 다시 방송」과 `OnEnable` 기준값 송출 때문입니다. **연출은 멱등해야 합니다.**
5. **`OnDisable`에서 `EventBus` 구독을 반드시 해제.** static이라 씬을 바꿔도 살아 있어서 파괴된 오브젝트로 이벤트가 날아갑니다.
6. **`EventBus.ClearAll()` + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`은 죽은 코드가 아닙니다.** 도메인 리로드를 끄면 구독이 살아남아 이벤트가 중복 발화합니다. **지우지 마십시오.**
7. **조명 intensity에 배수를 곱할 땐 원본을 따로 기억해야 합니다.** 현재 값에 곱하면 매 프레임 지수적으로 어두워집니다.
8. **`AddComponent` 직후 `OnEnable`이 즉시 실행됩니다.** `SerializedObject`로 필드를 채우기 **전**이라, 초기화를 `OnEnable`에 의존하면 빈 값으로 돕니다.
9. `Editor` 어셈블리는 `Assembly-CSharp`을 참조할 수 없습니다. 개인 폴더 타입이 필요하면 `Type.GetType("이름, Assembly-CSharp")`.

### Unity / 에셋

10. **벤더 셰이더 필수 수정 (각 PC마다, 커밋 불가):**
    `Assets/NOT_Lonely/HQ_AbandonedSchool/Shaders/NOT_Lonely_LightRays.shader` 에서
    `uniform float4 _CameraDepthTexture_TexelSize;` **두 줄 삭제**(원래 224·507행).
    URP 17.3이 자체 선언해 redefinition 에러가 납니다. 변수는 쓰이지 않으므로 기능 변화 0.
    → **증상: `redefinition of '_CameraDepthTexture_TexelSize'`**
11. **벤더 데모씬의 조명은 전부 Area Light = 베이크 전용.** 실시간으로 껐다 켜도 화면에 반영되지 않습니다. 조도 축 확인에는 **Point/Spot + Realtime**이 필요합니다.
12. **라이팅은 Realtime.** Mixed는 간접광이 남아 Band4의 「전부 소등」에서 방이 은은하게 밝습니다.
13. **URP Forward+ 확인됨** (`m_RenderingMode: 2`). per-object 조명 제한 없음. 단 Additional Light Shadow 아틀라스가 2048이라 **등 8개가 전부 그림자를 던지면 부족**합니다.
14. `DemoScene_LeeTest.unity`가 **19MB**입니다. **부품 창고로만 쓰고 절대 커밋하지 마십시오.**
15. Scene 뷰가 초록색이면 조명 토글이 아니라 **디버그 드로우 모드가 `Contributors / Receivers`**로 켜진 것입니다. 해제법을 못 찾았습니다 — **Game 뷰를 쓰면 됩니다**(에디트 모드에서도 렌더).

### git

16. `.unity` / `.prefab`은 **LFS lock 대상**. 편집 전 `git lfs locks` → `lock` → push 후 `unlock`.
17. `Assets/NOT_Lonely/`는 gitignore. **Git 클라이언트의 "Discard All"이 이 폴더를 통째로 삭제합니다.**
18. 커밋 프리픽스: `feat:` `fix:` `art:` `chore:` `docs:`


## 10. 작업 환경 메모

- **Unity MCP:** `com.unity.pipeline 0.7.0-exp.1`이 `Packages/manifest.json`에 있어야 함. 병합 때 빠질 수 있음 → stash 복구. MCP 서버가 에디터보다 먼저 켜지면 도구 0개 → 에디터 띄운 뒤 앱 재시작.
- 에디터가 뒤에 있으면 패키지 해석·플레이 틱이 멈춤 → `editor_focus`.
- 파일 작성 후 `Assets/Refresh` → 25~30초 뒤 `recompile_status`/`console_status`.
- 플레이 중 `set_component_properties` 불가 → `eval`. `FindAnyObjectByType`는 DontSave 오브젝트를 못 찾음.
- 확인 대화상자가 뜨는 메뉴를 MCP로 실행하면 에디터가 멈춤.
- `run_tests` 결과가 크면 파일로 저장됨 → 요약 grep.
- 병합 후 빌드 씬 목록이 옛것이면 Unity 재시작(재시작 전 저장하지 말 것).
- **기기 파일 전송:** `device_bash` 없음. 기기로 올릴 때 매번 **새 `/mnt/user-data/outputs/codeN` 폴더**에 스테이징(같은 경로 재사용 시 옛 내용이 올라감), `expectedMtimeMs` 사용, 올린 뒤 크기 확인.
- IMGUI: 버튼 동작은 `Later(...)` 큐 → Update 실행, GUI.matrix는 finally에서 복구, 지원 안 되는 기호(✔✖⚠■) 쓰지 않기, F1/F2는 `#if ENABLE_LEGACY_INPUT_MANAGER`.

- **Unity 없이 컴파일 확인이 필요할 때:** `mono-mcs` + UnityEngine 최소 스텁, `-langversion:7.2`, 심볼 3구성(`UNITY_EDITOR`/없음/`NIGHTDUTY_DEBUG`). 스텁 누락 에러는 코드 문제가 아님. 지금은 Unity MCP로 직접 컴파일·테스트하는 것이 기본.
- **서브 에이전트:** 공유 타입 시그니처를 프롬프트에 그대로 넣기. 기기·Unity 조작과 결과 반영은 메인 세션만.
- **사용자 선호:** 커밋 메시지는 Summary(50자 내외) + Description, 파일 목록보다 이유와 실수하기 쉬운 점 위주. 문서는 `Docs/Claude outputs/`에 markdown. 맡긴 일은 끝까지.

## 11. 첫 세션 시작 절차

1. 이 문서 통독 → `horror house/CLAUDE.md` 확인(폐기 항목이 남아 있을 수 있음 — §1.1이 우선).
2. Unity MCP 연결 확인: `editor_status` → `console_status`(에러 0) → EditMode 테스트(272개 통과 기준).
3. git 상태는 **사용자에게 묻는다**(Claude는 git 명령 금지).
4. §7의 1번(NightRun ↔ 게임 흐름 연결)부터 사용자 확인 후 착수.

## 12. 참고

- 아키텍처 페이지(현행): https://claude.ai/artifact/5qgPXsVt7thj32VdMHFgPu (v3, 패널 개편 전)
- 옛 아키텍처 페이지(기획서 확정 이전): https://claude.ai/artifact/HzdAHPQN8uiRGeNEkZcLLJ · https://claude.ai/artifact/6Za2wDCM4eFHNzFfz7wUL5
- claude.ai 프로젝트 「varco 3d ai 프로젝트」에 이 문서 사본(`claude/인수인계_야간근무.md`)이 있다.

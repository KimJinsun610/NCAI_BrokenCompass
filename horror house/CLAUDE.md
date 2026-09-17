# CLAUDE.md

이 파일은 이 저장소에서 코드를 다루는 Claude 세션을 위한 안내서입니다.
**답변·문서·코드 주석은 모두 한국어로 작성합니다.**

> 개정: 2026-09-17. 확정 기획서(2026-09-12 판정 정리)와 인수인계서(`HANDOFF_야간근무_인수인계`)를 반영해 전면 재작성했습니다.
> 이전 판에 있던 `RED_THRESHOLD = 75`, 미방문 페널티 배치 +22, `ComplianceMode` 3종, 과학실 붉은 잔광 유지는 **모두 폐기**되었습니다.

---

## 1. 프로젝트

**야간근무 (Night Duty).** 폐교를 배경으로 한 1인칭 **규칙 준수 공포게임**입니다. 나폴리탄 괴담을 참조합니다.
플레이어는 야간 경비로서 태블릿에 실리는 근무수칙을 지키며 4개 공간을 순찰합니다.
**수칙을 지키면 신뢰가 오르고, 어기면 감각축이 오릅니다. 어느 축이든 100에 도달하면 포획 엔딩(게임오버)입니다.**

- 엔진: Unity **6000.3.17f1** · URP **17.3** (Forward+) · New Input System 1.19
- 인원: 2인 개발 (시스템 / 클라이언트, §10)
- 이 `horror house/` 폴더가 **Unity 프로젝트 루트**이고, git 저장소 루트는 한 단계 위(`NCAI_BrokenCompass/`)입니다.
  저장소 루트에도 `Assets/` 폴더가 있지만 Unity 프로젝트가 아닙니다. 루트를 Unity로 열지 마십시오.
- 빌드·린트 도구는 없습니다. 검증은 Unity 에디터에서 합니다(§3).

### 1.1 문서 우선순위 — 충돌하면 위가 이깁니다

| 순위 | 문서 | 위치 | 역할 |
|---|---|---|---|
| 1 | **`야간근무_공간별_지침록_개발명세반영본.html`** | 사용자에게 요청 | **판정의 정본.** 24수칙 전문, 카드별 「개발 판정 상세」, 공통 개발 명세 1~7절 |
| 2 | `HANDOFF_야간근무_인수인계 (3).md` | `../Docs/Claude outputs/` | 내려진 결정, 발견된 함정, 남은 작업 목록 |
| 3 | 이 파일 | `horror house/CLAUDE.md` | 작업 규약 요약 |
| 4 | `형상관리_매뉴얼.md` | `../Docs/` | git/LFS 운영 규칙 |

- **수칙 전문·델타·판정 상세를 이 파일이나 코드 주석에 옮겨 적지 마십시오.** 사본이 어긋나면 어느 쪽이 맞는지 알 수 없게 됩니다. 카드를 구현할 때는 정본의 해당 카드 7항목(발생 자격·시작 / 준수 / 위반 / 종료·반복 / 예외·충돌 / 씬 연결·설정값 / 검증 절차·기대 결과)을 직접 읽으십시오.
- `../Docs/Claude outputs/`의 다음 문서는 **구버전 기준(3공간·30장·§0 조항)** 입니다. 설계 원리만 참고하고 스코프·수치는 따르지 마십시오:
  `4주_압축_스케줄.md`(무효), `8주_스코프_절단_제안서[구버전].md`, `절단_확정_계획서_v2_기획팀용.md`, `근무수칙_구현난이도_판정서.md`,
  `시스템파트_개발계획서_ProgrammerLee.md`, `클라이언트파트_개발계획서_ProgrammerKim.md`(8주 기준, 설계 원리는 유효).
- `NightDuty.Core_현황과_사용법.md`는 진선님 전달용 Core 사용법(`CorridorPresenter` 예제 포함)입니다. 인수인계서 §0.1의 폐기 항목과 겹치는 부분은 정본을 따르십시오.
- 이미 내려진 결정은 다시 논의하지 않습니다. 바꿔야 한다면 **사용자에게 먼저 확인**받으십시오.

---

## 2. 확정 게임 규칙 요약

### 2.1 스코프

| 항목 | 확정값 |
|---|---|
| 공간 | **4개**: 복도 · 교실(1-1/1-3) · 과학실 · 화장실 |
| 점검 ID | **5개**: 복도 · 1-1 · 1-3 · 과학실 · 화장실 (`SpaceId`와 일치) |
| 근무수칙 | **24장**, 공간당 6장: 복도 **H1~H6** · 교실 **C1~C6** · 과학실 **S1~S6** · 화장실 **T1~T6** |
| 인체모형 장면 | **8개**: H-A · H-B · C-A · C-B · S-A · S-B · T-A · T-B |
| 태블릿 문자 | **12개**: 발견 유도 7(G-H1·G-H2·G-C1·G-C2·G-S1·G-T1·G-T2) + 재방문 1(N1) + **역설 4(P1~P4)** |
| 역설 쌍 | **P1↔S2 · P2↔H4 · P3↔C1 · P4↔T6** |

도서관·탈의실은 이번 스코프에 없습니다.

### 2.2 네 개의 축

| 축 | enum | 오르는 조건 | 델타 |
|---|---|---|---|
| 청각 | `Auditory` | 청각 수칙 **위반** | +12 ~ +15 |
| 조도 | `Illuminance` | 조도 수칙 **위반** | +12 |
| 배치 | `Layout` | 배치 수칙 **위반** | +12 ~ +25 |
| 신뢰 | `Trust` | 수칙 **준수** | +2 ~ +4 |

- 값은 0~100 누적입니다. **감쇠와 상시 증가는 없습니다.** 결과 한 건마다 `새 값 = min(100, 기존 + 델타)`로 계산합니다.
- **어느 축이든 100이면 종료 잠금**입니다(신뢰 100 포함). 종료 원인 축, 카드/문자 ID, 현재 공간을 기록하고 **종료 요청을 한 번만** 보냅니다. 그 뒤의 미적용 델타와 신규 판정·문자·조우·정산은 모두 중단합니다.
- 델타는 카드마다 고정값입니다. 프로파일링 가중치나 각인축은 없습니다.
- 청각·조도·배치는 월드 연출을 움직입니다. **신뢰는 월드에 그리지 않고 수치만 누적합니다.**

### 2.3 구간(Band) — 폭이 균일하지 않습니다

```
Band0 = 0–24 · Band1 = 25–49 · Band2 = 50–74 · Band3 = 75–89 · Band4 = 90–99 · 100 = 종료
```

- **`value / 20` 같은 균등 분할 계산을 하지 마십시오.** 반드시 `Bands.Of(value)`를 씁니다.
- 90~99 표현이 엔딩 직전 경고를 맡습니다. 별도 경고 시스템은 추가하지 않습니다.
- 수칙의 발동 자격은 카드마다 가진 `eligibleBand`로 판단합니다. 공통 「붉게 보이면」 임계값은 없습니다.

### 2.4 조도 — 공통 색온도표, 공간마다 다른 것은 등 개수뿐

| 구간 | 색온도 | 복도 | 교실 | 과학실 | 화장실 |
|---|---|---:|---:|---:|---:|
| Band0 | 6500K 백색 | 8 | 8 | 4 | 4 |
| Band1 | 4500K 옅은 노랑 | 6 | 6 | 3 | 3 |
| Band2 | 3200K 주황 | 4 | 4 | 2 | 2 |
| Band3 | 2000K 적갈 | 2 | 2 | 1 | 1 |
| Band4 | 붉은 잔광 | 0 | 0 | 0 | 0 |

- 이 표는 `BandTableSO`가 한 곳에서 관리합니다. 공간별로 복사하지 마십시오.
- 일부 카드(C5, S4 등)는 수치가 아니라 **실제로 켜진 등 개수**를 조건으로 씁니다. 조도값과 실제 라이트 그룹 상태를 **둘 다** 검사하고, 둘이 어긋나면 사건을 시작하지 않고 개발 로그에 남깁니다.

### 2.5 판정 공통 규약 (정본 공통 명세 1절 요약)

1. **카드 상태는 5종**입니다: 대기 / 진행 중 / 준수·위반 / 미판정 / 종료 잠금. 시작하지 않은 카드를 성공으로 계산하지 않습니다.
2. **미판정은 델타 0**입니다. 카드 미배정, 단서 미발생, 대상 참조 누락, 실제 선택 불가능이 모두 미판정입니다. 참조 누락은 크래시가 아니라 미판정이어야 합니다.
3. **소리가 났다는 사실만으로 카드를 채점하지 않습니다.** 이상현상이 발생했다고 카드가 활성화되는 것은 아닙니다.
4. 위반은 **즉시** 큰 델타 하나를 적용합니다. 준수는 **카드마다 지정한 완료 시점**(`settleAt`: 통행 종료 / 퇴실 / 밤 종료)에만 지급합니다.
5. 준수와 위반은 배타적이며 **같은 카드의 하룻밤 결과는 하나**입니다. 대상 개수나 왕복 횟수로 곱하지 않습니다. **이미 정산한 결과는 되돌리지 않습니다.**
6. **진행 중인 사건은 대상·구간·연출을 고정**합니다. 델타로 구간이 바뀌어도 관련 월드 변화는 사건 종료 뒤 안전한 시점에 적용합니다. **단, 100 도달은 지연하지 않습니다.**
7. **한 공간 방문에서 새로 시작하는 단기 판단 사건은 하나**입니다. 후보가 여럿이면 그날 덱 순서의 첫 유효 카드를 우선합니다. 이미 켜진 장기 의무는 계속 감시합니다.
8. **동시 성립 시 실패가 우선**입니다. 같은 시각 여러 카드가 반응하면 그날 덱 표시 순서로 처리합니다.
9. **밤 종료 순서:** 필수 점검과 오늘 조우의 관찰·방문 종료가 끝나야 종료 요청을 수락합니다 → 남은 수칙을 덱 순서로 정산 → P형 미도달 정산 → 다음 날.
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
| Tab(태블릿) | 열려 있는 동안 **이동, 문·손전등 조작, 게임 시계, 판정 타이머, 관련 음원·문 동작이 모두 멈춥니다.** 닫으면 이어서 재개합니다. 대기 중 신규 사건은 시작하지 않습니다. Tab이 100 종료나 이미 성립한 실패를 취소하지는 않습니다. |

**시간 상수:** 모형 관찰 1초 · H2/C3 금지 응시 3초 · S4 금지 응시 2초 · 식별 0.2초 · 손전등 유예 2초 · T2 물 내림 시퀀스 12초(정확히 12초 퇴실은 실패) · 근접 1.5m(H6만 2m).
수치는 코드에 박지 말고 데이터(SO)에 둡니다.

### 2.7 인체모형과 문자

- **인체모형은 시야 밖에 있을 때만 위치가 바뀌는 유일한 존재**입니다. 이 게임에 추격 AI는 없습니다.
- **S-A만 첫날 과학실에 고정**합니다. 나머지 7개 장면은 회차 시작에 순서를 한 번 섞어 저장합니다. 선정한 장면은 잠그고, 준비 위치를 플레이어가 보고 있으면 대기합니다. **시야 안에서 생성하거나 움직이지 않습니다.**
- 기존 선/앉은 포즈만 씁니다. 새 포즈는 만들지 않습니다.
- **모형 효과음은 카드 단서 ID를 보내지 않습니다.** C-A 타격음은 C3를, C-B 칠판음은 C4를 시작하지 않습니다. 분위기 효과음도 마찬가지입니다.
- **문자는 이동만 요구합니다.** 발송 / 전문 노출 / 목적지 도착 / 모형 관찰은 서로 다른 기록입니다. 확인 버튼이나 열람 시간 벌점은 없습니다.
- P형: 도착 보상 0, 유효 미도달 배치 +6(시험값). 하루 최대 1쌍 · 회차 최대 2쌍 · 각 P ID 회차 1회. 동시 후보는 **P3 → P2 → P1 → P4** 순서. 전문 노출 전에 짝 카드가 정산되면 P형은 미판정으로 취소합니다. **수칙 결과를 먼저 처리하고**, 게임이 아직 진행 중일 때만 문자 결과를 적용합니다.
- 세부 조건(발송 자격, 목적지, 충돌표)은 정본 3·4절을 따릅니다.

### 2.8 설계 금기 — 선의로 어기기 쉽습니다

- **플레이 중 축·델타·위반 팝업이나 게이지를 표시하지 마십시오.** 플레이어는 자기가 어겼다는 사실을 모른 채 환경이 변하는 것만 봐야 합니다.
- **공지 카드나 별도 인수 목록 UI를 추가하지 마십시오.**
- **태블릿에는 수칙 본문만 노출합니다.** 「개발 판정 상세」와 공통 명세는 제작자용입니다.
- **수칙 본문이 요구한 행동만 채점합니다.** 괴담의 이유나 기관 설명은 판정 조건이 아닙니다. 「목록에 없던 것」은 목록 UI를, 「이름」은 개인정보·음성 인식을, 「손이 네 개」는 추가 모델을 요구하지 않습니다.
- 의도적으로 모형을 보지 않는 플레이에 **강제 시선이나 체류 즉사를 추가하지 마십시오.**
- 새 공지·카드·모형·문자를 추가하지 마십시오. 24·8·12 구성은 고정입니다.
- 정본은 **새 범용 규칙 엔진이나 대규모 구조 개편을 요구하지 않습니다.** 팀의 현재 데이터 구조에 연결하십시오.

---

## 3. Unity 에디터와 작업하기

### 3.1 연결

이 프로젝트에는 `com.unity.pipeline`이 설치되어 있어, 실행 중인 에디터를 **Unity CLI** 또는 **Unity CLI의 MCP 서버**(`unity mcp`)로 직접 조작할 수 있습니다.

```powershell
unity status        # 에디터 인스턴스가 "ready"인지 확인
unity command       # 에디터가 노출하는 명령 목록
unity command console_status   # 컴파일 실패 여부와 콘솔 카운트
```

- MCP로 연결된 세션에서는 같은 명령이 `unity` MCP 도구(`editor_status`, `console_status`, `recompile`, `run_tests` 등)로 보입니다.
- **작업 전에 에디터가 `ready`인지 먼저 확인하십시오.** 에디터가 닫혀 있으면 모든 명령이 「Pipeline instance를 찾을 수 없음」으로 실패합니다. MCP 도구에는 프로젝트를 여는 명령이 없으므로, 사용자에게 `unity open "<horror house 경로>"` 또는 Unity Hub로 열어 달라고 요청하십시오.
- 에디터가 자동화 모드로 열리지 않았기 때문에 **모달 대화상자가 뜨면 명령이 멈출 수 있습니다.** 응답이 없으면 에디터 화면을 확인해 달라고 요청하십시오.
- **연결이 안 되는데 에디터는 켜져 있다면 Safe Mode(컴파일 에러)를 먼저 의심하십시오.** `unity pipeline list`로 확인한 뒤 C# 컴파일 에러를 고치고 Unity를 재시작합니다.
- `Library/Pipeline/.unity-pipeline-port`에는 에디터에서 C#을 실행할 수 있는 인증 토큰이 들어 있습니다. 내용을 출력하거나 공유하지 마십시오.
- `eval`, `run_script`는 임의 코드 실행입니다. 읽기 목적 외에는 사용자 확인 후 쓰십시오.

### 3.2 씬·에셋 수정 규칙

- **에디터가 연결되어 있으면 `.unity` / `.prefab` / `.asset` YAML을 직접 편집하지 마십시오.** fileID·GUID를 틀리기 쉽고, 에디터가 재임포트 전까지 변경을 모르며, 활성 씬이 아닌 파일을 고치는 실수가 생깁니다. 에디터 명령(`create_gameobject`, `set_component_properties`, `save_scene` 등)을 쓰십시오.
- 에디터가 없을 때만 파일을 직접 고치고, 그 사실을 명시하십시오.
- 씬·프리팹 수정 전에는 **LFS 잠금**을 확인합니다(§9).
- `switch_build_target`, `clear_baked_lighting`, `delete_asset`, `package_add/remove`, 설정 변경(`set_*_settings`)처럼 되돌리기 어려운 명령은 **사용자 확인 후** 실행합니다.

### 3.3 검증

1. 코드 수정 후 `recompile` → `recompile_status` → `console_status`로 **컴파일 에러 0**을 확인합니다.
2. 테스트가 있으면 `run_tests`로 실행합니다. 정본 6절의 교차 검증 16종과 각 카드의 「검증 절차·기대 결과」가 테스트 케이스의 원천입니다.
3. 에디터를 쓸 수 없을 때의 대안: `mono-mcs`와 UnityEngine 최소 스텁으로 `-langversion:7.2` 컴파일(`UNITY_EDITOR` / 심볼 없음 / `NIGHTDUTY_DEBUG` 세 구성). 스텁에 없는 멤버 에러는 스텁 문제이며, 스텁을 대충 구현하면 테스트가 거짓 통과합니다. 가능하면 실제 에디터 검증을 우선합니다.
4. `NightDuty ▸ 테스트 씬 생성` 메뉴가 32m 복도 + 실시간 Point Light 8개 + `DebugAxisDriver` 씬을 만듭니다. 조도 축 연출은 이 씬에서 눈으로 확인합니다. 에디트 모드에서도 **Game 뷰**를 쓰십시오(§8-6).

---

## 4. 코드 구조

### 4.1 어셈블리 — 의존은 단방향

```
Assets/_Game/Scripts/
├── NightDuty.Core.asmdef          references: []               판정 · 수치 · 편성
│   ├── Core/          Vocabulary · Bands · EventBus · IFearAxisReader · DebugAxisDriver
│   ├── Data/          BandTableSO · DocumentTypes
│   ├── Stats/         DaySummary
│   ├── Presentation/  ISpacePresenter · IDocumentView        (인터페이스만)
│   ├── Rules/ · Rules/Conditions/ · Direction/               (비어 있음)
│   └── Editor/        NightDuty.Editor.asmdef   references: [NightDuty.Core], Editor 전용
│                      BandTableAssetCreator · TestLightRigSetup · TestSceneBuilder
└── (예정) NightDuty.Client.asmdef   references: [NightDuty.Core]   플레이어 · UI · 연출
Assets/_Game/ScriptableObjects/BandTable.asset
```

- `NightDuty.Client` 어셈블리는 **아직 만들어지지 않았습니다.** 클라이언트 코드를 추가할 때 함께 만듭니다.
- **`NightDuty.Core`는 연출을 참조하지 않습니다.** 축이 바뀌면 Core는 이벤트만 올리고, 무엇을 그릴지는 Client가 정합니다. 「축이 올랐으니 여기서 바로 불을 끄면 되겠다」는 유혹이 반드시 옵니다. **asmdef에 참조를 추가해 우회하지 마십시오.**
- `Assets/3.1. Programmer_lee/02 Scripts/AxisTestLightRig.cs`는 실험용이며 버릴 파일입니다.

### 4.2 데이터 흐름과 폴더 배치

```
[Data]        RuleSO 24장 · SpaceProfileSO · BandTableSO · AudioSequenceSO
[Direction]   DayDirector → 덱 배정 → EncounterDirector(조우) → MessageDirector/ParadoxResolver(문자)
[Rules]       RuleWatcher × N  (대기 → 진행 중 → 준수/위반/미판정)
[Stats]       FearAxisSystem  min(100, 기존+델타), 100 → 종료 잠금 → BandResolver → BandChanged
[Presentation] 청각·조도·배치 → SpacePresenter (Client)   /   신뢰 → 수치만 누적
```

새 파일의 자리는 「고르는 것인가(Direction) / 판단하는 것인가(Rules) / 숫자를 올리는 것인가(Stats) / 그리는 것인가(Presentation·Client)」로 정합니다.
연출 시퀀스만 **Unity Timeline + Signal**을 씁니다.

### 4.3 핵심 API

```csharp
namespace NightDuty {
    enum SpaceId  { None=0, Corridor=1, Toilet=2, Classroom_1_1=3, Classroom_1_3=4, ScienceRoom=5 }
    enum FearAxis { Auditory=0, Illuminance=1, Layout=2, Trust=3 }
    enum Band     { Band0=0, Band1=1, Band2=2, Band3=3, Band4=4 }

    static class Bands { static Band Of(int value); static int LowerBound(Band); static int UpperBound(Band);
                         static float Progress(int value, Band band); }
    interface IFearAxisReader { int GetValue(FearAxis); Band GetBand(FearAxis); }

    static class EventBus {
        event Action<SpaceId, FearAxis, Band, Band> BandChanged;   // space, axis, from, to
        event Action<SpaceId, FearAxis, float>      BandProgress;
        event Action<int, ClauseZeroType>           DayStarted;    // 2번째 인자 재설계 예정
        event Action<DaySummary>                    DayEnded;
        event Action<FearAxis>                      AxisCritical;  // 100 도달
        static void ClearAll();
    }
    interface ISpacePresenter { SpaceId Space { get; }
        void OnBandChanged(FearAxis, Band, Band); void OnBandProgress(FearAxis, float); void ResetForNewDay(); }
}
```

- `DebugAxisDriver`는 실제 `FearAxisSystem`과 **똑같이 `IFearAxisReader`를 구현**해 같은 포트에 꽂히는 가짜 공급원입니다. 클라이언트 작업이 판정 시스템 완성을 기다리지 않게 해 줍니다. **계속 동작하게 유지하십시오.** 플레이어 빌드에는 포함되지 않아야 합니다(`#if UNITY_EDITOR || NIGHTDUTY_DEBUG`).
- 시스템과 클라이언트 사이를 넘나드는 것은 `BandChanged` 이벤트와 `ISpacePresenter` / `IDocumentView` 인터페이스, 그리고 정본 5절의 **기존 이벤트 연결**(문 명령 수락/닫힘 완료, 공간 진입/이탈, 점검 완료, 단서 재생/식별, 모형 관찰, Tab 상태, 손전등 상태, 밤 종료 요청)뿐입니다. 기존 시스템에 신호가 없으면 해당 연결만 추가합니다.

### 4.4 개정 대기 중인 기존 코드 — 정본과 어긋남

아래는 알려진 불일치입니다. 새 코드에서 이 값들에 의존하지 마십시오.

| 위치 | 현재 | 정본 |
|---|---|---|
| `Bands` · `Vocabulary` · `BandTableSO` 주석 | Band4 = 90~100 | 90~99 표현, **100은 종료 잠금 경로로 별도 처리** |
| `Bands.RedThreshold = 75` | 존재 | 없음 → 제거 대상 |
| `BandTableSO.cs` 과학실 Band4 등 개수 | 1 | **0** (수정 후 `BandTable.asset` 재생성) |
| `ClauseZeroType`, `DayStarted(int, ClauseZeroType)` | 존재 | §0 조항 폐기 → 역설 문자 체계로 대체, 시그니처 재설계 |
| `DaySummary.ImprintAxis` | 존재 | 프로파일링 폐기 → 제거 대상 |
| `AxisCritical(FearAxis)` | 축만 전달 | 원인 카드/문자 ID·공간 기록, **종료 신호 1회 후 델타 중단** 보장 |
| `IDocumentView.Render(..., ClauseZeroType, ...)` | 존재 | `ClauseZeroType` 제거에 맞춰 시그니처 정리 |

- `DayStarted`·`IDocumentView`는 클라이언트 코드가 구독·구현하는 시그니처입니다. 바꿀 때는 진선님 쪽 작업과 함께 맞춥니다.

---

## 5. 구현 규칙과 함정

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
14. 축은 오르기만 하지만, 구간 전환에는 히스테리시스(상승은 임계값, 하강은 임계값 −5)를 둡니다. 등 개수는 구간으로 스냅하고 색온도는 구간 안에서 보간합니다.

### 5.4 오디오

15. **「정확히 세 번」(C1) 같은 횟수 단서는 반드시 단발 클립**으로 발주합니다. 루프 클립은 횟수를 셀 수 없어 해당 카드를 판정할 수 없게 됩니다. C1은 세 획 전체가 전달돼야 활성화됩니다.
16. T2의 12초 시퀀스는 판정 데이터와 클립 길이를 같은 값으로 씁니다.
17. 분위기 효과음과 모형 효과음은 카드 단서 ID를 보내지 않습니다(§2.7).

---

## 6. 폴더 구조

- `Assets/_Game/` — **팀의 실제 게임 콘텐츠.** Scenes/, Scripts/, Prefabs/, Materials/, Art/, Audio/, ScriptableObjects/. 전부 git으로 추적합니다. 벤더 프리팹을 고칠 때는 원본을 수정하지 말고 `Assets/_Game/Prefabs/`에 **Prefab Variant**를 만듭니다.
- `Assets/3.1. Programmer_lee/`, `Assets/3.2 Programmer_Kim/`, `Assets/0. Main/` — 개인 작업 폴더. **일회성 실험과 단일 시스템 테스트 씬 전용**입니다(예: `_Test_AxisRig.unity`). 공유 코드와 출시 콘텐츠는 처음부터 `_Game`에 둡니다. 나중에 옮기면 SO·프리팹의 스크립트 참조가 모두 깨집니다.
  - 폴더명 표기가 불일치합니다(`3.1.` 뒤에는 점이 있고 `3.2` 뒤에는 없습니다). **경로를 하드코딩하지 마십시오.**
  - `3.2 Programmer_Kim`은 진선님 폴더이며 브랜치명(`Programmer_Jinsun`)과 다릅니다.
- `Assets/1. Design/`, `Assets/2. Art/` — 기획·아트 팀 작업 폴더.
- `Assets/NOT_Lonely/` — 서드파티 패키지(`HQ_AbandonedSchool`, `Object Placement Tool`, `SimpleFPController`). **gitignore 대상**이며 팀원마다 **같은 버전**을 로컬에 설치합니다.
- `Assets/Scenes/`, `Assets/Settings/` — URP 템플릿 기본 씬과 렌더 설정.
- `.gitkeep`은 의도적으로 비워 둔 폴더 표시입니다. 실제 파일이 생긴 폴더의 `.gitkeep`은 지웁니다(현재 `Core/`, `Data/`, `Editor/`, `Presentation/`, `Stats/`에 남아 있음).

---

## 7. 씬과 라이팅

- 조도 연출용 라이트는 **Point/Spot + Realtime**입니다. Mixed는 간접광이 남아 Band4 「전부 소등」에서도 방이 은은하게 밝습니다.
- **벤더 데모씬의 조명은 전부 Area Light(베이크 전용)** 이라 실시간으로 켜고 꺼도 화면에 반영되지 않습니다.
- URP Forward+이므로 오브젝트당 라이트 수 제한은 없습니다. 다만 Additional Light Shadow 아틀라스가 2048이라 **등 8개가 모두 그림자를 던지면 부족**합니다. 대부분 `Shadows: None`으로 둡니다.
- 새 씬은 `Assets/_Game/Scenes/`에 새로 만듭니다(공간 4개 + 경비실).
- **벤더 데모씬 사본(약 19MB)은 부품 창고로만 씁니다.** 그 안에서 작업하거나 커밋하지 마십시오. 로드가 느리고 `Ran out of Graphics Ring Buffer space`를 일으킬 수 있으며, 병합 충돌은 사실상 해결할 수 없습니다.

---

## 8. 환경 필수 수정과 알려진 문제

1. **벤더 셰이더 수정 — 모든 PC에서 각자 해야 하며 커밋할 수 없습니다.**
   `Assets/NOT_Lonely/HQ_AbandonedSchool/Shaders/NOT_Lonely_LightRays.shader`에서 `uniform float4 _CameraDepthTexture_TexelSize;` **두 줄을 삭제**합니다(원래 224행과 507행).
   URP 17.3이 이 변수를 자체 선언하므로 d3d11에서 재정의 에러가 납니다. 변수는 쓰이지 않아 기능 변화가 없습니다.
   **증상:** `redefinition of '_CameraDepthTexture_TexelSize'`. `URP.unitypackage`를 다시 임포트하면 수정이 되돌아갑니다.
2. `HQ_AbandonedSchool`은 Built-in RP 형태로 배포되며, 함께 들어 있는 `URP.unitypackage`를 반드시 임포트해야 합니다(현재 작업본은 임포트 완료).
3. 콘솔의 `Account API did not become accessible...` 경고는 무시해도 됩니다.
4. 압축 해제 시 백신이 `.cs`/`.unity` 파일을 격리하는 사례가 있었습니다. 벤더 폴더에 파일이 실제로 있는지 확인하십시오.
5. `BandTable.asset`의 Band3 Tint 보간 때문에 조도 89에서 이미 붉은색에 가깝습니다. 연속이라 튀지는 않지만 눈으로 판단할 값입니다.
6. Scene 뷰가 초록색이면 조명 문제가 아니라 디버그 드로우 모드(`Contributors / Receivers`)가 켜진 것입니다. 해제법을 아직 못 찾았으니 **Game 뷰**를 쓰십시오.

---

## 9. Git / LFS

자세한 내용은 `../Docs/형상관리_매뉴얼.md`를 따릅니다.

- **씬·프리팹 수정 전에 잠금을 확인합니다.** `.unity`와 `.prefab`은 LFS 잠금 대상입니다(Force Text YAML로 저장되며 LFS 저장이 아니라 잠금만 추적).
  `git lfs locks` → `git lfs lock "Assets/경로/파일.unity"` → 커밋·푸시 → `git lfs unlock "Assets/경로/파일.unity"`.
- **`Assets/NOT_Lonely/`는 gitignore입니다.** `git add`하지 마십시오. Git 클라이언트의 **"Discard All"이 이 미추적 폴더를 통째로 삭제할 수 있습니다.**
- 텍스처·모델·오디오·영상·폰트·압축 파일·네이티브 플러그인은 `.gitattributes`에 따라 LFS로 추적합니다. LFS를 우회해 대용량 바이너리를 커밋하지 마십시오. `.gitattributes`를 개인적으로 수정하지 마십시오.
- `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, `UserSettings/`를 강제로 add하지 마십시오.
- **커밋 전 `git status`를 확인하십시오.** 데스크톱 앱 세션에서 프로젝트 루트에 `Claude outputs/` 폴더가 생길 수 있습니다. 프로젝트의 일부가 아니므로 `git reset "Claude outputs"`로 제외합니다.
- 커밋 프리픽스: `feat:` · `fix:` · `art:` · `chore:` · `docs:`
- 사용자는 커밋 메시지 작성을 자주 요청합니다. **Summary(50자 내외) + Description** 형식으로, 파일 목록보다 **왜 이렇게 했는지**와 **나중에 실수하기 쉬운 점**을 적습니다.

---

## 10. 사람과 브랜치

| 사람 | 역할 | 브랜치 | 개인 폴더 |
|---|---|---|---|
| 이성현 (민트) | **시스템** — 판정 · 수치 · 편성(덱·조우·문자) · 규칙 데이터 스키마. 주로 `.cs`·`.asset` 작업이라 씬 잠금이 드뭅니다. | `Programmer_Lee` | `Assets/3.1. Programmer_lee/` |
| 진선 | **클라이언트** — 플레이어 · 태블릿 UI · HUD · 공간 연출 · 씬 · 오디오. 씬 잠금을 가장 자주 잡습니다. 아트팀과 잠금 시간을 조율하고 당일 해제합니다. | `Programmer_Jinsun` | `Assets/3.2 Programmer_Kim/` |

- 원격에는 `main`, `Programmer_Lee`, `Programmer_Jinsun`, `hyunuung`, `Art` 브랜치가 있습니다. 개인 브랜치에서 작업한 뒤 `main`에 병합합니다.
- 공간 레이아웃이나 대상 서수(「세 번째 칸」 등)를 바꾸면 수칙의 의미가 바뀝니다. 시스템 담당에게 알리십시오.

---

## 11. 작업 방식

- 작업을 맡기면 **끝까지 해 주기를 기대**합니다. 단, 파괴적이거나 되돌릴 수 없는 작업은 먼저 확인합니다.
- 설계 산출물은 `../Docs/Claude outputs/`에 markdown으로 둡니다. 다이어그램은 아티팩트로도 발행합니다.
- 병렬 에이전트를 쓸 때는 **공유 타입 시그니처를 모든 프롬프트에 똑같이 넣고** 「자기 것만 정의하고 나머지는 이름으로만 참조하라」고 지시합니다. 컴퓨터 조작 도구와 에디터·디바이스 반영은 **메인 세션 한 곳에서만** 합니다.
- 경로는 세션마다 다를 수 있습니다. 절대경로를 하드코딩하지 말고 연결된 폴더를 먼저 확인하십시오.

---

## 12. 미해결 — 사용자 확인 필요

| # | 항목 | 비고 |
|---|---|---|
| 1 | **근무 일수** | 정본에 고정 일수가 없습니다. 조우 8개를 하루 하나씩 소비하는 구조와 「남은 근무일 수」 언급만 있습니다. |
| 2 | 일정 재작성 | `4주_압축_스케줄.md`는 3공간 기준이라 무효입니다. 4공간·24수칙 기준으로 다시 짜야 합니다. |
| 3 | 응시 판정 여유 | 정본은 「중앙 레이의 첫 가시 충돌체, 끊기면 0」입니다. 인수인계서는 H2·C3의 「짧게 본 행동 비판정」을 위해 약 12° 판정 원뿔을 권합니다. 가림 검사와 0 초기화는 공통이며, 원뿔 폭은 확인 후 확정합니다. |
| 4 | 진선님 폴더명 ↔ 브랜치 불일치 | `3.2 Programmer_Kim` ↔ `Programmer_Jinsun`. 정한 뒤 이 파일에 반영합니다. |
| 5 | git 커밋 상태 | 이전 세션이 디바이스 연결이 끊긴 채 종료됐습니다. `git log --oneline -10`·`git status`로 먼저 확인합니다. |
| 6 | 보완안 수치 | 0.2초 식별, 미도달 +6, 1.5m 반경, 중립 구역 폭 등은 **시험값**입니다. 플레이테스트로 확정합니다. |
| 7 | 포획 엔딩 연출 | 종료 신호까지만 계약되어 있습니다. 신뢰 100의 포획 연출 연결도 별도 확인 항목입니다. |
| 8 | 씬 대상 연결 | 24장의 `eligibleBand`·`triggerId`·`targetIds` 매핑은 기획팀 확인이 필요합니다. |
| 9 | 구현 전 자산 확인 | 선/앉은 전신 모형 프리팹, 문 E 스크립트, 등 그룹 수, 닫힌 칸 아래 빛 자산, Tab 일시정지 기능. |

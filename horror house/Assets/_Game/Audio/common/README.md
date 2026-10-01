# 공통 · 화면 — 오디오 사용처

폴더: `audio_final/common/` · 파일 8개

뒤에 `_Hear75`, `_Hear90`이 붙은 파일은 그 청각 구간 전용 합본이다. 앞 구간 소리에 그 구간에서 추가되는 소리가 이미 붙어 있으므로, 합본을 쓰는 구간에서는 기본형을 함께 재생하지 않는다.

## 파일별 사용처

| A ID | 파일 | 언제 재생하나 | 길이 | 최대 음량 |
|---|---|---|---|---|
| A24 | `SFX_COMMON_Footstep.wav` | 플레이어 보행 시 발소리. 한 걸음 소스 | 1.00초 | 0.0 dB |
| A25 | `SFX_COMMON_TabletChime.wav` | 태블릿 문자(G·N·P)를 받을 때 1회 | 1.52초 | -2.3 dB |
| A26 | `SFX_COMMON_FlashlightClick_off.wav` | 손전등을 끌 때 | 1.00초 | -2.8 dB |
| A26 | `SFX_COMMON_FlashlightClick_on.wav` | 손전등을 켤 때 | 1.28초 | -4.1 dB |
| A29 | `SFX_COMMON_ShiftEndChime.wav` | 04:00 근무 자동 종료 때 교내 방송 차임 1회. 이 소리 뒤 화면이 어두워진다 | 4.68초 | -2.0 dB |
| A30 | `AMB_SCHOOL_RoomTone.wav` | 근무 내내 깔리는 기본 환경음(루프) | 10.24초 | -35.7 dB |
| A31 | `BGM_TITLE_NightShift.mp3` | 시작 화면과 타이틀 메뉴 음악 | 181.63초 |  |
| A32 | `SFX_COMMON_GameOver.wav` | 네 축 중 하나가 100에 도달해 게임오버 화면으로 넘어갈 때 | 9.81초 | -2.0 dB |

## 손볼 곳과 정할 것

- **A24 `SFX_COMMON_Footstep.wav`**: 피치를 조금씩 바꾼 변형 5개를 만들어 번갈아 재생 권장 / 파형이 최대치에 닿아 잘린 샘플이 81개 있어 지직거릴 수 있다. 2~3 dB 낮춰 다시 뽑는 편이 안전하다
- **A25 `SFX_COMMON_TabletChime.wav`**: 역설 문자도 같은 소리를 쓴다. 소리로 구분되면 안 된다
- **A30 `AMB_SCHOOL_RoomTone.wav`**: 15~30초 루프가 필요한데 10.24초다. 이어 붙일 때 이음새를 확인할 것
- **A31 `BGM_TITLE_NightShift.mp3`**: 3분 1초 원본이다. 60~90초 루프 구간으로 잘라 쓸 것

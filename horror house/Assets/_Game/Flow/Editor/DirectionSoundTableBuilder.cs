using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 연출 소리 표(<see cref="DirectionSoundTableSO"/>)를 채운다 — 2026-10-04 사운드 전달본 기준.
/// <list type="bullet">
/// <item>경로가 <c>_*</c>로 끝나면 <c>_1</c>·<c>_2</c>·<c>_3</c> …(또는 <c>_01</c>·<c>_02</c> …)을 모두 한 항목에 넣는다(낼 때 무작위).</item>
/// <item>이름 뒤 <c>+</c>는 같은 순간 겹쳐 내는 둘째 소리.</item>
/// <item>가짜 놀람 사물함·벌레 떼는 김진선님 타임라인이 제 소리를 낸다 — 여기 항목은 그 자리가 없을 때의 대체다.</item>
/// </list>
/// <see cref="StandInPrefabBuilder"/>의 「몹 대역·소리 연결」도 이것을 부른다.
/// </summary>
public static class DirectionSoundTableBuilder
{
    public const string TablePath = "Assets/_Game/Resources/DirectionSounds.asset";

    private const string Au = "Assets/_Game/Audio/";
    private const string Amb = "Assets/_Game/Resources/Ambience/";
    private const string Kim = "Assets/3.2 Programmer_Kim/99 Resources/04 Sound/";

    /// <summary>키 · 경로 · 볼륨.</summary>
    public static readonly string[,] Map =
    {
        // 조우 공통 — 다가오는 기척(전조)
        { "*.foreshadow", Au + "common/SFX_COMMON_EncounterCue_*", "0.6" },

        // 교실 소년: 의자 삐걱 → 놓아줄 때 수업 종 · C2 교차 = 머리 박기
        { "E.BoySeated.confront", Au + "class/SFX_CLASS_SeatCreak_*", "0.9" },
        { "E.BoySeated.release", Au + "class/SFX_CLASS_SchoolBell.wav", "0.6" },
        { "E.BoyBang.confront", Au + "class/SFX_CLASS_SeatCreak_*", "0.9" },
        { "E.BoyBang.release", Au + "class/SFX_CLASS_SchoolBell.wav", "0.6" },
        { "E.BoyBang.headbang", Au + "class/SFX_CLASS_HeadThud.wav", "1" },

        // 천장 다리: 쿵쿵(전조) → 먼지
        { "E.CeilingLegs.foreshadow", Au + "class/SFX_CLASS_LegThump_*", "0.85" },
        { "E.CeilingLegs.confront", Au + "class/SFX_CLASS_CeilingDust_*", "0.9" },

        { "E.PhantomDoor.confront", Au + "class/SFX_CLASS_DoorSettle_*", "0.9" },
        { "E.Footsteps.confront", Au + "hall/SFX_HALL_H3_BarefootRun.wav", "0.9" },
        { "E.CallingVoice.confront", Au + "voice/VO_Hey.wav", "0.85" },
        { "E.PeopleTree.confront", Au + "creature/SFX_TREE_BranchCreak.wav", "0.8" },
        { "E.PeopleTree.confront+", Au + "creature/SFX_TREE_HumanMurmur.wav", "0.6" },
        { "E.YellowFace.confront", Amb + "Stingers/stinger_breath.ogg", "0.8" },
        { "E.SuitMan.confront", Au + "library/SFX_LIBRARY_WindowTap.wav", "1" },
        { "E.SuitMan.confront+", Au + "library/SFX_LIBRARY_BlindRattle_*", "0.6" },
        { "E.HallEndFigure.confront", Kim + "SFX_MosterBreath.wav", "0.8" },
        { "E.HallEndFigure.release", Au + "hall/SFX_HALL_EndFigureExit_*", "0.85" },
        { "E.ModelRush.confront", Au + "hall/SFX_HALL_MannequinApproach_*", "1" },
        { "E.ModelRush.release", Au + "creature/SFX_MANNEQUIN_PassByRush.wav", "1" },
        { "E.ScienceBlackout.confront", Au + "light/SFX_LIGHT_BreakerOff.wav", "1" },
        { "E.ScienceBlackout.release", Au + "light/SFX_LIGHT_BreakerOn.wav", "0.9" },
        { "E.ToiletBlackout.confront", Au + "light/SFX_LIGHT_BreakerOff.wav", "1" },
        { "E.ToiletBlackout.release", Au + "light/SFX_LIGHT_BreakerOn.wav", "0.9" },
        { "E.ToiletGirl.confront", Au + "toilet/SFX_TOILET_GirlSteps_*", "0.9" },
        { "E.ToiletGirl.confront+", Au + "toilet/SFX_TOILET_DoorClose.wav", "1" },
        { "E.CctvPerson.confront", "Assets/_Game/Resources/Cctv/cctv_static.wav", "0.6" },

        // 소리 수칙 신호
        { "H2.cue", Au + "door/SFX_HALL_DoorUnlatchCreak.wav", "0.9" },   // 60차(민: 「H2 문이 열릴 때 소리가 닫히는 소리에 가깝다」 — 옛 DoorOpenSlow는 2.3초에 쾅): BigSoundBank 1702 손잡이 딸깍 → 느린 열림
        // 50차: 플레이어가 [E]로 여닫는 문(PlayerInteractor.PlayDoorSound). H2 단서(DoorOpenSlow)와 다른 소리여야 한다.
        { "door.open", Au + "door/SFX_DOOR_Open.wav", "0.4" },   // 61차(민: 「문 열리는 소리 너무 큼」) 0.7 → 0.4(약 -5dB). 60차: 나무 문 삐걱 열림(BigSoundBank 3205 앞 1.7초) — 옛 TOILET_DoorOpen(쇳소리 끽)은 관물대로
        { "door.close", Au + "door/SFX_DOOR_Close.wav", "0.95" },   // 61차(민: 「닫히는 소리가 작다 · 관물대 닫히는 소리 같다」): BigSoundBank 2421 나무 문 쿵 + 걸쇠(옛 HALL_DoorClose는 쇳소리)
        { "door.locker", Au + "hall/SFX_HALL_LockerDoorSwing_*", "0.7" },
        // 51차(민: 「서랍·관물대·책장 아래 여닫이에도 문 소리가 난다」) — 민이 고른 소리(furniture/LICENSE.txt).
        { "door.drawer.open", Au + "furniture/SFX_FURN_DrawerOpen.mp3", "0.7" },
        { "door.drawer.close", Au + "furniture/SFX_FURN_DrawerClose.mp3", "0.7" },
        { "door.cabinet.open", Au + "furniture/SFX_FURN_CabinetOpen.wav", "0.55" },
        { "door.cabinet.close", Au + "furniture/SFX_FURN_CabinetClose.wav", "0.55" },
        { "door.locker.open", Au + "hall/SFX_HALL_LockerOpen_Swing1.wav", "0.7" },   // 67차(민 선택 「D 철문 젖힘」): HALL_LockerDoorSwing_1 앞 1.2초(끝 0.25초 줄임) — 옛 TOILET_DoorOpen(3.4초)이 길다는 피드백
        { "door.locker.close", Au + "furniture/SFX_FURN_LockerClose.wav", "0.6" },
        // 51차 점프스케어(민: 「현악기 효과음이 짜친다」) — OpenGameArt Horror Hit Soundpack 1(CC0). 약·중·강, 얼굴용 고음 겹.
        { "stinger.weak", Au + "stinger/SFX_STING_Weak_*", "0.8" },
        { "stinger.mid", Au + "stinger/SFX_STING_Mid_*", "0.95" },
        { "stinger.strong", Au + "stinger/SFX_STING_Strong_*", "1" },
        { "stinger.high", Au + "stinger/SFX_STING_High_*", "0.75" },
        { "stinger.corpse", Au + "stinger/SFX_STING_Corpse.mp3", "1" },   // 60차: 민 제공 시체 점프스케어
        { "corpse.fall", Au + "class/SFX_CLASS_HeadThud.wav", "1" },
        { "cctv.face", Au + "ui/SFX_TABLET_Glitch.wav", "0.9" },
        { "C1.cue", Au + "class/SFX_CLASS_ChalkStroke.wav", "1" },
        { "C4.cue", Au + "class/SFX_CLASS_C4_RedHum.wav", "0.9" },
        { "S2.cue", Au + "lab/SFX_LAB_GlassBreak.wav", "1" },
        { "T1.cue", Au + "toilet/SFX_TOILET_Flush_Hear25.wav", "1" },
        { "T2.cue", Au + "toilet/SFX_TOILET_StallShuffle_*", "0.9" },
        { "K2.cue", "Assets/_Game/Resources/Cctv/cctv_static.wav", "0.5" },
        { "L2.cue", Au + "library/SFX_LIBRARY_PageTurn.wav", "1" },

        // 가짜 놀람(자리가 없을 때의 대체 · 손전등 깜빡임은 항상)
        { "fake.locker.rattle", Au + "hall/SFX_HALL_LockerRattle_*", "0.8" },
        { "fake.locker.row", Au + "hall/SFX_HALL_LockerDoorSwing_*", "0.8" },
        { "fake.flashlight.flicker", Au + "common/SFX_Flashlight_Flicker_*", "0.7" },
        { "fake.bugs", Au + "creature/SFX_ROACH_Scurrying.wav", "0.6" },
        { "fake.bugs+", Au + "creature/SFX_ROACH_Drop.wav", "0.6" },

        // 5일차 피날레
        { "finale.letmein", Kim + "SFX_MosterBreath.wav", "0.85" },   // 57차(민: 「들어가게 해줘 오디오가 너무 구리다 — 비명이나 숨소리로」): 목소리 → 창 너머 거친 숨
        { "finale.letmein+", Au + "guard/SFX_GUARD_DoorHandle_*", "0.8" },
        { "finale.smile", Au + "capture/varco/END-FIN-02_*", "0.9" },
        { "finale.crtoff", Au + "capture/END_FIN_01_CRT_PowerOff.wav", "1" },
        { "finale.wipe", Au + "capture/END_FIN_03_MessageDelete.wav", "0.9" },
        { "finale.knock", Au + "library/SFX_LIBRARY_WindowTap.wav", "1" },

        // 붙잡힘(CaptureDirector.Audio) — 2026-10-04 클라이맥스_VARCO생성본(CLX) + 잡힘연출(CAP). 충격음은 hits(가장 큰 지점)로 박자를 맞춘다
        // 흔들림 구간: 상승음이 소리 끊김 순간에 꼭대기
        { "capture.rise.auditory", Au + "climax/CLX-13_2.wav", "0.6" },
        { "capture.rise.illuminance", Au + "climax/CLX-14_1.wav", "0.6" },
        { "capture.rise.layout", Au + "climax/CLX-13_2.wav", "0.6" },
        { "capture.rise2.auditory", Au + "climax/CLX-16_1.wav", "0.75" },
        { "capture.breath", Au + "climax/CLX-15_3.wav", "0.6" },
        // 소리 끊김 · 정적
        { "capture.cut", Au + "climax/CLX-32_2.wav", "0.8" },
        { "capture.cut.illuminance", Au + "climax/CLX-24_1.wav", "0.8" },
        { "capture.swell", Au + "climax/CLX-08_3.wav", "0.8" },
        { "capture.pre.auditory", Au + "climax/CLX-10_2.wav", "0.8" },
        { "capture.pre.illuminance", Au + "climax/CLX-27_2.wav", "0.9" },
        { "capture.dark.illuminance", Au + "capture/CAP_S3_07_DarkDrone.wav", "0.7" },
        { "capture.choke.illuminance", Au + "capture/CAP_COM_10_ChokedBreath.wav", "0.6" },
        { "capture.choke.layout", Au + "capture/CAP_COM_10_ChokedBreath.wav", "0.6" },
        // 얼굴 — 저음 · 중음 · 고음 · 목소리(축별 등장 소리 capture.<축>은 장면 쪽이 3D로)
        { "capture.face.low", Au + "climax/CLX-04_3.wav", "0.75" },
        { "capture.face.mid.auditory", Au + "climax/CLX-01_3.wav", "0.7" },
        { "capture.face.mid.illuminance", Au + "climax/CLX-09_3.wav", "0.7" },
        { "capture.face.mid.layout", Au + "climax/CLX-01_3.wav", "0.7" },
        { "capture.face.high.auditory", Au + "climax/CLX-07_3.wav", "0.5" },
        { "capture.face.high.illuminance", Au + "climax/CLX-05_2.wav", "0.55" },
        { "capture.face.high.layout", Au + "climax/CLX-21_3.wav", "0.65" },
        { "capture.face.voice.auditory", Au + "climax/CLX-03_3.wav", "0.55" },
        { "capture.face.voice.layout", Au + "climax/CLX-22_2.wav", "0.6" },
        { "capture.tablet", Au + "climax/CLX-25_2.wav", "0.6" },
        { "capture.gasp", Au + "capture/CAP_COM_09_Gasp.wav", "0.5" },
        { "capture.auditory", Au + "capture/CAP_C3_01_EarWhisper.wav", "0.9" },
        { "capture.illuminance", Au + "capture/CAP_S3_06_FlashlightFalseClicks.wav", "0.9" },
        { "capture.layout", Au + "capture/CAP_H1_02_TangledWhispers.wav", "0.8" },
        // 암전 · 카드 · 다시 근무
        { "capture.tail", Au + "climax/CLX-30_2.wav", "0.55" },
        { "capture.tail.auditory", Au + "climax/CLX-31_3.wav", "0.6" },
        { "capture.precard.auditory", Au + "climax/CLX-18_2.wav", "0.45" },
        { "capture.precard.illuminance", Au + "climax/CLX-12_2.wav", "0.7" },
        { "capture.precard.layout", Au + "climax/CLX-12_2.wav", "0.7" },
        { "capture.died", Au + "capture/SFX_RESULT_YouDied.wav", "0.9" },
        { "capture.card", Au + "capture/RST_02_CardAppear.wav", "0.8" },
        { "capture.return", Au + "capture/RST_03_GuardFadeIn.wav", "0.7" },
        { "capture.wake", Au + "capture/varco/RST-04_*", "0.7" },

        // 58차: 조우 대면에 깔던 떨리는 현 tension.confront(CLX-29)는 뺐다 — 되살리지 말 것(민: 「현악기 효과음」).

        // 몸 계기(BodyMeter) — 청각 심박·귀 먹먹함·이명, 조도 거친 호흡, 경계 신호, 회복 한숨
        { "body.heart.62", Au + "body/loud/SFX_BODY_Heartbeat_62.wav", "0.35" },   // 61차(민: 「축 소리들 너무 작음, 티가 안 남」) +12dB 판 — 층 1~4가 약 +13~16dB
        { "body.heart.72", Au + "body/loud/SFX_BODY_Heartbeat_72.wav", "0.5" },
        { "body.heart.86", Au + "body/loud/SFX_BODY_Heartbeat_86.wav", "0.7" },
        { "body.heart.104", Au + "body/loud/SFX_BODY_Heartbeat_104.wav", "0.95" },
        { "body.ear", Au + "body/loud/SFX_BODY_EarPressure.wav", "0.5" },   // 61차: +20dB 판(옛 판은 실제 -46dB로 들리지 않았다)
        { "body.tinnitus", Au + "body/loud/SFX_BODY_Tinnitus.wav", "0.35" },   // 61차: +22dB 판(옛 판은 실제 -57dB)
        { "body.heart.boundary", Au + "body/loud/SFX_BODY_HeartBoundary_2s.wav", "0.85" },   // 61차: +12dB 판
        { "body.breath", Au + "body/SFX_BODY_RoughBreath_*", "0.6" },
        { "body.breath.stop", Au + "body/SFX_BODY_BreathStop.wav", "0.85" },
        { "body.sigh", Au + "body/SFX_BODY_LongSigh.wav", "0.6" },

        // 맵 소리(WorldSounds) — 청각 구간별 하루 한 번(위 구간은 아래 구간에 더해짐), 배치 3·4, 조도 소등
        { "map.hall.1", Au + "hall/SFX_HALL_DoorClose.wav", "0.75" },
        { "map.hall.1b", Au + "hall/SFX_HALL_LockerRattle_*", "0.7" },
        { "map.hall.2", Au + "hall/SFX_HALL_DoorClose.wav", "0.8" },
        { "map.hall.3", Au + "hall/SFX_HALL_LatchOnly.wav", "0.85" },
        { "map.hall.3+", Au + "hall/SFX_HALL_DoorClose.wav", "0.8" },
        { "map.hall.4", Au + "hall/SFX_HALL_FollowSteps_*", "0.85" },
        { "map.class.1", Au + "class/SFX_CLASS_EraserTap.wav", "0.75" },
        { "map.class.2", Au + "class/SFX_CLASS_DeskHit.wav", "0.85" },
        { "map.class.3", Au + "class/SFX_CLASS_BoardScratch.wav", "0.8" },
        { "map.class.3+", Au + "class/SFX_CLASS_TeacherChair.wav", "0.8" },
        { "map.class.4", Au + "class/SFX_CLASS_TeacherChair.wav", "0.85" },
        { "map.lab.1", Au + "lab/SFX_LAB_GlassClink.wav", "0.7" },
        { "map.lab.2", Au + "lab/SFX_LAB_Scrape.wav", "0.75" },
        { "map.lab.3", Au + "lab/SFX_LAB_GlassClink.wav", "0.55" },
        { "map.lab.4", Au + "lab/SFX_LAB_HeavySet.wav", "0.85" },
        { "map.toilet.2", Au + "toilet/SFX_TOILET_SinkKnock.wav", "0.75" },
        { "map.layout.3", Au + "hall/SFX_HALL_CeilingDrop.wav", "0.8" },
        { "map.layout.4", Au + "common/SFX_COMMON_PropTremble_*", "0.22" },
        { "map.lamp.off", Au + "hall/SFX_HALL_FluoTubeDie_*", "0.8" },

        // 교차·신뢰(WorldSounds)
        { "cross.toilet.water", Au + "toilet/SFX_TOILET_WaterMove_*", "0.7" },
        { "cross.guard.breath", Au + "creature/VO_WORKER_HeldBreath.wav", "0.75" },
        { "trust.guard.rattle", Au + "guard/SFX_GUARD_PropRattle_*", "0.8" },

        // 위반 현장 반응(WorldSounds) — 어긴 축의 언어로 그 대상이 반응한다(위반 피드백 1단계)
        { "react.C1", Au + "class/SFX_CLASS_ChalkSnap_*", "0.9" },
        { "react.L1", Au + "library/SFX_LIBRARY_ShelfGroan_*", "0.9" },
        { "react.L2", Au + "library/SFX_LIBRARY_BookShut_*", "0.9" },
        { "react.L5", Au + "library/SFX_LIBRARY_GlassPalm_*", "0.9" },
        { "react.S2", Au + "lab/SFX_LAB_GlassCrunch_*", "0.85" },
        { "react.G1", Au + "hall/SFX_HALL_FollowSteps_*", "0.85" },
        { "react.K2", Au + "cctv/SFX_CCTV_ChairCreakFeed_*", "0.8" },

        // 놓친 이상 다음 날 CCTV 한 컷(CctvReplay)
        { "cctv.replay", Au + "cctv/SFX_CCTV_ReplayFrame_*", "0.8" },

        // 경고·처벌(NightDutySfx)
        { "punish.stamp", Au + "capture/varco/PUN-01_*", "0.9" },
        { "punish.auditory", Au + "capture/PUN_03_WhisperSweep.wav", "0.9" },
        { "punish.illuminance", Au + "capture/varco/PUN-04_*", "0.9" },
        { "punish.layout", Au + "capture/varco/PUN-05_*", "0.9" },
        { "punish.hit", Au + "capture/PUN_06_ShortHit.wav", "0.8" },
        { "punish.cut", Au + "climax/CLX-32_2.wav", "0.6" },

        // 태블릿(TabletBridge · NightDutySfx)
        { "tablet.buzz", Au + "capture/varco/PUN-02_*", "0.7" },
        { "tablet.corrupt", Au + "ui/SFX_TABLET_TextCorrupt_*", "0.7" },
        { "ui.ready", Au + "ui/SFX_UI_ReportReadyTick.wav", "0.6" },
        { "ui.confirm", Au + "ui/SFX_UI_ScannerBeep.mp3", "0.55" },   // 50차: freesound #202530 scanner beep(kalisemorrison, CC0) 미리듣기판

        // 점검 이상 — 보고할 때까지 그 자리에서 루프(NightDutySfx)
        { "inspect.C-3.loop", Au + "class/SFX_CLASS_CeilingCrawl.wav", "0.8" },
        { "inspect.H-2.loop", Au + "hall/SFX_HALL_FountainTrickle_1.wav", "0.7" },
        { "inspect.H-3.loop", Au + "hall/SFX_HALL_CheckBellHum.wav", "0.7" },
        { "inspect.H-4.kick", Au + "trash/SFX_TRASH_KickedCan_*", "1" },   // 65차: H-4 쓰레기통 이상 — 걷어차여 날아가는 깡통(BigSoundBank 0667 CC0 낙하음 이어 붙임)
        { "duty.pickup", Au + "library/SFX_LIBRARY_PageTurn.wav", "0.8" },   // 65차: W15 biology 책 줍기
        { "inspect.S-3.loop", Au + "lab/SFX_LAB_SinkDrip_1.wav", "0.8" },
        { "inspect.T-2.loop", Au + "toilet/SFX_TOILET_PaperTear.wav", "0.7" },
        { "inspect.H-5.loop", "Assets/_Game/Resources/Cctv/cctv_static.wav", "0.55" },   // 66차: 복도 스피커 지직거림
        { "inspect.T-4.loop", Au + "hall/SFX_HALL_FountainTrickle_2.wav", "0.7" },   // 66차: 세면대 물 흐름

        // 점검 「가까이」(NightDutySfx) — +는 0.6초 뒤 이어서, inspect.near는 공용 충격음
        { "inspect.near", Au + "creature/SFX_STINGER_CloseImpact_*", "0.8" },
        { "inspect.H-1.near", Au + "hall/SFX_HALL_PowderPuff.wav", "1" },
        { "inspect.H-2.near", Au + "toilet/SFX_TOILET_DrainBreath.wav", "1" },
        { "inspect.H-3.near", Au + "hall/SFX_HALL_BellMountCreak_*", "1" },
        { "inspect.C-2.near", Au + "class/SFX_CLASS_LampOff_*", "1" },
        { "inspect.C-2.near+", Au + "hall/SFX_HALL_ChairDrag.wav", "0.9" },
        { "inspect.L-1.near", Au + "hall/SFX_HALL_ChairDrag.wav", "0.9" },
        { "inspect.S-1.near", Au + "lab/SFX_LAB_MannequinNeckTurn.wav", "1" },
        { "model.neck", Au + "lab/SFX_LAB_MannequinNeckTurn.wav", "1" },   // 64차: 3일차부터 오래 바라보면 과학실 모형의 목이 꺾인다(ScienceModel)
        { "inspect.S-3.near", Au + "toilet/SFX_TOILET_DrainBreath.wav", "1" },
        { "inspect.T-2.near", Au + "toilet/SFX_TOILET_DoorClose.wav", "1" },
        { "inspect.C-5.near", Au + "class/SFX_CLASS_TeacherChair.wav", "0.9" },   // 66차
        { "inspect.K-2.near", Au + "hall/SFX_HALL_ChairDrag.wav", "0.8" },
        { "inspect.H-6.near", Au + "hall/SFX_HALL_ChairDrag.wav", "0.8" },
        { "inspect.L-4.near", Au + "class/SFX_CLASS_ChalkStroke.wav", "1" },
        { "inspect.S-4.near", Au + "lab/SFX_LAB_Scrape.wav", "0.8" },

        // 플레이어 발소리(PlayerFootsteps) — 바닥별 걷기·뛰기. 과학실·경비실은 FootstepSplitter가 한 걸음씩 자른 것
        { "step.CLASS.walk", Au + "footsteps/SFX_STEP_CLASS_Wood_Walk_*", "1" },
        { "step.CLASS.run", Au + "footsteps/SFX_STEP_CLASS_Wood_Run_*", "1" },
        { "step.HALL.walk", Au + "footsteps/SFX_STEP_GUARD_Walk_*", "1" },   // 61차(민: 「복도 발소리 경비실 발소리랑 같게」) — 옛 타일 판은 footsteps/SFX_STEP_HALL_Tile_*
        { "step.HALL.run", Au + "footsteps/SFX_STEP_GUARD_Run_*", "1" },
        { "step.LIBRARY.walk", Au + "footsteps/SFX_STEP_LIBRARY_Walk_*", "1" },
        { "step.LIBRARY.run", Au + "footsteps/SFX_STEP_LIBRARY_Run_*", "1" },
        { "step.TOILET.walk", Au + "footsteps/SFX_STEP_TOILET_WetTile_Walk_*", "1" },
        { "step.TOILET.run", Au + "footsteps/SFX_STEP_TOILET_WetTile_Run_*", "1" },
        { "step.LAB.walk", Au + "footsteps/SFX_STEP_LAB_Walk_*", "1" },
        { "step.LAB.run", Au + "footsteps/SFX_STEP_LAB_Run_*", "1" },
        { "step.GUARD.walk", Au + "footsteps/SFX_STEP_GUARD_Walk_*", "1" },
        { "step.GUARD.run", Au + "footsteps/SFX_STEP_GUARD_Run_*", "1" }
    };

    [MenuItem("야간근무/연출/소리 표만 다시 만들기")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
        AssetDatabase.SaveAssets();
    }

    /// <summary>표를 비우고 <see cref="Map"/>으로 다시 채운다. 보고 문자열을 돌려준다.</summary>
    public static string Build()
    {
        DirectionSoundTableSO table = AssetDatabase.LoadAssetAtPath<DirectionSoundTableSO>(TablePath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<DirectionSoundTableSO>();
            AssetDatabase.CreateAsset(table, TablePath);
        }

        table.Entries.Clear();
        int ok = 0;
        int clips = 0;
        StringBuilder missing = new StringBuilder();
        for (int i = 0; i < Map.GetLength(0); i++)
        {
            List<AudioClip> found = Load(Map[i, 1]);
            if (found.Count == 0)
            {
                missing.Append(Map[i, 0]).Append(' ');
                continue;
            }

            float[] hits = new float[found.Count];
            for (int k = 0; k < found.Count; k++) hits[k] = PeakSeconds(AssetDatabase.GetAssetPath(found[k]));
            DirectionSoundTableSO.Entry e = new DirectionSoundTableSO.Entry
            {
                key = Map[i, 0],
                clip = found[0],
                more = found.GetRange(1, found.Count - 1).ToArray(),
                volume = float.Parse(Map[i, 2], CultureInfo.InvariantCulture),
                hits = hits
            };
            table.Entries.Add(e);
            ok++;
            clips += found.Count;
        }

        EditorUtility.SetDirty(table);
        return "✓ 소리 표 " + ok + "개(클립 " + clips + ")" + (missing.Length > 0 ? " (클립 없음: " + missing + ")" : string.Empty);
    }

    /// <summary>파일에서 가장 큰 지점(초). WAV가 아니거나 못 읽으면 0. 짧은 소리는 앞쪽이라 대개 0 근처다.</summary>
    public static float PeakSeconds(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)) return 0f;
        float[] x;
        int rate;
        if (!FootstepSplitter.ReadWav(assetPath, out x, out rate) || rate <= 0) return 0f;
        float peak = 0f;
        int at = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float v = Mathf.Abs(x[i]);
            if (v > peak)
            {
                peak = v;
                at = i;
            }
        }

        return at / (float)rate;
    }

    /// <summary>경로 하나(<c>_*</c>면 여러 판)의 클립들.</summary>
    public static List<AudioClip> Load(string path)
    {
        List<AudioClip> list = new List<AudioClip>();
        if (!path.EndsWith("_*", System.StringComparison.Ordinal))
        {
            AudioClip one = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (one != null) list.Add(one);
            return list;
        }

        string stem = path.Substring(0, path.Length - 1);
        for (int n = 1; n <= 9; n++)
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(stem + n + ".wav");
            if (c == null) c = AssetDatabase.LoadAssetAtPath<AudioClip>(stem + n.ToString("00") + ".wav");   // _01·_02 식 이름
            if (c == null) break;
            list.Add(c);
        }

        return list;
    }
}

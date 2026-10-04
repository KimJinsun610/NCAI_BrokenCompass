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
        { "H2.cue", Au + "hall/SFX_HALL_DoorOpenSlow.wav", "0.9" },
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
        { "finale.letmein", Au + "voice/VO_LetMeIn.wav", "0.9" },
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

        // 긴장 — 강도 3 이상 조우의 대면에 작게 까는 떨리는 현(NightDutySfx)
        { "tension.confront", Au + "climax/CLX-29_1.wav", "0.4" },

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
        { "ui.confirm", Au + "ui/SFX_UI_ReportHoldConfirm.wav", "0.7" },

        // 점검 이상 — 보고할 때까지 그 자리에서 루프(NightDutySfx)
        { "inspect.C-3.loop", Au + "class/SFX_CLASS_CeilingCrawl.wav", "0.8" },
        { "inspect.H-2.loop", Au + "hall/SFX_HALL_FountainTrickle_1.wav", "0.7" },
        { "inspect.H-3.loop", Au + "hall/SFX_HALL_CheckBellHum.wav", "0.7" },
        { "inspect.L-3.loop", Au + "library/SFX_LIBRARY_PageTurn.wav", "0.7" },
        { "inspect.S-3.loop", Au + "lab/SFX_LAB_SinkDrip_1.wav", "0.8" },
        { "inspect.T-2.loop", Au + "toilet/SFX_TOILET_PaperTear.wav", "0.7" },

        // 점검 「가까이」(NightDutySfx) — +는 0.6초 뒤 이어서, inspect.near는 공용 충격음
        { "inspect.near", Au + "creature/SFX_STINGER_CloseImpact_*", "0.8" },
        { "inspect.H-1.near", Au + "hall/SFX_HALL_PowderPuff.wav", "1" },
        { "inspect.H-2.near", Au + "toilet/SFX_TOILET_DrainBreath.wav", "1" },
        { "inspect.H-3.near", Au + "hall/SFX_HALL_BellMountCreak_*", "1" },
        { "inspect.H-4.near", Au + "hall/SFX_HALL_LockerSlam_*", "1" },
        { "inspect.C-2.near", Au + "class/SFX_CLASS_LampOff_*", "1" },
        { "inspect.C-2.near+", Au + "hall/SFX_HALL_ChairDrag.wav", "0.9" },
        { "inspect.L-1.near", Au + "hall/SFX_HALL_ChairDrag.wav", "0.9" },
        { "inspect.S-1.near", Au + "lab/SFX_LAB_MannequinNeckTurn.wav", "1" },
        { "inspect.S-3.near", Au + "toilet/SFX_TOILET_DrainBreath.wav", "1" },
        { "inspect.T-2.near", Au + "toilet/SFX_TOILET_DoorClose.wav", "1" },

        // 플레이어 발소리(PlayerFootsteps) — 바닥별 걷기·뛰기. 과학실·경비실은 FootstepSplitter가 한 걸음씩 자른 것
        { "step.CLASS.walk", Au + "footsteps/SFX_STEP_CLASS_Wood_Walk_*", "1" },
        { "step.CLASS.run", Au + "footsteps/SFX_STEP_CLASS_Wood_Run_*", "1" },
        { "step.HALL.walk", Au + "footsteps/SFX_STEP_HALL_Tile_Walk_*", "1" },
        { "step.HALL.run", Au + "footsteps/SFX_STEP_HALL_Tile_Run_*", "1" },
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

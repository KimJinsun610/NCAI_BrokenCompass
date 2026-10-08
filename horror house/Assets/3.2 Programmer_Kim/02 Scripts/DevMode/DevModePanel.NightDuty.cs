#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using NightDuty;
using UnityEngine;

/// <summary>
/// 개발자 모드 패널의 야간근무 탭(2026-10-08 64차, 이성현 — 민: 「디버그 모드는 진선님 것에 합치고 우리 것(F3 콘솔)은 폐지 · 축 변화가 잘 보이게 · 연출을 의도한 흐름대로 다 확인」).
/// <list type="bullet">
/// <item><b>축</b>: 패널 위쪽 띠(늘 보임) + ③ 탭의 큰 막대. 막대 색 = 그 값의 구간, 흰 눈금 = 구간 경계(감각 25·50·75·90 / 신뢰 15·30·45·65),
/// 방금 바뀐 축은 1.5초 동안 테두리와 「+7」이 깜빡인다. 변화 기록은 같은 축·같은 출처가 1.5초 안에 이어지면 한 줄로 묶는다(응시처럼 0.1초마다 오르는 것).</item>
/// <item><b>조우</b>: 오늘 편성(슬롯 A·B·C)마다 단계 사슬 대기 › 전조 › 세움(보면 시작) › 대면 › 마무리 › 끝 — 지금 단계가 노랗게.
/// 「그 시각·자리로」 = 밤 시계를 슬롯 시작으로 옮기고 방아쇠 자리로 옮긴 뒤 패널을 닫는다 → 실제 발동 조건(머무름·깊이·시선)대로 연출이 난다.
/// 「흐름 확인」 = 그 자리로 옮기고 강제로 시작(전조부터) — 몹을 세운 연출은 패널이 닫힌 뒤 몹을 바라봐야 대면이 시작된다.</item>
/// </list>
/// </summary>
public sealed partial class DevModePanel
{
    private static readonly string[] AxisNames = { "청각", "조도", "배치", "신뢰" };
    private static readonly string[] AxisHex = { "#7ac0ff", "#ffd95a", "#d58cff", "#8be39c" };
    private static readonly int[] SenseTicks = { 25, 50, 75, 90 };
    private static readonly int[] TrustTicks = { 15, 30, 45, 65 };
    private static readonly SpaceId[] TeleportSpaces =
    {
        SpaceId.SecurityRoom, SpaceId.Corridor, SpaceId.Classroom, SpaceId.ScienceRoom, SpaceId.Toilet, SpaceId.Library
    };

    private const float FlashSeconds = 1.5f;
    private const int MaxAxisLog = 80;

    private sealed class AxisEntry
    {
        public FearAxis Axis;
        public int From;
        public int To;
        public string Source;
        public int Minute;
        public float Updated;
    }

    private readonly int[] axisLast = { -1, -1, -1, -1 };
    private readonly float[] axisChangedAt = { -99f, -99f, -99f, -99f };
    private readonly int[] axisFlashDelta = new int[4];
    private readonly string[] axisPendingSource = new string[4];
    private readonly List<AxisEntry> axisLog = new List<AxisEntry>();
    private FearAxisSystem hookedAxes;

    private bool closeOnRun = true;
    private bool clockHeld;
    private bool runningSignal;
    private int captureRepeat = 1;

    private Action afterSettle;
    private float settleAt;
    private int settleFrame;
    private SpaceId settleSpace = SpaceId.None;

    // ─────────────────────────────── 기록 연결 ───────────────────────────────

    private void HookNightDuty(bool on)
    {
        EventBus.DirectionEmitted -= OnDirection;
        EventBus.FinalRuleSettled -= OnRule;
        EventBus.InspectionReported -= OnReport;
        EventBus.NightRestarted -= OnRestart;
        if (!on)
        {
            if (hookedAxes != null) hookedAxes.Raised -= OnRaised;
            hookedAxes = null;
            return;
        }

        EventBus.DirectionEmitted += OnDirection;
        EventBus.FinalRuleSettled += OnRule;
        EventBus.InspectionReported += OnReport;
        EventBus.NightRestarted += OnRestart;
    }

    private void OnDirection(DirectionEvent e)
    {
        if (e.Kind == DirectionEventKind.Note && e.Text.StartsWith("예산")) return;
        AddLog("<color=#9cf>연출</color> " + Escape(e.ToString()));
    }

    private void OnRule(FinalRuleResult r)
    {
        string color = r.Outcome == FinalOutcome.Violated ? "#f77" : r.Outcome == FinalOutcome.Noted ? "#ccc" : "#7f7";
        AddLog("<color=" + color + ">수칙</color> " + Escape(r.ToString()));
    }

    private void OnReport(InspectionReport r)
    {
        AddLog("<color=#fd7>점검</color> " + r.ItemId + " " + (r.SaidAnomaly ? "[이상]" : "[정상]") + " → " + r.Outcome + " " + r.Axis + " " + r.Change);
    }

    private void OnRestart(RestartResult r)
    {
        AddLog("<color=#f9f>재시작</color> " + r.Kind + " k=" + r.K + " 시작 " + r.StartMinute + "분");
    }

    private void OnRaised(FearAxis axis, int delta, string source)
    {
        int i = (int)axis;
        if (i >= 0 && i < 4) axisPendingSource[i] = source;
    }

    // ─────────────────────────────── 축 추적 ───────────────────────────────

    /// <summary>매 프레임 네 축을 읽어 바뀐 만큼 기록한다(출처는 FearAxisSystem.Raised가 남긴 것 — 없으면 내림·되돌림).</summary>
    private void TrackAxes()
    {
        if (NightRun.Day <= 0) return;
        IFearAxisReader reader = NightRun.Axes;
        FearAxisSystem sys = reader as FearAxisSystem;
        if (sys != hookedAxes)
        {
            if (hookedAxes != null) hookedAxes.Raised -= OnRaised;
            hookedAxes = sys;
            if (sys != null) sys.Raised += OnRaised;
            for (int i = 0; i < 4; i++) axisLast[i] = -1;
        }

        for (int i = 0; i < 4; i++)
        {
            int v = reader.GetValue((FearAxis)i);
            if (axisLast[i] < 0)
            {
                axisLast[i] = v;
                continue;
            }

            if (v == axisLast[i]) continue;
            string source = axisPendingSource[i];
            axisPendingSource[i] = null;
            if (string.IsNullOrEmpty(source)) source = v < axisLast[i] ? "내림·되돌림" : "?";
            RecordAxis((FearAxis)i, axisLast[i], v, source);
            axisLast[i] = v;
        }
    }

    private void RecordAxis(FearAxis axis, int from, int to, string source)
    {
        int i = (int)axis;
        float now = Time.unscaledTime;
        bool fresh = now - axisChangedAt[i] > FlashSeconds;
        axisFlashDelta[i] = fresh ? to - from : axisFlashDelta[i] + (to - from);
        axisChangedAt[i] = now;

        AxisEntry last = axisLog.Count > 0 ? axisLog[axisLog.Count - 1] : null;
        if (last != null && last.Axis == axis && last.Source == source && now - last.Updated < FlashSeconds)
        {
            last.To = to;
            last.Updated = now;
            return;
        }

        axisLog.Add(new AxisEntry { Axis = axis, From = from, To = to, Source = source, Minute = NightRun.NightMinute, Updated = now });
        while (axisLog.Count > MaxAxisLog) axisLog.RemoveAt(0);
    }

    private static string SourceLabel(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        if (id == "debug") return "디버그";
        string s = CaptureDirector.SourceName(id);
        if (s.Length > 40) s = s.Substring(0, 39) + "…";
        return s == id ? id : id + " · " + s;
    }

    private static Color BandColor(Band band)
    {
        switch (band)
        {
            case Band.Band0: return new Color(0.45f, 0.62f, 0.5f);
            case Band.Band1: return new Color(0.9f, 0.82f, 0.35f);
            case Band.Band2: return new Color(0.98f, 0.6f, 0.25f);
            case Band.Band3: return new Color(0.95f, 0.32f, 0.25f);
            default: return new Color(1f, 0.12f, 0.2f);
        }
    }

    /// <summary>막대 하나 — 배경 · 값(구간 색) · 구간 경계 눈금 · 방금 바뀌었으면 밝은 테두리.</summary>
    private void DrawBar(Rect r, FearAxis axis, int value)
    {
        Color saved = GUI.color;
        GUI.color = new Color(0.22f, 0.22f, 0.25f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        Band band = Bands.OfAxis(axis, value);
        GUI.color = BandColor(band);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value / 100f), r.height), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        int[] ticks = axis == FearAxis.Trust ? TrustTicks : SenseTicks;
        for (int t = 0; t < ticks.Length; t++)
        {
            float x = r.x + r.width * ticks[t] / 100f;
            GUI.DrawTexture(new Rect(x, r.y, 1f, r.height), Texture2D.whiteTexture);
        }

        int i = (int)axis;
        float age = Time.unscaledTime - axisChangedAt[i];
        if (age < FlashSeconds)
        {
            float a = 1f - age / FlashSeconds;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
        }

        GUI.color = saved;
    }

    private string FlashText(FearAxis axis)
    {
        int i = (int)axis;
        if (Time.unscaledTime - axisChangedAt[i] >= FlashSeconds || axisFlashDelta[i] == 0) return string.Empty;
        int d = axisFlashDelta[i];
        return d > 0 ? "<color=#ff6b6b><b>+" + d + "</b></color>" : "<color=#7fd0ff><b>" + d + "</b></color>";
    }

    /// <summary>패널 위쪽에 늘 보이는 네 축 띠.</summary>
    private void DrawAxisStrip()
    {
        if (NightRun.Day <= 0)
        {
            GUILayout.Label("<color=#999>근무 밤이 아닙니다 — 축 없음</color>", small);
            return;
        }

        IFearAxisReader axes = NightRun.Axes;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++)
        {
            FearAxis ax = (FearAxis)i;
            int v = axes.GetValue(ax);
            GUILayout.BeginVertical(GUILayout.Width(116f));
            GUILayout.Label("<color=" + AxisHex[i] + ">" + AxisNames[i] + "</color> <b>" + v + "</b> " + FlashText(ax), rich);
            Rect r = GUILayoutUtility.GetRect(110f, 9f, GUILayout.Width(110f), GUILayout.Height(9f));
            if (Event.current.type == EventType.Repaint) DrawBar(r, ax, v);
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
    }

    private static string NightStatusLine()
    {
        if (NightRun.Day <= 0) return string.Empty;
        int minute = NightRun.NightMinute;
        string s = "밤 " + Clock(minute) + (minute >= 0 ? " " + PhaseName(minute) : string.Empty)
                   + " · " + (NightRun.IsJudgingNow ? "<color=#7f7>판정 중</color>" : "<color=#f77>판정 정지</color>")
                   + " · " + SpaceName(NightRun.CurrentSpace);
        if (!NightRun.IsNightActive) s += " · <color=#f77>밤 닫힘</color>";
        if (NightRun.IsCaptured) s += " · <color=#f44>붙잡힘</color>";
        return s;
    }

    private static string PhaseName(int minute)
    {
        if (minute < NightClock.JudgingStart) return "(준비)";
        if (minute < NightClock.Call1) return "(판정)";
        if (minute < NightClock.RelaxStart) return "(슬롯 A)";
        if (minute < NightClock.Call2) return "(이완)";
        if (minute < NightClock.SlotCStart) return "(슬롯 B)";
        if (minute < NightClock.JudgingEnd) return "(슬롯 C)";
        return "(마감)";
    }

    // ─────────────────────────────── ③ 축 ───────────────────────────────

    private void DrawAxesTab()
    {
        if (NightRun.Day <= 0)
        {
            GUILayout.Label("근무 밤이 아닙니다.", small);
            return;
        }

        IFearAxisReader axes = NightRun.Axes;
        IFearAxisReader shown = NightRun.Shown;
        BeginSection("네 축", "막대 색 = 그 값의 구간(회녹 0 · 노랑 1 · 주황 2 · 빨강 3 · 진홍 4), 흰 눈금 = 구간 경계. 100이면 붙잡힘(신뢰는 붙잡지 않음). 「연출 구간」은 일차 하한·래칫이 걸린, 연출이 실제로 쓰는 구간.");
        for (int i = 0; i < 4; i++)
        {
            FearAxis ax = (FearAxis)i;
            int v = axes.GetValue(ax);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=" + AxisHex[i] + "><b>" + AxisNames[i] + "</b></color>", rich, GUILayout.Width(40f));
            Rect r = GUILayoutUtility.GetRect(250f, 20f, GUILayout.Width(250f), GUILayout.Height(20f));
            if (Event.current.type == EventType.Repaint) DrawBar(r, ax, v);
            GUILayout.Label("<b>" + v + "</b> " + FlashText(ax), rich, GUILayout.Width(70f));
            GUILayout.Label("<color=#aaa>구간 " + (int)Bands.OfAxis(ax, v) + " · 연출 " + (int)shown.GetBand(ax) + "</color>", small);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(44f);
            if (GUILayout.Button("−10", GUILayout.Width(48))) Later(() => NightRun.DebugLowerAxis(ax, 10));
            if (GUILayout.Button("+5", GUILayout.Width(48))) Later(() => NightRun.DebugAddAxis(ax, 5));
            if (GUILayout.Button("+10", GUILayout.Width(48))) Later(() => NightRun.DebugAddAxis(ax, 10));
            if (GUILayout.Button("+25", GUILayout.Width(48))) Later(() => NightRun.DebugAddAxis(ax, 25));
            if (ax != FearAxis.Trust && GUILayout.Button("다음 구간", GUILayout.Width(76))) Later(() => ToNextBand(ax));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
        EndSection();

        BeginSection("변화 기록 (최신이 위)", "같은 축·같은 출처가 1.5초 안에 이어지면 한 줄로 묶습니다(응시처럼 계속 오르는 것). 출처가 「?」면 디렉터·연출이 직접 올린 것.");
        if (GUILayout.Button("지우기", GUILayout.Width(60))) Later(() => axisLog.Clear());
        if (axisLog.Count == 0) GUILayout.Label("<color=#999>아직 변화 없음</color>", small);
        for (int i = axisLog.Count - 1; i >= 0 && i >= axisLog.Count - 40; i--)
        {
            AxisEntry e = axisLog[i];
            int d = e.To - e.From;
            string delta = d >= 0 ? "<color=#ff8080><b>+" + d + "</b></color>" : "<color=#80c8ff><b>" + d + "</b></color>";
            GUILayout.Label("<color=#888>[" + Clock(e.Minute) + "]</color> <color=" + AxisHex[(int)e.Axis] + ">" + AxisNames[(int)e.Axis] + "</color> "
                            + e.From + " → " + e.To + " " + delta + "  <color=#ccc>" + Escape(SourceLabel(e.Source)) + "</color>", small);
        }
        EndSection();
    }

    private void ToNextBand(FearAxis axis)
    {
        int v = NightRun.Axes.GetValue(axis);
        int[] ticks = SenseTicks;
        for (int t = 0; t < ticks.Length; t++)
        {
            if (v < ticks[t])
            {
                NightRun.DebugAddAxis(axis, ticks[t] - v);
                return;
            }
        }

        Note(AxisNames[(int)axis] + "은 이미 마지막 구간입니다");
    }

    // ─────────────────────────────── ① 일차 — 야간근무 ───────────────────────────────

    private void ReopenAsDay(int day)
    {
        NightRunDriver d = NightRunDriver.Current;
        if (d == null)
        {
            Note("밤 구동기가 없습니다(근무 씬이 아님)");
            return;
        }

        d.DebugRestartAsDay(day);
        Note(day + "일차로 이 자리에서 다시 열었습니다");
    }

    private void DrawCaptureButtons()
    {
        GUILayout.BeginHorizontal();
        foreach (FearAxis axis in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout })
        {
            FearAxis a = axis;
            if (GUILayout.Button("붙잡힘: " + AxisNames[(int)a])) Later(() =>
            {
                SetShow(false);
                NightRun.DebugForceCapture(a);
            });
        }
        if (GUILayout.Button("붙잡힘: 인체 모형 응시")) Later(() =>
        {
            SetShow(false);
            NightRun.DebugForceCapture(FixedMobStare.Axis, FixedMobStare.SourcePrefix + FinalCues.ModelTarget);
        });
        GUILayout.EndHorizontal();
    }

    private void DrawNightControls()
    {
        if (NightRun.Day <= 0) return;

        BeginSection("밤");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("밤 종료 요청(04:00 정산)")) Later(() => NightRun.RequestEndNight());
        if (GUILayout.Button("붙잡힌 뒤 재시작")) Later(() => NightRun.RestartAfterCapture());
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        FlashlightRelay light = FlashlightRelay.Active;
        if (GUILayout.Button(light != null && light.IsOn ? "손전등 끄기" : "손전등 켜기") && light != null) Later(() => light.SetOn(!light.IsOn));
        if (GUILayout.Button(runningSignal ? "달리기 신호 끄기" : "달리기 신호 켜기")) Later(() =>
        {
            runningSignal = !runningSignal;
            NightRun.Send(JudgeSignal.Run(runningSignal));
        });
        GUILayout.EndHorizontal();

        FlashlightBattery battery = NightRun.Battery;
        if (battery != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(battery.ToString(), small, GUILayout.Width(150f));
            if (GUILayout.Button("100%")) Later(() => battery.DebugSet(1f, battery.Spare));
            if (GUILayout.Button("25%")) Later(() => battery.DebugSet(0.25f, battery.Spare));
            if (GUILayout.Button("8%")) Later(() => battery.DebugSet(0.08f, battery.Spare));
            if (GUILayout.Button("0%")) Later(() => battery.DebugSet(0f, battery.Spare));
            if (GUILayout.Button("+예비")) Later(() => battery.DebugSet(battery.Charge, battery.Spare + 1));
            GUILayout.EndHorizontal();
        }
        EndSection();

        BeginSection("공간으로 이동");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TeleportSpaces.Length; i++)
        {
            SpaceId s = TeleportSpaces[i];
            if (GUILayout.Button(SpaceName(s))) Later(() => TeleportToSpace(s));
        }
        GUILayout.EndHorizontal();
        EndSection();
    }

    // ─────────────────────────────── ② 시간 — 밤 시계 ───────────────────────────────

    private void DrawNightClockControls()
    {
        if (NightRun.Day <= 0) return;

        BeginSection("밤 시계 구간으로", "게임 시간과 같은 시계를 밤 구간 기준으로 옮깁니다. 판정은 00:16~03:30(이완 제외)에만 합니다.");
        GUILayout.BeginHorizontal();
        JumpButton("00:16 판정", NightClock.JudgingStart + 1);
        JumpButton("01:00 슬롯A", NightClock.Call1 + 1);
        JumpButton("01:52 이완", NightClock.RelaxStart + 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        JumpButton("02:16 슬롯B", NightClock.Call2 + 1);
        JumpButton("03:08 슬롯C", NightClock.SlotCStart + 1);
        JumpButton("03:30 판정 끝", NightClock.JudgingEnd);
        if (GUILayout.Button("+5분")) Later(() => JumpNight(NightRun.NightMinute + 5));
        GUILayout.EndHorizontal();
        EndSection();

        BeginSection("스위치");
        GUILayout.BeginHorizontal();
        Toggle(!NightRun.JudgingWindowEnabled, "판정: 늘(시간창 무시)", "판정: 시간창대로", () => NightRun.JudgingWindowEnabled = !NightRun.JudgingWindowEnabled);
        Toggle(NightRun.DirectorAutoRun, "자동 연출: 켬", "자동 연출: 끔(버튼으로만)", () => NightRun.DirectorAutoRun = !NightRun.DirectorAutoRun);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        Toggle(clockHeld, "시계: 멈춤", "시계: 흐름", ToggleClock);
        Toggle(PlayerInteractor.IgnoreLocks, "잠긴 문 무시: 켬", "잠긴 문 무시: 끔", () => PlayerInteractor.IgnoreLocks = !PlayerInteractor.IgnoreLocks);
        Toggle(DoorPolicySO.OpenEverythingOverride, "모든 문 열기: 켬", "모든 문 열기: 끔", () => DoorPolicySO.OpenEverythingOverride = !DoorPolicySO.OpenEverythingOverride);
        GUILayout.EndHorizontal();
        EndSection();
    }

    private void Toggle(bool on, string onLabel, string offLabel, Action flip)
    {
        Color saved = GUI.backgroundColor;
        if (on) GUI.backgroundColor = SelectedItem;
        if (GUILayout.Button(on ? onLabel : offLabel)) Later(flip);
        GUI.backgroundColor = saved;
    }

    private void JumpButton(string label, int minute)
    {
        if (GUILayout.Button(label)) Later(() => JumpNight(minute));
    }

    private void JumpNight(int minute)
    {
        NightRunDriver d = NightRunDriver.Current;
        if (d == null)
        {
            Note("<color=#f77>밤 시계 구동기가 없습니다</color>");
            return;
        }

        d.DebugJumpToNightMinute(minute);
        Note("밤 시계 → " + Clock(minute));
    }

    private void ToggleClock()
    {
        NightRunDriver d = NightRunDriver.Current;
        if (d == null || d.Clock == null) return;
        clockHeld = !clockHeld;
        if (clockHeld) d.Clock.Hold(this);
        else d.Clock.Release(this);
    }

    // ─────────────────────────────── ④ 조우 ───────────────────────────────

    private void DrawEncounterTab()
    {
        TensionDirector t = NightRun.Tension;
        if (t == null)
        {
            GUILayout.Label("디렉터가 없습니다(근무 밤이 아니거나 새 편성 꺼짐).", small);
            return;
        }

        GUILayout.BeginHorizontal();
        Toggle(closeOnRun, "실행하면 패널 닫기: 켬", "실행하면 패널 닫기: 끔", () => closeOnRun = !closeOnRun);
        Toggle(NightRun.DirectorAutoRun, "자동 연출: 켬", "자동 연출: 끔", () => NightRun.DirectorAutoRun = !NightRun.DirectorAutoRun);
        GUILayout.EndHorizontal();

        BeginSection("오늘 흐름 (" + NightRun.Day + "일차 편성)",
            "「그 시각·자리로」 = 밤 시계를 슬롯 시작으로 옮기고 방아쇠 자리로 데려다 놓습니다 — 실제 조건(머무름·깊이·시선)대로 나는지 봅니다. 「바로」 = 조건을 건너뛰고 전조부터 시작.");
        for (int i = 0; i < t.Runs.Count; i++)
        {
            EncounterRun r = t.Runs[i];
            if (r.Def == null) continue;
            DrawRun(r, true);
        }
        if (t.Runs.Count == 0) GUILayout.Label("<color=#999>오늘 편성된 조우가 없습니다.</color>", small);
        EndSection();

        BeginSection("지금 진행 중", "몹을 세운 연출(세움)은 패널이 닫힌 뒤 플레이어가 몹을 화면 안에서 0.35초 보면 대면이 시작됩니다.");
        bool any = false;
        for (int i = 0; i < t.Runs.Count; i++)
        {
            EncounterRun r = t.Runs[i];
            if (r.State == EncounterRunState.Waiting || r.State == EncounterRunState.Done || r.State == EncounterRunState.Missed) continue;
            any = true;
            GUILayout.Label("<b>" + Escape(r.Def != null ? r.Def.Name : "?") + "</b>  " + Chain(r), small);
        }
        if (!any) GUILayout.Label("<color=#999>없음</color>", small);
        if (t.RuleRuns.Count > 0)
        {
            string s = "수칙 단서: ";
            for (int i = 0; i < t.RuleRuns.Count; i++)
            {
                RuleTriggerRun r = t.RuleRuns[i];
                s += r.Script.RuleId + (r.Done ? "(끝) " : r.Running ? "<color=#fd7>(울림)</color> " : "(대기 " + r.NeedDwell.ToString("0") + "초) ");
            }
            GUILayout.Label(s, small);
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("다음 단계로")) Later(NightRun.DebugSkipDirectionPhase);
        if (GUILayout.Button("무대 정리")) Later(() =>
        {
            if (DirectionStage.Active != null) DirectionStage.Active.ClearAll(DirectionPhase.Aborted);
        });
        GUILayout.EndHorizontal();
        GUILayout.Label("<color=#999>디렉터 " + t.Mood + " · 예산 " + Escape(t.Budget.Usage.ToString()) + " · 가짜 놀람 " + t.FakesUsed + " · 긴장 " + Escape(t.Pacer.ToString()) + "</color>", small);
        EndSection();

        BeginSection("조우 15개 (편성과 무관하게)", "「흐름 확인」 = 그 공간(고정 자리면 그 자리가 보이는 곳)으로 옮긴 뒤 전조부터 시작. 「여기서」 = 이 자리에서 시작.");
        foreach (EncounterDef e in ProgramCatalog.AllEncounters)
        {
            EncounterScript s = EncounterScripts.Find(e.Id);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + Escape(e.Name) + "</b>  <color=" + AxisHex[(int)e.Axis] + ">" + AxisNames[(int)e.Axis] + "</color> 강도 " + e.Intensity
                            + " · " + SpaceName(SpaceIds.Canonical(e.Space)) + " · 대응 " + e.ResponseRule + (e.IsCross ? " · 교차" : string.Empty), small, GUILayout.Width(300f));
            string id = e.Id;
            if (ColorButton("흐름 확인 ▶", ComplyColor, GUILayout.Width(84f))) Later(() => ForceEncounter(id, true));
            if (GUILayout.Button("여기서", GUILayout.Width(56f))) Later(() => ForceEncounter(id, false));
            GUILayout.EndHorizontal();
            if (s != null)
            {
                string how = TriggerHint(s);
                GUILayout.Label("   <color=#9a9a9a>" + Escape(how) + (s.Note.Length > 0 ? " — " + Escape(s.Note) : string.Empty) + "</color>", small);
            }
        }
        EndSection();
    }

    private void DrawRun(EncounterRun r, bool buttons)
    {
        int from, to;
        SlotWindow(r.Slot, out from, out to);
        string flags = (r.Forced ? " · 강제" : string.Empty) + (r.CarriedOver ? " · 이월" : string.Empty) + (r.FalseCount > 0 ? " · 헛예고 " + r.FalseCount : string.Empty);
        GUILayout.Label("<b>" + r.Slot + "</b> <color=#aaa>" + Clock(from) + "–" + Clock(to) + "</color>  <b>" + Escape(r.Def.Name) + "</b> · "
                        + SpaceName(SpaceIds.Canonical(r.Def.Space)) + flags, rich);
        GUILayout.Label(Chain(r), small);
        if (r.Script != null) GUILayout.Label("<color=#9a9a9a>방아쇠: " + Escape(TriggerHint(r.Script)) + (r.Waiting.Length > 0 ? " · 지금: " + Escape(r.Waiting) : string.Empty) + "</color>", small);
        if (buttons)
        {
            GUILayout.BeginHorizontal();
            string id = r.Def.Id;
            EncounterSlot slot = r.Slot;
            if (ColorButton("그 시각·자리로 ▶", ComplyColor)) Later(() => GoNatural(id, slot));
            if (GUILayout.Button("바로(전조부터)")) Later(() => ForceEncounter(id, true));
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(4f);
    }

    /// <summary>단계 사슬: 대기 › 전조 › 세움(보면 시작) › 대면 › 마무리 › 끝 — 지금 단계를 노랗게.</summary>
    private static string Chain(EncounterRun r)
    {
        if (r.State == EncounterRunState.Missed) return "<color=#f77><b>놓침</b></color>";
        bool sight = r.Script != null && r.Script.NeedsSight;
        string[] names = sight
            ? new[] { "대기", "전조", "세움(보면 시작)", "대면", "마무리", "끝" }
            : new[] { "대기", "전조", "대면", "마무리", "끝" };
        int at;
        switch (r.State)
        {
            case EncounterRunState.Waiting: at = 0; break;
            case EncounterRunState.Foreshadow: at = 1; break;
            case EncounterRunState.Presenting: at = 2; break;
            case EncounterRunState.Active: at = sight ? 3 : 2; break;
            case EncounterRunState.Releasing: at = sight ? 4 : 3; break;
            default: at = names.Length - 1; break;
        }

        string s = string.Empty;
        for (int i = 0; i < names.Length; i++)
        {
            if (i > 0) s += " <color=#666>›</color> ";
            if (i == at) s += "<color=#ffd24a><b>▶ " + names[i] + "</b></color>";
            else if (i < at) s += "<color=#7fbf7f>" + names[i] + "</color>";
            else s += "<color=#777>" + names[i] + "</color>";
        }
        return s;
    }

    private static string TriggerHint(EncounterScript s)
    {
        string where = SpaceName(SpaceIds.Canonical(s.ExactSpace != SpaceId.None ? s.ExactSpace : s.Space));
        if (!string.IsNullOrEmpty(s.GazeTargetId)) return where + "에서 대상을 바라보기";
        if (s.NearAnchor > 0f) return where + " 고정 자리 " + s.NearAnchor.ToString("0.#") + "m 안에 " + s.Dwell.ToString("0.#") + "초";
        if (s.DeepMargin > 0f) return where + " 안쪽(벽에서 " + s.DeepMargin.ToString("0.#") + "m 넘게)에 " + s.Dwell.ToString("0.#") + "초";
        if (s.Dwell > 0f) return where + "에 " + s.Dwell.ToString("0.#") + "초 머물기";
        return where + "에 들어가기";
    }

    private static void SlotWindow(EncounterSlot slot, out int from, out int to)
    {
        switch (slot)
        {
            case EncounterSlot.A: from = NightClock.Call1; to = NightClock.RelaxStart; break;
            case EncounterSlot.B: from = NightClock.Call2; to = NightClock.SlotCStart; break;
            default: from = NightClock.SlotCStart; to = NightClock.JudgingEnd; break;
        }
    }

    /// <summary>그 슬롯 시각으로 옮기고(이미 지났으면 그대로) 방아쇠 자리로 데려다 놓는다 — 연출은 실제 조건대로 디렉터가 연다.</summary>
    private void GoNatural(string id, EncounterSlot slot)
    {
        EncounterScript s = EncounterScripts.Find(id);
        if (s == null) return;
        NightRun.DirectorAutoRun = true;
        int from, to;
        SlotWindow(slot, out from, out to);
        if (NightRun.NightMinute < from || NightRun.NightMinute >= to) JumpNight(from + 1);
        MoveForEncounter(s);
        Note("자연 발동 대기: " + id + " — " + TriggerHint(s));
        if (closeOnRun) SetShow(false);
    }

    private void ForceEncounter(string id, bool move)
    {
        EncounterDef e = ProgramCatalog.Encounter(id);
        EncounterScript s = EncounterScripts.Find(id);
        if (e == null || s == null) return;

        // 판정 단서가 들어가도록 판정 책에 그 수칙이 없으면 붙인다.
        if (!string.IsNullOrEmpty(e.ResponseRule)) NightRun.DebugAddFinalRule(e.ResponseRule);
        if (!string.IsNullOrEmpty(e.SecondRule)) NightRun.DebugAddFinalRule(e.SecondRule);

        Action run = () =>
        {
            if (NightRun.DebugForceEncounter(id)) Note("조우 시작: " + e.Name + (s.NeedsSight ? " — 몹을 보면 대면" : string.Empty));
            else Note("<color=#f77>조우 실행 실패: " + id + "</color>");
        };

        if (closeOnRun) SetShow(false);
        if (!move)
        {
            run();
            return;
        }

        // 옮긴 뒤 센서가 새 자리·공간을 한두 번 샘플할 때까지 기다린다(0.1초 샘플) — 바로 실행하면 옛 자리로 판정한다.
        MoveForEncounter(s);
        afterSettle = run;
        settleAt = Time.unscaledTime + 0.35f;
        settleFrame = Time.frameCount;
        settleSpace = s.Space;
    }

    private void MoveForEncounter(EncounterScript s)
    {
        SpaceId room = s.ExactSpace != SpaceId.None ? s.ExactSpace : s.Space;
        StageAnchor fixedAt = StageAnchor.Find(s.StageAnchor);
        if (fixedAt != null) TeleportToView(fixedAt, room);   // 고정 자리 몹은 그 자리가 보이는 곳으로.
        else TeleportToSpace(room);
    }

    private void RunAfterSettle()
    {
        if (afterSettle == null || !Settled()) return;
        Action s = afterSettle;
        afterSettle = null;
        try { s(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    /// <summary>옮긴 뒤 디렉터가 새 공간을 알게 됐는지(센서 샘플 3프레임 이상) — 2초가 지나면 그냥 진행.</summary>
    private bool Settled()
    {
        if (Time.unscaledTime > settleAt + 2f) return true;
        if (Time.frameCount < settleFrame + 3 || Time.unscaledTime < settleAt) return false;
        TensionDirector t = NightRun.Tension;
        return settleSpace == SpaceId.None || t == null || t.Space == SpaceIds.Canonical(settleSpace);
    }

    // ─────────────────────────────── ⑤ 장면 ───────────────────────────────

    private void DrawSceneTab()
    {
        DrawScienceModel();
        DrawCapturePreview();
        DrawFakes();
        DrawFinale();
    }

    private void DrawScienceModel()
    {
        ScienceModel m = ScienceModel.Active;
        BeginSection("과학실 인체 모형", "오래 보면 배치가 오름(유예 1일 2.5 · 2일 2 · 3일~ 1.5초) → 붙잡히면 인체 모형 컷신. 3일차부터 2.5초 보면 목이 꺾임. 2일차부터 안 보는 사이 다가옴.");
        if (m == null || m.Model == null)
        {
            GUILayout.Label("<color=#999>모형이 없습니다(근무 씬이 아님).</color>", small);
            EndSection();
            return;
        }

        FixedMobStare stare = NightRun.Stare;
        GUILayout.Label("자리 <b>" + m.Spot + "</b> (그 밤 " + m.BaseSpot + "~" + m.MaxSpot + ") · " + (m.Visible ? "보임" : "<color=#f77>숨음</color>")
                        + " · 목 " + (m.NeckTurned ? "<color=#ffd24a>꺾임</color>" : "그대로") + " · 다가선 걸음 " + m.Creeps, small);
        GUILayout.Label("응시: " + (stare.TargetId.Length > 0 ? Escape(stare.TargetId) + " " + stare.Seconds.ToString("0.0") + "초" : "없음")
                        + " · 유예 " + FixedMobStare.GraceFor(NightRun.Day).ToString("0.0") + "초 · 판정 " + (NightRun.FixedMobStareEnabled ? "켬" : "<color=#f77>끔</color>"), small);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < ScienceModel.SpotCount; i++)
        {
            int spot = i;
            if (GUILayout.Button("자리 " + i)) Later(() => m.DebugSpot(spot));
        }
        if (ColorButton("모형 앞으로", ComplyColor)) Later(() =>
        {
            TeleportNear(m.Model.transform.position, m.Spot >= 3 ? SpaceId.Corridor : SpaceId.ScienceRoom, 3f);
            if (closeOnRun) SetShow(false);
        });
        GUILayout.EndHorizontal();
        Toggle(NightRun.FixedMobStareEnabled, "응시 판정: 켬", "응시 판정: 끔", () => NightRun.FixedMobStareEnabled = !NightRun.FixedMobStareEnabled);
        EndSection();
    }

    private void DrawCapturePreview()
    {
        BeginSection("붙잡힘 장면 미리 보기 (" + captureRepeat + "회차)", "재시작·카드 없이 장면만. 2회차 = 짧게, 3회차 = 아무 키로 건너뛰기.");
        GUILayout.BeginHorizontal();
        foreach (FearAxis axis in new[] { FearAxis.Auditory, FearAxis.Illuminance, FearAxis.Layout })
        {
            FearAxis a = axis;
            if (GUILayout.Button(AxisNames[(int)a])) Later(() => PreviewCapture(a, null));
        }
        if (GUILayout.Button("인체 모형 응시")) Later(() => PreviewCapture(FixedMobStare.Axis, FixedMobStare.SourcePrefix + FinalCues.ModelTarget));
        if (GUILayout.Button("회차 바꾸기")) Later(() => captureRepeat = captureRepeat % 3 + 1);
        GUILayout.EndHorizontal();
        EndSection();
    }

    private void PreviewCapture(FearAxis axis, string source)
    {
        CaptureDirector cd = CaptureDirector.Active;
        if (cd == null || !cd.Preview(axis, captureRepeat, source))
        {
            Note("붙잡힘 장면 미리 보기 실패(연출기 없음 또는 진행 중)");
            return;
        }

        SetShow(false);
        StartCoroutine(LogWhenDone(cd));
    }

    private IEnumerator LogWhenDone(CaptureDirector cd)
    {
        yield return null;
        while (cd != null && cd.IsPlaying) yield return null;
        if (cd != null) Note("붙잡힘 장면: " + cd.LastScene);
    }

    private void DrawFakes()
    {
        BeginSection("가짜 놀람 (여기서, 예산 안 씀)");
        GUILayout.BeginHorizontal();
        foreach (string fake in TensionDirector.FakeScares)
        {
            string id = fake;
            if (GUILayout.Button(FakeName(id))) Later(() => Note(NightRun.DebugForceFake(id) ? "가짜 놀람: " + FakeName(id) : "가짜 놀람 실패: " + id));
        }
        GUILayout.EndHorizontal();
        EndSection();
    }

    private void DrawFinale()
    {
        FinaleDirector fd = FinaleDirector.Active;
        FinaleWatch fw = NightRun.Finale;
        string state = fd != null && fd.IsRunning ? fd.Phase + " · 시도 " + fw.Attempt + " · 봤다 " + (fw.Seen ? "예" : "아니오") + " · CCTV " + fw.ChannelsSeen + "채널" : "대기";
        if (NightRun.LastFinaleEnding != FinaleEnding.None) state += " · 지난 결말 " + NightRun.LastFinaleEnding;
        BeginSection("피날레 (5일차)", state);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("5일차로 다시 열기")) Later(() => ReopenAsDay(FinaleWatch.Day));
        if (GUILayout.Button("피날레 시작")) Later(() => Note(fd != null && fd.Begin() ? "피날레 시작" : "피날레를 열 수 없음(5일차 밤이 아님·진행 중)"));
        if (GUILayout.Button("CCTV 건너뛰기")) Later(() =>
        {
            if (fd != null) fd.DebugSkipCctv();
        });
        if (GUILayout.Button("창을 봤다(비춤)")) Later(() => NightRun.Send(JudgeSignal.Beam(FinaleWatch.WindowTarget, 0.1f)));
        GUILayout.EndHorizontal();

        DirectionStage stage = DirectionStage.Active;
        foreach (FinaleRole role in new[] { FinaleRole.WindowMan, FinaleRole.SeatFigure })
        {
            FinaleRole r = role;
            FinaleMob mob = stage != null ? stage.FinaleOf(r) : null;
            string ms = mob == null ? "없음" : (mob.HasArt ? "아트" : "대역") + " · " + FinaleBeats.Label(mob.CurrentBeat) + (mob.IsBusy ? "…" : string.Empty);
            GUILayout.Label(FinaleBeats.Label(r) + " (" + ms + ")", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mob == null ? "세우기" : "치우기")) Later(() => ToggleFinale(r));
            foreach (FinaleBeat beat in FinaleBeats.For(r))
            {
                FinaleBeat b = beat;
                if (GUILayout.Button(FinaleBeats.Label(b))) Later(() => PlayFinale(r, b));
            }
            GUILayout.EndHorizontal();
        }
        EndSection();
    }

    private FinaleMob EnsureFinale(FinaleRole r, bool teleport)
    {
        DirectionStage stage = DirectionStage.Active;
        if (stage == null) return null;
        FinaleMob mob = stage.FinaleOf(r);
        if (mob != null) return mob;

        mob = stage.StageFinale(r);
        if (mob == null)
        {
            Note("피날레 " + FinaleBeats.Label(r) + ": 씬에 자리가 없습니다");
            return null;
        }

        mob.BeatFinished += (m, b) => Note("피날레 " + m.LastLog);
        mob.Cued += (m, cue) => Note("피날레 " + FinaleBeats.Label(m.Role) + " 신호: " + cue);
        if (teleport)
        {
            StageAnchor a = StageAnchor.Find(FinaleCastSO.Load().Get(r).anchorId);
            if (a != null) TeleportToView(a, SpaceId.SecurityRoom);
        }

        Note("피날레 " + FinaleBeats.Label(r) + " 세움 — " + mob.LastLog);
        return mob;
    }

    private void ToggleFinale(FinaleRole r)
    {
        DirectionStage stage = DirectionStage.Active;
        if (stage == null) return;
        if (stage.FinaleOf(r) != null)
        {
            stage.ClearFinale(r);
            Note("피날레 " + FinaleBeats.Label(r) + " 치움");
            return;
        }

        EnsureFinale(r, true);
    }

    private void PlayFinale(FinaleRole r, FinaleBeat b)
    {
        FinaleMob mob = EnsureFinale(r, false);
        if (mob == null) return;
        if (!mob.Supports(b)) Note("피날레 " + FinaleBeats.Label(r) + " · " + FinaleBeats.Label(b) + ": Animator에 없음 — 건너뜀");
        mob.Play(b);
    }

    // ─────────────────────────────── ⑥ 수칙 ───────────────────────────────

    private void DrawRulesTab()
    {
        FinalRuleBook book = NightRun.FinalRules;
        if (book == null)
        {
            GUILayout.Label("새 편성이 꺼져 있어 수칙 판정이 없습니다(근무 밤이 아님).", small);
            return;
        }

        BeginSection("오늘 덱", "<color=#7f7>✓</color> 방아쇠가 당겨짐 · <color=#f77>✗</color> 위반. 「단서」 = 수칙의 방아쇠(소리·형체)를 지금 울림, 「조우」 = 묶인 조우를 흐름대로.");
        IReadOnlyList<FinalJudge> judges = book.Judges;
        for (int i = 0; i < judges.Count; i++)
        {
            FinalJudge j = judges[i];
            RuleDef d = j.Def;
            string mark = (j.Triggered ? "<color=#7f7>✓</color>" : "·") + (j.Violated ? "<color=#f77>✗</color>" : " ");
            GUILayout.Label(mark + " <b>" + d.Id + "</b> " + (d.IsThreat ? "<color=#f96>위협</color> " : string.Empty)
                            + (d.HasAxis ? "<color=" + AxisHex[(int)d.Axis] + ">" + AxisNames[(int)d.Axis] + "</color>" : "-") + "  <color=#ddd>" + Escape(d.Text) + "</color>"
                            + (j.Status.Length > 0 ? "  <color=#9cf>" + Escape(j.Status) + "</color>" : string.Empty), small);

            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            string rid = d.Id;
            if (d.Space != SpaceId.None && GUILayout.Button("이동", GUILayout.Width(50f))) Later(() => TeleportForRule(rid));
            if (RuleTriggers.Find(rid) != null)
            {
                if (GUILayout.Button("단서", GUILayout.Width(50f))) Later(() => NightRun.DebugFireRuleCue(rid));
                if (GUILayout.Button("끝", GUILayout.Width(40f))) Later(() => NightRun.DebugEndRuleCue(rid));
            }
            else if (d.BoundEncounter.Length > 0)
            {
                string enc = d.BoundEncounter;
                if (GUILayout.Button("조우", GUILayout.Width(50f))) Later(() => ForceEncounter(enc, true));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        EndSection();

        DrawParadox();

        BeginSection("덱에 추가");
        GUILayout.BeginHorizontal();
        int n = 0;
        foreach (RuleDef r in ProgramCatalog.AllRules)
        {
            if (book.Judge(r.Id) != null) continue;
            string rid = r.Id;
            if (GUILayout.Button(rid, GUILayout.Width(44f))) Later(() => NightRun.DebugAddFinalRule(rid));
            if (++n % 9 == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }
        }
        GUILayout.EndHorizontal();
        EndSection();

        BeginSection("정산 결과 (최신 15)");
        IReadOnlyList<FinalRuleResult> results = NightRun.FinalResults;
        for (int i = results.Count - 1; i >= 0 && i >= results.Count - 15; i--)
        {
            GUILayout.Label(Escape(results[i].ToString()), small);
        }
        EndSection();
    }

    private void DrawParadox()
    {
        ParadoxRun pr = NightRun.Paradox;
        string prState = pr.RuleId == null ? "없음" : pr.RuleId + " " + pr.Pattern + (pr.Plan.WillSend ? "" : " (보류)") + " · " + (pr.SafeRead ? "안전한 읽기 완료" : pr.Sent ? "보냄 " + Mathf.RoundToInt(pr.Progress * 100f) + "%" : "대기");
        BeginSection("역설·변조", Escape(prState) + " · " + Escape(pr.Plan.Report));
        GUILayout.BeginHorizontal();
        GUILayout.Label("문자 보내기:", GUILayout.ExpandWidth(false));
        IReadOnlyList<RuleDef> deck = NightRun.Program.Deck;
        for (int i = 0; i < deck.Count; i++)
        {
            ParadoxEntry e = ParadoxCatalog.Find(deck[i].Id);
            if (e == null || !e.HasMessage) continue;
            string rid = deck[i].Id;
            if (GUILayout.Button(rid, GUILayout.ExpandWidth(false))) Later(() => Note(NightRun.DebugSendParadox(rid) ? "역설 문자 " + rid : "보낼 수 없음(" + rid + ")"));
        }
        if (GUILayout.Button("안전한 읽기 완료", GUILayout.ExpandWidth(false))) Later(() => Note(NightRun.DebugSafeRead() ? "안전한 읽기 완료" : "보낸 문자가 없음"));
        GUILayout.EndHorizontal();

        UnavoidableRun ur = NightRun.Unavoidable;
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>회피 불가</b>  " + Escape(ur.Status) + (NightRun.EmptyRoomChannel >= 0 ? "  <color=#999>빈 방 채널 CAM0" + (NightRun.EmptyRoomChannel + 1) + "</color>" : string.Empty), small);
        if (GUILayout.Button("지금 걸기", GUILayout.ExpandWidth(false))) Later(() => Note(NightRun.DebugStageUnavoidable() ? "회피 불가 역설을 걸었습니다" : "오늘 회피 불가 역설이 없거나 이미 걸림"));
        GUILayout.EndHorizontal();
        EndSection();
    }

    // ─────────────────────────────── ⑦ 점검 ───────────────────────────────

    private void DrawInspectionTab()
    {
        InspectionBoard board = NightRun.Inspections;
        if (board == null || board.Plan == null || board.Plan.Assignments.Count == 0)
        {
            GUILayout.Label("오늘 점검표가 없습니다.", small);
            return;
        }

        InspectionDispatcher orders = NightRun.Orders;
        BeginSection("순차 지시", orders != null ? Escape(orders.Describe(NightRun.NightMinute, NightRun.BannedSpace)) : "지시기 없음");
        if (orders != null && GUILayout.Button("다음 지시 보내기", GUILayout.Width(130f))) Later(() => NightRun.DebugIssueOrder());
        EndSection();

        BeginSection("오늘 점검표 — " + board.Plan.Day + "일차", "늦게 열리는 공간 " + board.Plan.LateSpace + ". 「이동」 = 항목 앞(보고 가능 거리)에서 항목을 바라봄.");
        IReadOnlyList<InspectionAssignment> rows = board.Plan.Assignments;
        for (int i = 0; i < rows.Count; i++)
        {
            InspectionAssignment a = rows[i];
            string id = a.Id;
            GUILayout.Label("<b>" + id + "</b> " + Escape(a.Item.Name) + (a.IsAnomaly ? " <color=#f77>[이상 " + a.Intensity + "]</color>" : " <color=#7f7>[정상]</color>")
                            + (a.IsLate ? " 늦게" : string.Empty) + (board.IsIssued(id) ? string.Empty : " <color=#999>지시 전</color>") + "  <color=#9cf>" + board.StateOf(id) + "</color>", small);
            GUILayout.Label("   <color=#9a9a9a>" + Escape(a.Item.TabletLine) + "</color>", small);
            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            if (GUILayout.Button("이동", GUILayout.Width(50f))) Later(() => TeleportToTarget(a.Item.TargetId, a.Item.Space));
            if (ColorButton("정상", ComplyColor, GUILayout.Width(50f))) Later(() => NightRun.ReportInspection(id, false));
            if (ColorButton("이상", ViolateColor, GUILayout.Width(50f))) Later(() => NightRun.ReportInspection(id, true));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        EndSection();
    }

    // ─────────────────────────────── 이동 ───────────────────────────────

    private void TeleportForRule(string ruleId)
    {
        RuleDef d = ProgramCatalog.Rule(ruleId);
        if (d == null) return;

        // 기준점이 있는 수칙은 기준점 앞 3m로, 없으면 공간으로.
        string anchor = AnchorOf(ruleId);
        JudgeTarget t;
        if (anchor.Length > 0 && JudgeTargetRegistry.TryGet(anchor, out t) && t != null)
        {
            TeleportNear(t.AnchorPosition, d.Space, 3f);
            return;
        }

        TeleportToSpace(d.Space);
    }

    private static string AnchorOf(string ruleId)
    {
        switch (ruleId)
        {
            case "H1": return FinalCues.H1Object;
            case "L1": return FinalCues.L1Shelf;
            case "L4": return FinalCues.L4Box;
            case "S1": return FinalCues.S1Center;
            case "S3": return FinalCues.ModelTarget;
            default: return string.Empty;
        }
    }

    private void TeleportToTarget(string targetId, SpaceId space)
    {
        JudgeTarget t;
        if (!JudgeTargetRegistry.TryGet(targetId, out t) || t == null)
        {
            Note("<color=#f77>씬에 대상이 없습니다: " + targetId + "</color>");
            TeleportToSpace(space);
            return;
        }

        TeleportNear(t.AnchorPosition, space, 1.2f);   // 보고 가능 거리(2m) 안 — 1.6m는 책상에 막히면 2m 밖에 섰다(42차)
    }

    private void TeleportToSpace(SpaceId space)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (SpaceIds.Canonical(space) == SpaceId.Classroom) space = SpaceId.Classroom_1_3;   // 점검 항목이 있는 교실(Classroom02).
        if (zones == null || !zones.TryGetSpaceBox(space, out box))
        {
            Note("<color=#f77>공간 상자를 모릅니다: " + space + "</color>");
            return;
        }

        Vector3 spot;
        if (!FreeSpot(new Vector3(box.center.x, box.min.y + 1f, box.center.z), box, out spot))
        {
            Note("<color=#f77>빈자리를 찾지 못했습니다: " + space + "</color>");
            return;
        }

        Teleport(spot, null);
        Note("이동 → " + SpaceName(space));
    }

    /// <summary>고정 연출 자리가 보이는 곳(자리 앞 <see cref="StageAnchor.DebugViewDistance"/>m)으로 옮기고 그쪽을 보게 한다.</summary>
    private void TeleportToView(StageAnchor anchor, SpaceId space)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box;
        if (zones == null || !zones.TryGetSpaceBox(space, out box))
        {
            TeleportToSpace(space);
            return;
        }

        Vector3 f = anchor.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        Vector3 want = anchor.transform.position + f.normalized * anchor.DebugViewDistance;
        Vector3 spot;
        if (!FreeSpot(want, box, out spot))
        {
            TeleportToSpace(space);
            return;
        }

        Vector3 look = anchor.transform.position - spot;
        look.y = 0f;
        Teleport(spot, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
        Note("이동 → " + SpaceName(space) + " (" + anchor.AnchorId + " 앞)");
    }

    private void TeleportNear(Vector3 target, SpaceId space, float distance)
    {
        SpaceZones zones = FindAnyObjectByType<SpaceZones>();
        Bounds box = new Bounds(target, new Vector3(12f, 6f, 12f));
        if (zones != null)
        {
            Bounds b;
            if (zones.TryGetSpaceBox(space, out b)) box = b;
        }

        Vector3 toward = box.center - target;
        toward.y = 0f;
        if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
        Vector3 want = target + toward.normalized * distance;

        Vector3 spot;
        if (!FreeSpot(want, box, out spot))
        {
            Note("<color=#f77>빈자리를 찾지 못했습니다</color>");
            return;
        }

        Vector3 look = target - spot;
        look.y = 0f;
        Teleport(spot, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
    }

    /// <summary><paramref name="want"/> 둘레에서 사람이 설 수 있는 바닥 점을 찾는다(나선, 0.6m 간격, 상자 안).</summary>
    private static bool FreeSpot(Vector3 want, Bounds box, out Vector3 spot)
    {
        Transform player = PlayerRoot();
        for (int ring = 0; ring < 8; ring++)
        {
            int steps = ring == 0 ? 1 : ring * 8;
            for (int k = 0; k < steps; k++)
            {
                float ang = k * Mathf.PI * 2f / steps;
                Vector3 p = want + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (ring * 0.6f);
                if (p.x < box.min.x + 0.4f || p.x > box.max.x - 0.4f || p.z < box.min.z + 0.4f || p.z > box.max.z - 0.4f) continue;

                Vector3 floor = DirectionStage.FloorBelow(new Vector3(p.x, box.min.y + 1.2f, p.z));
                if (floor.y > box.min.y + 1.2f) continue;   // 책상 위 등

                if (!Blocked(floor, player))
                {
                    spot = floor;
                    return true;
                }
            }
        }

        spot = Vector3.zero;
        return false;
    }

    private static bool Blocked(Vector3 floor, Transform player)
    {
        Collider[] hits = Physics.OverlapCapsule(floor + Vector3.up * 0.45f, floor + Vector3.up * 1.6f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].transform.IsChildOf(player)) continue;
            return true;
        }

        return false;
    }

    private static Transform PlayerRoot()
    {
        PlayerSensors hub = PlayerSensors.Active;
        if (hub != null && hub.PlayerRoot != null) return hub.PlayerRoot;
        FPController fp = FindAnyObjectByType<FPController>();
        return fp != null ? fp.transform : null;
    }

    private static void Teleport(Vector3 floor, float? yaw)
    {
        Transform root = PlayerRoot();
        if (root == null) return;

        // 루트 피벗에서 발바닥(콜라이더 아래끝)까지 — 그만큼 바닥 위에 놓는다.
        Collider body = root.GetComponent<Collider>();
        if (body == null) body = root.GetComponentInChildren<Collider>();
        float lift = body != null ? root.position.y - body.bounds.min.y + 0.02f : 0.85f;
        if (lift < 0f || lift > 3f) lift = 0.85f;
        Vector3 target = floor + Vector3.up * lift;

        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = target;
        }

        root.position = target;
        if (yaw.HasValue)
        {
            root.rotation = Quaternion.Euler(0f, yaw.Value, 0f);
            if (rb != null) rb.rotation = root.rotation;
        }

        Physics.SyncTransforms();
    }

    // ─────────────────────────────── 도구 ───────────────────────────────

    private static string FakeName(string id)
    {
        switch (id)
        {
            case "fake.locker.rattle": return "덜컹이는 사물함";
            case "fake.locker.row": return "열려 있는 사물함";
            case "fake.flashlight.flicker": return "손전등 깜빡임";
            case TensionDirector.FakeBugs: return "벌레 떼";
            default: return id;
        }
    }

    private static string SpaceName(SpaceId s)
    {
        switch (SpaceIds.Canonical(s))
        {
            case SpaceId.Corridor: return "복도";
            case SpaceId.Classroom: return "교실";
            case SpaceId.ScienceRoom: return "과학실";
            case SpaceId.Toilet: return "화장실";
            case SpaceId.Library: return "도서관";
            case SpaceId.SecurityRoom: return "경비실";
            case SpaceId.None: return "-";
            default: return s.ToString();
        }
    }

    private static string Clock(int minute)
    {
        if (minute < 0) return "--:--";
        return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
    }

    private static string Escape(string s)
    {
        return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("<", "‹").Replace(">", "›");
    }
}
#endif

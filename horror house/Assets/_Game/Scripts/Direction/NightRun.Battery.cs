using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    // 56차 — 손전등 배터리(설계 문서 「야간근무 손전등 배터리 설계안」). 상태와 놓는 자리는 코어, 빛·입력·줍기는 화면 쪽 FlashlightPower.
    public static partial class NightRun
    {
        private static FlashlightBattery _battery;
        private static BatteryPlan _batteryPlan;
        private static List<string> _batteryYesterday = new List<string>();

        /// <summary>배터리를 쓸지. 구동기(<c>NightRunDriver</c>)가 켠다. 옛 테스트를 위해 코어 기본값은 꺼짐(꺼져 있으면 손전등은 닳지 않는다).</summary>
        public static bool BatteryEnabled { get; set; }

        /// <summary>배터리 자리 뽑기 씨앗(시험·재현용). 값이 있으면 그 씨앗 + 일차로 뽑는다.</summary>
        public static int? BatterySeed { get; set; }

        /// <summary>오늘 배터리. 꺼져 있거나 밤 전이면 null.</summary>
        public static FlashlightBattery Battery
        {
            get { return _battery; }
        }

        /// <summary>오늘 배터리 자리. 꺼져 있거나 밤 전이면 null.</summary>
        public static BatteryPlan BatteryPlan
        {
            get { return _batteryPlan; }
        }

        /// <summary>지금 비춤 판정 거리(m) — 배터리가 10% 아래면 짧다.</summary>
        public static float BeamRange
        {
            get { return _battery != null ? BatteryRules.BeamRange(_battery.Charge) : SensingRules.BeamRange; }
        }

        /// <summary>밤 편성 뒤(재시작 제외) 배터리를 새로 채우고 빈 자리 계획을 만든다(칸은 화면 쪽이 <see cref="FillBatteryPlan"/>로 채운다).</summary>
        private static void BeginBattery()
        {
            if (!BatteryEnabled)
            {
                _battery = null;
                _batteryPlan = null;
                return;
            }

            _battery = new FlashlightBattery();
            System.Random rng = BatterySeed.HasValue ? new System.Random(BatterySeed.Value * 31 + Day) : new System.Random();
            _batteryPlan = new BatteryPlan(Day, _batteryYesterday, rng);
        }

        /// <summary>씬의 칸으로 오늘 자리를 정한다(밤마다 한 번). 이미 채웠거나 꺼져 있으면 아무것도 하지 않는다.</summary>
        public static void FillBatteryPlan(IList<BatteryCache> caches)
        {
            if (_batteryPlan == null || _batteryPlan.Filled) return;
            _batteryPlan.Fill(caches);
            _batteryYesterday = new List<string>(_batteryPlan.Placed);
            Debug.Log("[NightRun] " + _batteryPlan);
        }

        /// <summary>그 칸의 배터리를 주머니에 넣는다. 배터리가 없거나 주머니가 차 있으면 false.</summary>
        public static bool TakeBattery(string cacheId)
        {
            if (_battery == null || _batteryPlan == null || IsCaptured) return false;
            if (!_batteryPlan.Holds(cacheId) || _battery.PocketFull) return false;
            _batteryPlan.Take(cacheId);
            _battery.TryPocket();
            Debug.Log("[NightRun] 배터리 주움 — " + _battery);
            return true;
        }

        private static void ResetBattery(bool clearSwitches)
        {
            _battery = null;
            _batteryPlan = null;
            _batteryYesterday = new List<string>();
            if (clearSwitches)
            {
                BatteryEnabled = false;
                BatterySeed = null;
            }
        }
    }
}

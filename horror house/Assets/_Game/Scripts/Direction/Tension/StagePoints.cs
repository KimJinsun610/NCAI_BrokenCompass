using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 고정 연출 자리(2026-10-01, 7단계 후속). 씬의 <c>StageAnchor</c>(연출 쪽)가 켜질 때 자기 자리를 여기 적고,
    /// 긴장 디렉터는 대본의 <see cref="EncounterScript.StageAnchor"/>가 있으면 플레이어 기준 자리 대신 이 자리를 쓴다
    /// (소리·판정 단서의 점이 실제 몹 자리와 같아진다).
    /// <para>코어는 연출을 모른다 — 자리(점)만 주고받는다. 등록이 없으면(EditMode 테스트·다른 씬) 옛 플레이어 기준 자리로 돌아간다.</para>
    /// </summary>
    public static class StagePoints
    {
        private static readonly Dictionary<string, Vector3> s_points = new Dictionary<string, Vector3>(StringComparer.Ordinal);

        /// <summary>등록된 자리 수.</summary>
        public static int Count
        {
            get { return s_points.Count; }
        }

        /// <summary>자리를 적는다(같은 ID면 덮는다).</summary>
        public static void Set(string id, Vector3 point)
        {
            if (string.IsNullOrEmpty(id)) return;
            s_points[id] = point;
        }

        /// <summary>자리를 지운다.</summary>
        public static void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            s_points.Remove(id);
        }

        /// <summary>그 ID의 자리.</summary>
        public static bool TryGet(string id, out Vector3 point)
        {
            point = Vector3.zero;
            return !string.IsNullOrEmpty(id) && s_points.TryGetValue(id, out point);
        }

        /// <summary>모두 지운다(테스트·도메인 리로드 없는 재생).</summary>
        public static void Clear()
        {
            s_points.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_points.Clear();
        }
    }
}

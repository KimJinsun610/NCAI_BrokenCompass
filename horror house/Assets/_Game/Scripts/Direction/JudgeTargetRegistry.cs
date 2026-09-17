using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// 씬 대상 등록부. 씬에 놓인 <see cref="JudgeTarget"/>가 켜질 때 자기 ID를 올리고 꺼질 때 내린다.
    /// <para>
    /// <see cref="NightRun.BeginNight"/>은 <see cref="NightRun.RegisteredTargets"/>가 비어 있고(null)
    /// 이 등록부에 ID가 하나라도 있으면 <b>이 등록부의 스냅숏</b>으로 카드 참조를 검사한다.
    /// 등록된 ID가 하나도 없으면(테스트 씬 등) 검사를 건너뛴다.
    /// </para>
    /// <para>
    /// 같은 ID를 여러 오브젝트가 올릴 수 있다(예: 구역 하나를 콜라이더 여럿으로 만든 경우). 개수를 세어
    /// 마지막 하나가 내려갈 때 ID가 빠진다.
    /// </para>
    /// </summary>
    public static class JudgeTargetRegistry
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<JudgeTarget>> Owners = new Dictionary<string, List<JudgeTarget>>(StringComparer.Ordinal);

        /// <summary>등록 상태가 바뀌었다.</summary>
        public static event Action Changed;

        /// <summary>등록된 서로 다른 ID 수.</summary>
        public static int Count
        {
            get { return Counts.Count; }
        }

        /// <summary>ID가 등록돼 있는지.</summary>
        public static bool Contains(string id)
        {
            return !string.IsNullOrEmpty(id) && Counts.ContainsKey(id);
        }

        /// <summary>지금 등록된 ID의 복사본. 판정 시작 때 한 번 찍어 쓴다.</summary>
        public static HashSet<string> Snapshot()
        {
            return new HashSet<string>(Counts.Keys, StringComparer.Ordinal);
        }

        /// <summary>
        /// ID를 가진 대상 컴포넌트 중 가장 먼저 올라와 아직 남아 있는 것(클라이언트가 위치·콜라이더를 찾을 때). 없으면 false.
        /// </summary>
        public static bool TryGet(string id, out JudgeTarget target)
        {
            target = null;
            List<JudgeTarget> list;
            if (string.IsNullOrEmpty(id) || !Owners.TryGetValue(id, out list))
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    target = list[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>ID 하나를 올린다. <paramref name="owner"/>는 null이어도 된다(테스트).</summary>
        public static void Register(string id, JudgeTarget owner)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            int count;
            Counts.TryGetValue(id, out count);
            Counts[id] = count + 1;

            if (owner != null)
            {
                List<JudgeTarget> list;
                if (!Owners.TryGetValue(id, out list))
                {
                    list = new List<JudgeTarget>();
                    Owners[id] = list;
                }

                list.Add(owner);
            }

            RaiseChanged();
        }

        /// <summary>ID 하나를 내린다. 올린 횟수만큼 내려야 빠진다.</summary>
        public static void Unregister(string id, JudgeTarget owner)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            int count;
            if (!Counts.TryGetValue(id, out count))
            {
                return;
            }

            if (count <= 1)
            {
                Counts.Remove(id);
                Owners.Remove(id);
            }
            else
            {
                Counts[id] = count - 1;
                List<JudgeTarget> list;
                if (owner != null && Owners.TryGetValue(id, out list))
                {
                    list.Remove(owner);   // 같은 소유자가 여러 번 올렸으면 하나만 뺀다.
                }
            }

            RaiseChanged();
        }

        /// <summary>전부 비운다(테스트·플레이 시작).</summary>
        public static void Clear()
        {
            Counts.Clear();
            Owners.Clear();
            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            Action handler = Changed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>도메인 리로드를 끈 상태에서 이전 플레이의 등록이 남지 않도록 비운다. <b>지우지 말 것.</b></summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Counts.Clear();
            Owners.Clear();
            Changed = null;
        }
    }
}

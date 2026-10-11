using System.Collections.Generic;
using UnityEngine;

namespace NightDuty
{
    /// <summary>
    /// CCTV 화면 속 사람이 나타나 걸어가는 자리 하나(71차).
    /// <see cref="From"/>에서 <see cref="To"/>로 걷는다 — 두 점 모두 그 채널 카메라에 보이는 바닥(발 높이는 연출이 그 자리 바닥으로 맞춘다).
    /// </summary>
    public sealed class CctvSpot
    {
        /// <summary>자리 ID(<c>cctv.hall.far</c> 등).</summary>
        public readonly string Id;

        /// <summary>CCTV 채널(0~4 — 복도·교실·과학실·화장실·도서관, <c>CctvConfigSO.FillDefaults</c> 순서).</summary>
        public readonly int Channel;

        /// <summary>그 자리의 공간.</summary>
        public readonly SpaceId Space;

        /// <summary>걷기 시작하는 바닥 점.</summary>
        public readonly Vector3 From;

        /// <summary>걷기가 끝나는 바닥 점.</summary>
        public readonly Vector3 To;

        /// <summary>사람 말 설명(개발자 모드·로그).</summary>
        public readonly string Note;

        /// <summary>만든다.</summary>
        public CctvSpot(string id, int channel, SpaceId space, Vector3 from, Vector3 to, string note)
        {
            Id = id;
            Channel = channel;
            Space = space;
            From = from;
            To = to;
            Note = note;
        }

        /// <summary>채널 ID(<c>cctv.chN</c> — 판정 신호와 같은 이름).</summary>
        public string ChannelId
        {
            get { return "cctv.ch" + Channel; }
        }

        /// <summary>걷는 거리(m, 수평).</summary>
        public float Length
        {
            get { return SensingRules.HorizontalDistance(From, To); }
        }

        public override string ToString()
        {
            return Id + "(CAM" + (Channel + 1).ToString("00") + " " + Note + ")";
        }
    }

    /// <summary>
    /// CCTV 사람이 나타날 수 있는 자리 표(71차, 민: 「사람이 CCTV에 등장하는 이벤트가 결정되면 등장할 장소도 함께 결정되도록 ·
    /// 기존에 사람이 등장할 수 있는 장소를 기준으로 하되 추후 다른 장소를 추가하기 쉽게」).
    /// <list type="bullet">
    /// <item>기존에 나타날 수 있던 곳 = CCTV 채널 다섯(K1 조우는 보고 있던 채널, K-1 이상은 일차로 고른 채널) — 채널마다 실제로 보이는 바닥 길을 잡았다
    /// (PlayScene에서 채널 카메라 화면 격자로 바닥을 쏘고, 길 위 일곱 점에서 발·허리·머리가 화면 안이고 가려지지 않는지 잼).</item>
    /// <item><b>자리를 더하려면 <see cref="Table"/>에 한 줄</b>. 채널을 새로 만들면 <c>CctvConfigSO</c>에 채널을 더하고 그 번호로 적는다.
    /// 연출(<c>CctvWalker</c>)은 실제 바닥 높이·화면 안인지 다시 확인하고, 화면에 안 보이면 그 채널 화면 바닥으로 물러난다.</item>
    /// <item>고르기: 채널을 먼저 고르게(자리가 많은 채널이 몰리지 않게) → 그 채널의 자리. 빈 방 채널(K2)과 직전 자리는 피한다.</item>
    /// </list>
    /// </summary>
    public static class CctvSpots
    {
        private static readonly CctvSpot[] Table =
        {
            new CctvSpot("cctv.hall.far", 0, SpaceId.Corridor, new Vector3(31.4f, 1.5f, 41.9f), new Vector3(23.0f, 1.5f, 42.4f), "복도 끝에서 카메라 쪽으로 걸어옴"),
            new CctvSpot("cctv.hall.away", 0, SpaceId.Corridor, new Vector3(20.0f, 1.5f, 42.6f), new Vector3(30.0f, 1.5f, 41.8f), "카메라 밑에서 복도 끝으로 멀어짐"),
            new CctvSpot("cctv.class.front", 1, SpaceId.Classroom, new Vector3(40.9f, 1.5f, 33.4f), new Vector3(41.2f, 1.5f, 38.4f), "1-3 교실 칠판 앞을 가로지름"),
            new CctvSpot("cctv.science.aisle", 2, SpaceId.ScienceRoom, new Vector3(52.2f, 1.5f, 42.0f), new Vector3(49.8f, 1.5f, 41.9f), "과학실 안쪽 통로에서 다가옴"),
            new CctvSpot("cctv.toilet.stalls", 3, SpaceId.Toilet, new Vector3(0.6f, 1.5f, 33.0f), new Vector3(1.4f, 1.5f, 34.9f), "화장실 칸 앞을 지나감"),
            new CctvSpot("cctv.library.stacks", 4, SpaceId.Library, new Vector3(5.0f, 1.5f, 49.6f), new Vector3(4.7f, 1.5f, 52.2f), "도서관 서가 사이에서 창가로"),
        };

        /// <summary>모든 자리.</summary>
        public static IReadOnlyList<CctvSpot> All
        {
            get { return Table; }
        }

        /// <summary>그 ID의 자리. 없으면 null.</summary>
        public static CctvSpot Find(string id)
        {
            for (int i = 0; i < Table.Length; i++) if (Table[i].Id == id) return Table[i];
            return null;
        }

        /// <summary>그 채널의 자리들.</summary>
        public static List<CctvSpot> InChannel(int channel)
        {
            List<CctvSpot> list = new List<CctvSpot>();
            for (int i = 0; i < Table.Length; i++) if (Table[i].Channel == channel) list.Add(Table[i]);
            return list;
        }

        /// <summary>
        /// 자리 하나를 고른다. <paramref name="onlyChannel"/>이 0 이상이면 그 채널에서만, <paramref name="avoidChannel"/>(빈 방 채널)·<paramref name="avoidId"/>(직전 자리)는 피한다
        /// (피하면 남는 것이 없으면 피하지 않는다). 자리가 하나도 없으면 null.
        /// </summary>
        public static CctvSpot Pick(System.Random rng, int onlyChannel = -1, int avoidChannel = -1, string avoidId = null)
        {
            if (rng == null) rng = new System.Random();
            List<int> channels = new List<int>();
            for (int i = 0; i < Table.Length; i++)
            {
                int ch = Table[i].Channel;
                if (onlyChannel >= 0 && ch != onlyChannel) continue;
                if (!channels.Contains(ch)) channels.Add(ch);
            }

            if (channels.Count == 0) return null;
            if (channels.Count > 1 && avoidChannel >= 0) channels.Remove(avoidChannel);

            // 직전 자리만 있는 채널은 피한다(남는 채널이 있을 때).
            if (!string.IsNullOrEmpty(avoidId) && channels.Count > 1)
            {
                CctvSpot last = Find(avoidId);
                if (last != null && InChannel(last.Channel).Count <= 1) channels.Remove(last.Channel);
            }

            int pickCh = channels[rng.Next(channels.Count)];
            List<CctvSpot> spots = InChannel(pickCh);
            if (spots.Count > 1 && !string.IsNullOrEmpty(avoidId)) spots.RemoveAll(s => s.Id == avoidId);
            return spots[rng.Next(spots.Count)];
        }
    }
}

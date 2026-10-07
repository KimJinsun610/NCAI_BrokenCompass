using UnityEngine;

/// <summary>
/// <b>빈 껍데기 — 아무 일도 하지 않는다(2026-10-03 폐기).</b> 옛 조도 연출(구간마다 등 8/6/4/2/0개 + 등 색온도)은
/// <see cref="IlluminanceMap"/>(공간별 톤 Volume + 기획서 표대로 소등)이 대신한다.
/// <para>
/// 이름만 남긴 이유: 다른 사람의 씬(김진선님 <c>PlayScene_test</c>, <c>_Recovery/0</c>)에 이 컴포넌트가 저장돼 있어
/// 스크립트를 지우면 그 씬에 「Missing Script」가 생긴다. 근무 씬(PlayScene)에서는 떼어 냈다.
/// 새 씬에 붙이지 마십시오. 기능을 되살리지 마십시오.
/// </para>
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class SpaceLights : MonoBehaviour
{
}

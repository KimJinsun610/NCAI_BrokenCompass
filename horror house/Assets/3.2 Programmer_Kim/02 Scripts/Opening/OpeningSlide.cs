using System;
using UnityEngine;

/// <summary>오프닝에서 한 장면. 그림·자막·머무는 시간·소리를 한 묶음으로 둔다.</summary>
[Serializable]
public class OpeningSlide
{
    [Tooltip("화면을 채울 그림. 비우면 검은 화면에 자막만 나온다.")]
    public Sprite image;

    [Tooltip("아래쪽 자막. 비우면 자막을 띄우지 않는다.")]
    [TextArea(2, 4)] public string caption;

    [Tooltip("이 장면이 머무는 시간(초). 소리를 넣으면 소리 길이와 둘 중 긴 쪽을 쓴다.")]
    [Min(0.1f)] public float hold = 3.5f;

    [Tooltip("이 장면에서 한 번 내는 소리(내레이션 등). 비우면 없음.")]
    public AudioClip voice;
}

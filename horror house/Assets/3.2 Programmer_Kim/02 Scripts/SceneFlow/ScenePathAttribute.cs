using UnityEngine;

/// <summary>
/// string 필드에 붙이면 인스펙터에서 씬 에셋을 드래그해 지정할 수 있다.
/// 저장되는 값은 "Assets/.../Scene.unity" 경로 문자열이라 빌드에서도 그대로 동작한다.
/// 이름이 아닌 경로를 쓰는 이유: 같은 이름의 씬(DemoScene 등)이 여러 폴더에 있어도 헷갈리지 않게.
/// </summary>
public class ScenePathAttribute : PropertyAttribute
{
}

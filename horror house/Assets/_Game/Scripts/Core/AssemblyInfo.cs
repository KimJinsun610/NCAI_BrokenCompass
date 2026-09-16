using System.Runtime.CompilerServices;

// EditMode 테스트가 RuleSO의 테스트용 구성 메서드(internal)를 쓸 수 있게 한다.
// 플레이어 빌드에는 테스트 어셈블리가 포함되지 않으므로 런타임 영향은 없다.
[assembly: InternalsVisibleTo("NightDuty.Tests.EditMode")]

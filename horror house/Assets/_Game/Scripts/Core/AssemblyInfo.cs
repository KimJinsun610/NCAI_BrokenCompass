using System.Runtime.CompilerServices;

// EditMode 테스트와 에디터 도구가 RuleSO·편성표의 코드 구성 메서드(internal)를 쓸 수 있게 한다.
// 두 어셈블리 모두 에디터 전용이라 플레이어 빌드에는 포함되지 않는다.
[assembly: InternalsVisibleTo("NightDuty.Tests.EditMode")]
[assembly: InternalsVisibleTo("NightDuty.Editor")]

using NightDuty;
using NUnit.Framework;

/// <summary>
/// 2026-11-23: EditMode 시험은 언제나 한국어(정본)로 돈다 — 시험들이 한국어 문구를 대조한다.
/// 플레이 모드에서 들어간 언어(이 PC에 저장된 English 등)가 에디터에 남아 있어도 시험 전에 되돌린다.
/// 이름공간 밖에 두어 시험 어셈블리 전체에 걸린다.
/// </summary>
[SetUpFixture]
public class LocalizationTestSetup
{
    [OneTimeSetUp]
    public void KoreanForTests()
    {
        Loc.Set(TextLang.Korean);
        Loc.SetTableForTest(null);
    }
}

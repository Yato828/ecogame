using NUnit.Framework;
namespace Wattshift.Tests
{
    public sealed class WebFontTests
    {
        [Test] public void EmbeddedFontCoversCyrillicAndEveryExistingUiSymbol()
        {
            var font = RuntimeUiFont.Definition.fontAsset;
            foreach (char character in "АБВабвЁё✓★☆⚡→↗↙-")
                Assert.IsTrue(font.HasCharacter(character, true, true), "Missing embedded glyph U+" + ((int)character).ToString("X4"));
        }
    }
}

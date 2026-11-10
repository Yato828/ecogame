using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Wattshift
{
    /// <summary>Embedded Cyrillic and existing UI symbols; never rely on OS fonts in Web.</summary>
    public static class RuntimeUiFont
    {
        static FontAsset primary;
        public static FontDefinition Definition
        {
            get
            {
                if (primary == null)
                {
                    primary = FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSans-Regular"));
                    primary.fallbackFontAssetTable = new List<FontAsset>
                    {
                        FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSansSymbols-Regular")),
                        FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NotoSansSymbols2-Regular"))
                    };
                }
                return FontDefinition.FromSDFFont(primary);
            }
        }
    }
}

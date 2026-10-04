using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>The original look: the game's parchment box, with the title as its first line.</summary>
    internal sealed class ClassicTheme : HudTheme
    {
        public override string Id => "classic";

        public override string Title => I18n.Get("hud.title");

        public override int TitleInside => (int)Game1.smallFont.MeasureString("Ag").Y;

        public override int MinBoxWidth => (int)Game1.smallFont.MeasureString(this.Title).X + this.Padding * 2;

        public override void DrawTitle(SpriteBatch b, Rectangle box)
        {
            this.DrawLine(b, this.Title, new Vector2(box.X + this.Padding, box.Y + this.Padding), this.Palette.Text);
        }
    }
}

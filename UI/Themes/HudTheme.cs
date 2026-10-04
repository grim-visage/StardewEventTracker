using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>What the HUD is showing, for themes that react to it.</summary>
    /// <param name="AnyAvailableNow">Whether an event on the HUD can happen right now.</param>
    internal readonly record struct HudState(bool AnyAvailableNow);

    /// <summary>The text colours a theme uses on the HUD.</summary>
    internal readonly record struct HudPalette(Color Text, Color Ready, Color Soon, Color Muted);

    /// <summary>
    /// How the HUD tracker looks: its box, its title and its text colours. Add a theme by subclassing this and listing
    /// it in <see cref="All"/>; players pick one by <see cref="Id"/> in the config, and its name comes from the
    /// <c>config.hud-theme.&lt;id&gt;</c> translation.
    /// </summary>
    internal abstract class HudTheme
    {
        /// <summary>The theme for new players, and the one used when the config names a theme that doesn't exist.</summary>
        public static readonly HudTheme Default = new CommunityCenterTheme();

        /// <summary>The themes with one fixed look.</summary>
        private static readonly IReadOnlyList<HudTheme> Fixed = new HudTheme[] { Default, new JojaTheme(), new SpiritsEveTheme() };

        /// <summary>The ID of the theme that follows the in-game season.</summary>
        public const string SeasonalId = "seasonal";

        /// <summary>Each season's theme, light and dark, made on first use.</summary>
        private static readonly Dictionary<(Season, bool), SeasonTheme> Seasons = new();

        /// <summary>Every theme players can choose, in the order the config menu lists them.</summary>
        public static string[] Ids =>
            Fixed.Select(t => t.Id)
                .Append(SeasonalId)
                .Concat(Enum.GetValues<Season>().Select(s => s.ToString().ToLowerInvariant()))
                .ToArray();

        /// <summary>The theme with this ID, or <see cref="Default"/>.</summary>
        /// <param name="dark">Whether the season themes use their dark mode (the other themes have one look).</param>
        public static HudTheme Get(string? id, bool dark)
        {
            if (Fixed.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } theme)
                return theme;

            Season? season = SeasonalId.Equals(id, StringComparison.OrdinalIgnoreCase)
                ? Game1.season
                : Enum.TryParse(id, ignoreCase: true, out Season named) ? named : null;
            if (season is not { } s)
                return Default;

            if (!Seasons.TryGetValue((s, dark), out SeasonTheme? seasonTheme))
                Seasons[(s, dark)] = seasonTheme = new SeasonTheme(s, dark);
            return seasonTheme;
        }

        /// <summary>How opaque the HUD is drawn, from 0 to 1; it fades when the player or the mouse is under it.</summary>
        public static float Opacity { get; set; } = 1f;

        /// <summary>A colour faded to the HUD's <see cref="Opacity"/>; every colour a theme draws with goes through this.</summary>
        protected static Color Fade(Color color) => color * Opacity;

        /// <summary>The ID saved in the config, e.g. "joja". Never change it once released, or players lose their choice.</summary>
        public abstract string Id { get; }

        /// <summary>The default text colours, matching the rest of the mod's menus.</summary>
        public virtual HudPalette Palette { get; } = new(Game1.textColor, new Color(20, 130, 40), new Color(185, 105, 0), new Color(110, 100, 90));

        /// <summary>The space between the box's edge and its text.</summary>
        public virtual int Padding => 20;

        /// <summary>The title text, translated.</summary>
        public abstract string Title { get; }

        /// <summary>How far the title sticks out above the box, so the box can be kept below the top of the screen.</summary>
        public virtual int TitleAbove => 0;

        /// <summary>The height the title takes inside the box, above the first entry.</summary>
        public virtual int TitleInside => 0;

        /// <summary>The narrowest the box can be, e.g. so it's at least as wide as a title drawn above it.</summary>
        public virtual int MinBoxWidth => 0;

        /// <summary>The area the title covers, so it can be dragged too; empty if it's inside the box.</summary>
        public virtual Rectangle TitleArea(Rectangle box) => Rectangle.Empty;

        /// <summary>Draws the box behind the entries.</summary>
        public virtual void DrawBox(SpriteBatch b, Rectangle box, bool dragging)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), box.X, box.Y, box.Width, box.Height, Fade(dragging ? Color.Wheat : Color.White * 0.9f), 4f, drawShadow: false);
        }

        /// <summary>Draws the title, after the box so it can overlap it.</summary>
        public abstract void DrawTitle(SpriteBatch b, Rectangle box, HudState state);

        /// <summary>
        /// Draws one of the game's title banners (SpriteText's scroll styles) around <paramref name="innerWidth"/> pixels
        /// of content whose top-left is at (<paramref name="x"/>, <paramref name="y"/>), laid out the way
        /// SpriteText.drawString lays them out around its text: the ends are 3 pixels wide and the middle stretches.
        /// </summary>
        protected static void DrawBanner(SpriteBatch b, Texture2D texture, Rectangle left, Rectangle middle, Rectangle right, int x, int y, int innerWidth)
        {
            b.Draw(texture, new Vector2(x - 12, y - 12), left, Fade(Color.White), 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            b.Draw(texture, new Vector2(x, y - 12), middle, Fade(Color.White), 0f, Vector2.Zero, new Vector2(innerWidth + 4, 4f), SpriteEffects.None, 1f);
            b.Draw(texture, new Vector2(x + innerWidth + 4, y - 12), right, Fade(Color.White), 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
        }

        /// <summary>Draws a line for a dark box: a dark drop shadow instead of the game's warm one, which glows on dark.</summary>
        protected static void DrawDarkLine(SpriteBatch b, string text, Vector2 position, Color color)
        {
            b.DrawString(Game1.smallFont, text, position + new Vector2(2, 2), Fade(Color.Black * 0.45f));
            b.DrawString(Game1.smallFont, text, position, Fade(color));
        }

        /// <summary>Draws one line of an entry in the HUD's font.</summary>
        public virtual void DrawLine(SpriteBatch b, string text, Vector2 position, Color color)
        {
            Utility.drawTextWithShadow(b, text, Game1.smallFont, position, Fade(color), shadowIntensity: 0.25f * Opacity);
        }
    }
}

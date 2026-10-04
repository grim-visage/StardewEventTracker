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

        /// <summary>Every theme players can choose, in the order the config menu lists them.</summary>
        public static readonly IReadOnlyList<HudTheme> All = new HudTheme[] { Default, new JojaTheme() };

        public static string[] Ids => All.Select(t => t.Id).ToArray();

        /// <summary>The theme with this ID, or <see cref="Default"/>.</summary>
        public static HudTheme Get(string? id) =>
            All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? Default;

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
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), box.X, box.Y, box.Width, box.Height, dragging ? Color.Wheat : Color.White * 0.9f, 4f, drawShadow: false);
        }

        /// <summary>Draws the title, after the box so it can overlap it.</summary>
        public abstract void DrawTitle(SpriteBatch b, Rectangle box, HudState state);

        /// <summary>Draws one line of an entry in the HUD's font.</summary>
        public virtual void DrawLine(SpriteBatch b, string text, Vector2 position, Color color)
        {
            Utility.drawTextWithShadow(b, text, Game1.smallFont, position, color, shadowIntensity: 0.25f);
        }
    }
}

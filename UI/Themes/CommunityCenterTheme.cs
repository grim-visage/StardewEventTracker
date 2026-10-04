using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// The Community Center: the title on the bundle book's title tab, floating above the parchment box, with three
    /// Junimos in bundle colours perched on its edge. They idle like the Community Center's Junimos and cheer when an
    /// event can happen right now.
    /// </summary>
    internal sealed class CommunityCenterTheme : HudTheme
    {
        /// <summary>The bundle book's title tab, in the English JunimoNote texture.</summary>
        private static readonly Rectangle TabSource = new(102, 0, 119, 20);

        /// <summary>For each row of the tab, the first pixel inside its rounded ends (the right end mirrors the left); the rest is the page around it.</summary>
        private static readonly int[] TabInset = { 3, 2, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 3 };

        /// <summary>The width of each rounded end of the tab; its middle column is stretched to fit the title.</summary>
        private const int TabEnd = 10;

        private const int Scale = 3;

        /// <summary>How far the tab starts in from the box's left edge.</summary>
        private const int TabLeft = 40;

        /// <summary>How far the tab's top sits above the box; the tab overlaps the box's frame below that.</summary>
        private const int TabAbove = 44;

        /// <summary>The space between the tab's ends and the title.</summary>
        private const int TitleMargin = 12;

        /// <summary>The Junimos' colours (the Community Center's bundle colours), and where each sits from the box's right edge.</summary>
        private static readonly (Color Color, int FromRight, bool Flip)[] Junimos =
        {
            (new Color(120, 220, 60), 160, false),
            (new Color(255, 170, 40), 110, true),
            (new Color(170, 110, 230), 64, false)
        };

        /// <summary>A Junimo's frames: standing idle (8-11, as in the game) and arms up while holding a star (44-47).</summary>
        private const int IdleFrame = 8, CheerFrame = 44, FrameCount = 4, JunimoSize = 16;

        /// <summary>Milliseconds per idle frame, slower than the game's 100 so they stay calm on screen.</summary>
        private const double IdleFrameMs = 220;

        private const double CheerFrameMs = 240;

        /// <summary>How often each Junimo hops while cheering, and how long a hop lasts, in milliseconds.</summary>
        private const double HopEveryMs = 2200, HopMs = 420;

        /// <summary>The tab with the page around its rounded ends made see-through, made from the game's texture on first use.</summary>
        private Texture2D? tab;

        /// <summary>Whether making the tab failed, so it isn't tried again every frame.</summary>
        private bool tabFailed;

        public override string Id => "community-center";

        public override string Title => I18n.Get("hud.title");

        public override int TitleAbove => TabAbove;

        public override int MinBoxWidth => TabLeft + this.TabWidth + 180;

        private int TitleWidth => SpriteText.getWidthOfString(this.Title);

        /// <summary>The tab's width on screen: its two ends and the stretched middle around the title.</summary>
        private int TabWidth => TabEnd * 2 * Scale + this.TitleWidth + TitleMargin * 2;

        public override Rectangle TitleArea(Rectangle box) =>
            new(box.X + TabLeft, box.Y - TabAbove, box.Width - TabLeft - 16, TabSource.Height * Scale);

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + TabLeft;
            int y = box.Y - TabAbove;

            if (this.GetTab() is { } tab)
            {
                int middle = this.TitleWidth + TitleMargin * 2;
                var left = new Rectangle(0, 0, TabEnd, TabSource.Height);
                var stretch = new Rectangle(TabSource.Width / 2, 0, 1, TabSource.Height);
                var right = new Rectangle(TabSource.Width - TabEnd, 0, TabEnd, TabSource.Height);
                b.Draw(tab, new Vector2(x, y), left, Fade(Color.White), 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);
                b.Draw(tab, new Vector2(x + TabEnd * Scale, y), stretch, Fade(Color.White), 0f, Vector2.Zero, new Vector2(middle, Scale), SpriteEffects.None, 1f);
                b.Draw(tab, new Vector2(x + TabEnd * Scale + middle, y), right, Fade(Color.White), 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);
            }

            int textX = x + TabEnd * Scale + TitleMargin;
            SpriteText.drawString(b, this.Title, textX, y + 10, width: this.TitleWidth + 16, alpha: Opacity);

            this.DrawJunimos(b, box, state.AnyAvailableNow);
        }

        private void DrawJunimos(SpriteBatch b, Rectangle box, bool cheering)
        {
            double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            for (int i = 0; i < Junimos.Length; i++)
            {
                (Color color, int fromRight, bool flip) = Junimos[i];

                // out of step with each other, so they don't move as one
                double t = now + i * 470;
                int frame = cheering
                    ? CheerFrame + (int)(t / CheerFrameMs) % FrameCount
                    : IdleFrame + (int)(t / IdleFrameMs) % FrameCount;

                // a quick hop now and then while cheering
                float hop = 0;
                double inHop = (t % HopEveryMs);
                if (cheering && inHop < HopMs)
                    hop = (float)Math.Sin(inHop / HopMs * Math.PI) * 14f;

                var source = new Rectangle(frame * JunimoSize % 128, frame * JunimoSize / 128 * JunimoSize, JunimoSize, JunimoSize);
                var position = new Vector2(box.Right - fromRight, box.Y - 40 - hop);
                b.Draw(Game1.content.Load<Texture2D>("Characters\\Junimo"), position, source, Fade(color), 0f, Vector2.Zero, Scale, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
            }
        }

        /// <summary>The tab, or null if the texture couldn't be read (e.g. a mod replaced it with a smaller one).</summary>
        private Texture2D? GetTab()
        {
            if (this.tab is { IsDisposed: false })
                return this.tab;
            if (this.tabFailed)
                return null;

            try
            {
                // the English book: translations can lay the page out differently
                Texture2D book = Game1.content.Load<Texture2D>("LooseSprites\\JunimoNote", LocalizedContentManager.LanguageCode.en);
                var pixels = new Color[TabSource.Width * TabSource.Height];
                book.GetData(0, TabSource, pixels, 0, pixels.Length);
                for (int y = 0; y < TabSource.Height; y++)
                {
                    for (int x = 0; x < TabSource.Width; x++)
                    {
                        if (x < TabInset[y] || x >= TabSource.Width - TabInset[y])
                            pixels[y * TabSource.Width + x] = Color.Transparent;
                    }
                }

                var texture = new Texture2D(Game1.graphics.GraphicsDevice, TabSource.Width, TabSource.Height);
                texture.SetData(pixels);
                return this.tab = texture;
            }
            catch (Exception)
            {
                // draw the title without it
                this.tabFailed = true;
                return null;
            }
        }
    }
}

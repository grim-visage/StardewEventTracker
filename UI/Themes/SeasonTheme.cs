using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A season's look, light or dark: a box in the season's colours, the title on a banner (the parchment scroll in light
    /// mode, dark metal in dark mode) with the season's companions at both ends, and the season's weather drifting down
    /// past the box: petals and a butterfly pair in spring, leaves (fireflies at night) and sunflowers in summer, leaves
    /// and pumpkins in fall, snow, a snowy box and crystal fruit in winter. The weather stays outside the box, so it
    /// never covers the text.
    /// </summary>
    internal sealed class SeasonTheme : HudTheme
    {
        /// <summary>The colours for one season in one mode.</summary>
        private readonly record struct Look(Color Paper, Color Frame, Color Title);

        private readonly Season season;
        private readonly bool dark;
        private readonly Look look;

        /// <summary>The banner's ends and middle: the parchment scroll (SpriteText style 0) in light mode, dark metal (style 2) in dark mode.</summary>
        private readonly Rectangle bannerLeft, bannerMiddle, bannerRight;

        /// <summary>How wide each banner end is on screen; the scroll's ends are wider than the metal's.</summary>
        private readonly int bannerEnd;

        /// <summary>Where the banner's content starts from the box's left edge, the same in both modes so nothing moves when switching.</summary>
        private const int ContentInset = 72;

        /// <summary>Where the banner's content sits relative to the box's top edge; the banner itself starts 12px higher.</summary>
        private const int TextAbove = 40;

        /// <summary>The companions are 16px sprites drawn at 3x, with this gap to the title.</summary>
        private const int CompanionScale = 3, CompanionGap = 10;

        private static int CompanionSize => 16 * CompanionScale;

        public SeasonTheme(Season season, bool dark)
        {
            this.season = season;
            this.dark = dark;
            this.look = (season, dark) switch
            {
                (Season.Spring, false) => new(new Color(242, 250, 234), new Color(108, 168, 88), new Color(70, 140, 60)),
                (Season.Spring, true) => new(new Color(30, 52, 38), new Color(96, 150, 92), new Color(255, 175, 215)),
                (Season.Summer, false) => new(new Color(255, 248, 222), new Color(222, 160, 40), new Color(205, 120, 0)),
                (Season.Summer, true) => new(new Color(24, 38, 66), new Color(70, 110, 170), new Color(255, 222, 100)),
                (Season.Fall, false) => new(new Color(253, 238, 220), new Color(196, 104, 44), new Color(185, 75, 20)),
                (Season.Fall, true) => new(new Color(60, 32, 26), new Color(160, 84, 44), new Color(255, 155, 65)),
                (Season.Winter, false) => new(new Color(238, 245, 255), new Color(112, 152, 204), new Color(55, 105, 175)),
                _ => new(new Color(28, 36, 64), new Color(110, 140, 196), new Color(195, 228, 255))
            };

            if (dark)
            {
                (this.bannerLeft, this.bannerMiddle, this.bannerRight, this.bannerEnd) = (new(327, 281, 3, 17), new(330, 281, 1, 17), new(333, 281, 3, 17), 12);
                this.Palette = new(new Color(235, 232, 240), new Color(140, 230, 110), new Color(255, 170, 70), new Color(185, 180, 200));
            }
            else
            {
                (this.bannerLeft, this.bannerMiddle, this.bannerRight, this.bannerEnd) = (new(325, 318, 12, 18), new(337, 318, 1, 18), new(338, 318, 12, 18), 48);
                this.Palette = new(Game1.textColor, new Color(20, 120, 40), new Color(180, 95, 0), new Color(105, 100, 110));
            }
        }

        /// <summary>The season's config ID ("spring", "summer"...). The "seasonal" theme picks one of these by the calendar.</summary>
        public override string Id => this.season.ToString().ToLowerInvariant();

        public override string Title => I18n.Get("hud.title");

        public override HudPalette Palette { get; }

        public override int TitleAbove => TextAbove + 16;

        public override int MinBoxWidth => ContentInset + this.ContentWidth + this.bannerEnd + 24;

        /// <summary>The banner's content: a companion, the title and another companion.</summary>
        private int ContentWidth => CompanionSize * 2 + CompanionGap * 2 + SpriteText.getWidthOfString(this.Title);

        public override Rectangle TitleArea(Rectangle box) =>
            new(box.X + ContentInset - this.bannerEnd, box.Y - TextAbove - 12, this.ContentWidth + this.bannerEnd * 2 + 4, 18 * 4);

        public override void DrawBox(SpriteBatch b, Rectangle box, bool dragging)
        {
            Texture2D pixel = Game1.staminaRect;
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), Color.Black * 0.24f);
            b.Draw(pixel, box, dragging ? Color.Lerp(this.look.Frame, Color.White, 0.4f) : this.look.Frame);
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8), this.look.Paper * 0.96f);

            if (this.season == Season.Winter)
                DrawSnowCap(b, box);
        }

        public override void DrawLine(SpriteBatch b, string text, Vector2 position, Color color)
        {
            if (this.dark)
                DrawDarkLine(b, text, position, color);
            else
                b.DrawString(Game1.smallFont, text, position, color);
        }

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + ContentInset;
            int y = box.Y - TextAbove;
            int content = this.ContentWidth;
            double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;

            // the banner, laid out like SpriteText.drawString lays out its scrolls around their text
            Texture2D sheet = Game1.mouseCursors;
            b.Draw(sheet, new Vector2(x - this.bannerEnd, y - 12), this.bannerLeft, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x, y - 12), this.bannerMiddle, Color.White, 0f, Vector2.Zero, new Vector2(content, 4f), SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x + content, y - 12), this.bannerRight, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            this.DrawCompanion(b, new Vector2(x, y - 4), now, right: false);
            this.DrawCompanion(b, new Vector2(x + content - CompanionSize, y - 4), now, right: true);
            string title = this.Title;
            SpriteText.drawString(b, title, x + CompanionSize + CompanionGap, y, width: SpriteText.getWidthOfString(title) + 16, color: this.look.Title);

            this.DrawWeather(b, box, x + content + this.bannerEnd, now);
        }

        /****
        ** Companions
        ****/
        /// <summary>The season's companion at one end of the banner, its top-left at <paramref name="at"/>.</summary>
        private void DrawCompanion(SpriteBatch b, Vector2 at, double now, bool right)
        {
            double t = now + (right ? 900 : 0);
            switch (this.season)
            {
                // butterflies flapping (pink, then yellow), as the game's spring butterflies do
                case Season.Spring:
                {
                    int baseFrame = right ? 136 : 152;
                    int[] flap = { 0, 1, 2, 3, 2, 1 };
                    int frame = baseFrame + flap[(int)(t / 110) % flap.Length];
                    Texture2D critters = Game1.content.Load<Texture2D>("TileSheets\\critters");
                    var source = new Rectangle(frame * 16 % critters.Width, frame * 16 / critters.Width * 16, 16, 16);
                    float bob = (float)Math.Sin(t / 500) * 2f;
                    b.Draw(critters, at + new Vector2(0, bob), source, Color.White, 0f, Vector2.Zero, CompanionScale, right ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
                    break;
                }

                // sunflowers swaying, pumpkins rocking gently, both from their bottoms
                case Season.Summer:
                case Season.Fall:
                {
                    bool summer = this.season == Season.Summer;
                    float angle = (float)Math.Sin(t / (summer ? 900 : 1300)) * (summer ? 0.12f : 0.07f);
                    DrawItem(b, summer ? "(O)421" : "(O)276", at, angle, right, Color.White);
                    break;
                }

                // crystal fruit shimmering
                default:
                {
                    DrawItem(b, "(O)414", at, 0f, right, Color.White);
                    float shimmer = (float)Math.Max(0, Math.Sin(t / 700)) * 0.35f;
                    if (shimmer > 0)
                        DrawItem(b, "(O)414", at, 0f, right, Color.White * shimmer);
                    break;
                }
            }
        }

        /// <summary>An item's sprite, rotated about its bottom middle, from its item data so retextures show.</summary>
        private static void DrawItem(SpriteBatch b, string itemId, Vector2 at, float angle, bool flip, Color color)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(itemId);
            Rectangle source = data.GetSourceRect();
            var origin = new Vector2(source.Width / 2f, source.Height);
            b.Draw(data.GetTexture(), at + new Vector2(CompanionSize / 2f, CompanionSize), source, color, angle, origin, CompanionScale, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
        }

        /****
        ** Weather
        ****/
        /// <summary>
        /// The season's weather, outside the box: some drifting down past its right edge, some across the space above it
        /// between the banner and the box's corner, fading in and out at the ends of their paths.
        /// </summary>
        private void DrawWeather(SpriteBatch b, Rectangle box, int bannerRight, double now)
        {
            if (this.season == Season.Summer && this.dark)
            {
                DrawFireflies(b, box, bannerRight, now);
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                double length = 7000 + i * 900;
                double p = (now + i * 1730) % length / length;
                float alpha = (float)Math.Min(1, Math.Min(p / 0.12, (1 - p) / 0.15));
                Vector2 position;
                if (i % 3 == 2)
                {
                    // across the top: from above the box's corner, drifting left and down towards the banner
                    float fromX = box.Right - 20, toX = Math.Min(bannerRight + 20, box.Right - 60);
                    position = new Vector2(MathHelper.Lerp(fromX, toX, (float)p), box.Y - 80 + (float)p * 56 + (float)Math.Sin(p * Math.PI * 3 + i) * 6);
                }
                else
                {
                    // down the right edge, swaying, never over the box
                    position = new Vector2(box.Right + 12 + (i % 3) * 22 + (float)Math.Sin(p * Math.PI * 3 + i) * 8, MathHelper.Lerp(box.Y - 80, box.Bottom + 30, (float)p));
                }
                this.DrawFlake(b, position, i, now, alpha);
            }
        }

        /// <summary>One petal, leaf or snowflake, from the game's weather sprites.</summary>
        private void DrawFlake(SpriteBatch b, Vector2 position, int i, double now, float alpha)
        {
            if (this.season == Season.Winter)
            {
                b.Draw(Game1.mouseCursors, position, new Rectangle(391 + 4 * (i % 5), 1236, 4, 4), Color.White * alpha, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                return;
            }

            int row = this.season switch { Season.Spring => 1184, Season.Summer => 1200, _ => 1216 };
            int frame = (int)((now + i * 370) / 150) % 6;
            b.Draw(Game1.mouseCursors, position, new Rectangle(352 + frame * 16, row, 16, 16), Color.White * (alpha * (this.dark ? 0.85f : 1f)), 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
        }

        /// <summary>Summer nights: fireflies wandering slowly around the box's corner and edge, glowing on and off.</summary>
        private static void DrawFireflies(SpriteBatch b, Rectangle box, int bannerRight, double now)
        {
            Vector2[] homes =
            {
                new(box.Right + 30, box.Y + 20),
                new(box.Right + 46, box.Y + 90),
                new(box.Right + 24, box.Bottom - 20),
                new((bannerRight + box.Right) / 2f, box.Y - 50),
                new(box.Right - 30, box.Y - 64)
            };
            for (int i = 0; i < homes.Length; i++)
            {
                var position = homes[i] + new Vector2((float)Math.Sin(now / 1700 + i * 2.1) * 18, (float)Math.Sin(now / 1300 + i * 1.3) * 12);
                float glow = (float)Math.Clamp(0.5 + Math.Sin(now / 420 + i * 1.7) * 0.7, 0, 1);
                if (glow <= 0)
                    continue;
                if (Game1.lantern != null)
                {
                    float scale = 36f / Game1.lantern.Width;
                    b.Draw(Game1.lantern, position, null, new Color(200, 255, 110) * (0.45f * glow), 0f, new Vector2(Game1.lantern.Width / 2f, Game1.lantern.Height / 2f), scale, SpriteEffects.None, 1f);
                }
                b.Draw(Game1.staminaRect, new Rectangle((int)position.X - 2, (int)position.Y - 2, 4, 4), new Color(240, 255, 170) * glow);
            }
        }

        /// <summary>Winter: snow piled along the top of the box, uneven in steps of the game's 4px pixels.</summary>
        private static void DrawSnowCap(SpriteBatch b, Rectangle box)
        {
            for (int x = box.X; x < box.Right; x += 4)
            {
                // a stable bumpy height from the position, not random each frame
                int height = 4 + (int)(4 * Math.Abs(Math.Sin(x / 37.0))) + ((x * 7919) % 5 == 0 ? 4 : 0);
                int width = Math.Min(4, box.Right - x);
                b.Draw(Game1.staminaRect, new Rectangle(x, box.Y - height + 4, width, height), new Color(250, 252, 255));
                b.Draw(Game1.staminaRect, new Rectangle(x, box.Y + 4, width, 4), new Color(200, 218, 240));
            }
        }
    }
}

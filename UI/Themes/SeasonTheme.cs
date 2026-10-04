using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A season's look, light or dark: a box in the season's colours with a seasonal border, and the title on a banner
    /// (the parchment scroll in light mode, dark metal in dark mode) with the season's companions at both ends: a frame
    /// of vines with blossoms and butterflies in spring, a frame of vines with yellow flowers and sunflowers in summer,
    /// fallen leaves piled on top and pumpkins in fall, snow on top, icicles underneath and crystal fruit in winter. The
    /// border keeps to the box's edges, so it never covers the text.
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

            // spring and summer frame the box with vines instead
            bool vines = this.season is Season.Spring or Season.Summer;
            if (!vines || dragging)
                b.Draw(pixel, box, dragging ? Color.Lerp(this.look.Frame, Color.White, 0.4f) : this.look.Frame);
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8), this.look.Paper * 0.96f);

            switch (this.season)
            {
                case Season.Spring:
                    DrawSpringBorder(b, box);
                    break;
                case Season.Summer:
                    DrawSummerBorder(b, box);
                    break;
                case Season.Fall:
                    DrawFallBorder(b, box);
                    break;
                default:
                    DrawWinterBorder(b, box);
                    break;
            }
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
        ** Borders
        ****/
        /// <summary>The game's pixels are 4 screen pixels at the HUD's scale; the borders are drawn in those.</summary>
        private const int Px = 4;

        private static readonly Color BlossomPink = new(255, 168, 204), BlossomPinkLight = new(255, 214, 232);
        private static readonly Color BlossomWhite = new(255, 240, 246), BlossomCenter = new(255, 214, 80);
        private static readonly Color VineDark = new(44, 96, 34), VineLight = new(98, 172, 66), VineKnot = new(68, 134, 48), VineLeaf = new(120, 196, 72), VineLeafShade = new(40, 92, 30), VineLeafVein = new(170, 220, 110);
        private static readonly Color SummerPetal = new(255, 206, 50), SummerPetalLight = new(255, 236, 140), SummerCenter = new(150, 86, 36);
        private static readonly Color Snow = new(250, 252, 255), SnowShade = new(205, 222, 242), Ice = new(225, 240, 255), IceTip = new(160, 200, 245), IceTop = new(240, 248, 255);

        /// <summary>Spring: a frame of vines, with leaves and pink and white blossoms growing out of it.</summary>
        private static void DrawSpringBorder(SpriteBatch b, Rectangle box)
        {
            DrawVineFrame(b, box, (x, y, n) =>
            {
                if (n % 2 == 0)
                    DrawBlossom(b, x, y, BlossomPink, BlossomPinkLight, BlossomCenter);
                else
                    DrawBlossom(b, x, y, BlossomWhite, Color.White, BlossomCenter);
            });
        }

        /// <summary>Summer: a frame of vines, with leaves and yellow flowers growing out of it.</summary>
        private static void DrawSummerBorder(SpriteBatch b, Rectangle box)
        {
            DrawVineFrame(b, box, (x, y, _) => DrawBlossom(b, x, y, SummerPetal, SummerPetalLight, SummerCenter));
        }

        /// <summary>
        /// A braided vine all the way around the box, in place of its frame (light and dark strands swapping every
        /// 12px), with leaves and flowers growing outward from it on every side.
        /// </summary>
        /// <param name="drawFlower">Draws a flower centred near (x, y); the third value counts the flowers, e.g. to alternate colours.</param>
        private static void DrawVineFrame(SpriteBatch b, Rectangle box, Action<int, int, int> drawFlower)
        {
            // the strands, along the top and bottom, then down the sides
            foreach (int y in new[] { box.Y - 2, box.Bottom - 6 })
            {
                for (int x = box.X - 2; x < box.Right + 2; x += Px)
                    DrawStrand(b, x, y, (x - box.X) / 12 % 2 == 1, (x - box.X) % 12 == 8, across: true);
            }
            foreach (int x in new[] { box.X - 2, box.Right - 6 })
            {
                for (int y = box.Y - 2; y < box.Bottom + 2; y += Px)
                    DrawStrand(b, x, y, (y - box.Y) / 12 % 2 == 1, (y - box.Y) % 12 == 8, across: false);
            }

            int flowers = 0;
            foreach ((int y, bool up) in new[] { (box.Y, true), (box.Bottom - 4, false) })
            {
                int k = 0;
                for (int x = box.X + 14; x < box.Right - 10; x += 30, k++)
                {
                    if (k % 3 == 1)
                        drawFlower(x + 4, y + (up ? -10 : 14), flowers++);
                    else
                        DrawVineLeaf(b, x, y + (up ? -6 : 6), up, flip: k % 2 == 1);
                }
            }
            foreach ((int x, bool left) in new[] { (box.X, true), (box.Right - 4, false) })
            {
                int k = 0;
                for (int y = box.Y + 22; y < box.Bottom - 16; y += 34, k++)
                {
                    if (k % 2 == 1)
                        drawFlower(x + (left ? -10 : 14), y + 2, flowers++);
                    else
                        DrawSideLeaf(b, x + (left ? -6 : 6), y, left, flip: k % 4 == 2);
                }
            }
        }

        /// <summary>One 4px step of the vine: two strands side by side, swapping light and dark, with a knot where they twist.</summary>
        private static void DrawStrand(SpriteBatch b, int x, int y, bool swapped, bool knot, bool across)
        {
            Color first = swapped ? VineLight : VineDark, second = swapped ? VineDark : VineLight;
            if (knot)
                first = second = VineKnot;
            Fill(b, x, y, Px, Px, first);
            if (across)
                Fill(b, x, y + Px, Px, Px, second);
            else
                Fill(b, x + Px, y, Px, Px, second);
        }

        /// <summary>Fall: fallen leaves piled two deep along the top, and a couple caught at each bottom corner.</summary>
        private static void DrawFallBorder(SpriteBatch b, Rectangle box)
        {
            int i = 0;
            foreach ((int row, int dy) in new[] { (0, -28), (1, -16) })
            {
                for (int x = box.X - 12 + row * 14; x < box.Right - 24; x += 26, i++)
                {
                    // a stable choice of leaf, flip and height for each spot, so the pile doesn't shuffle
                    uint h = Hash(i);
                    DrawFallLeaf(b, x, box.Y + dy + (int)(h % 3) * 3, (int)(h / 3 % 6), flip: h / 18 % 2 == 0);
                }
            }
            DrawFallLeaf(b, box.X - 16, box.Bottom - 34, 5, flip: false);
            DrawFallLeaf(b, box.X - 2, box.Bottom - 22, 2, flip: false);
            DrawFallLeaf(b, box.Right - 32, box.Bottom - 34, 5, flip: true);
            DrawFallLeaf(b, box.Right - 38, box.Bottom - 22, 2, flip: false);
        }

        /// <summary>Winter: snow piled unevenly along the top, and icicles of different lengths hanging underneath.</summary>
        private static void DrawWinterBorder(SpriteBatch b, Rectangle box)
        {
            for (int x = box.X - 4; x < box.Right + 4; x += Px)
            {
                // the bumps follow the box, so the snow doesn't shift while it's dragged
                int at = x - box.X;
                int height = 8 + (int)(6 * Math.Abs(Math.Sin(at / 41.0))) + (Hash(at) % 3 == 0 ? 4 : 0);
                Fill(b, x, box.Y - height + 4, Px, height, Snow);
                Fill(b, x, box.Y + 4, Px, Px, SnowShade);
            }

            int[] lengths = { 5, 8, 4, 7, 6 };
            int k = 0;
            for (int x = box.X + 16; x < box.Right - 16; x += 30, k++)
            {
                int length = lengths[k % lengths.Length];
                for (int j = 0; j < length; j++)
                {
                    int width = j < length - 2 ? 8 : 4;
                    Fill(b, x + (8 - width) / 2, box.Bottom + j * Px, width, Px, j < length - 1 ? Ice : IceTip);
                }
                Fill(b, x - 4, box.Bottom, 16, Px, IceTop);
            }
        }

        /// <summary>A five-petal flower about 16px across, centred near (x, y).</summary>
        private static void DrawBlossom(SpriteBatch b, int x, int y, Color petal, Color light, Color center)
        {
            foreach ((int dx, int dy) in new[] { (-1, -2), (0, -2), (-2, -1), (1, -1), (-2, 0), (1, 0), (-1, 1), (0, 1) })
                Fill(b, x + dx * Px, y + dy * Px, Px, Px, petal);
            Fill(b, x - Px, y - Px, Px, Px, light);
            Fill(b, x, y - Px, Px, Px, center);
            Fill(b, x - Px, y, Px, Px, center);
            Fill(b, x, y, Px, Px, center);
        }

        /// <summary>A vine leaf growing up (above the vine) or down (below it), leaning one way or the other.</summary>
        private static void DrawVineLeaf(SpriteBatch b, int x, int y, bool up, bool flip)
        {
            (int dx, int dy, Color color)[] shape =
            {
                (0, 0, VineLeafShade), (1, 0, VineLeaf), (2, 0, VineLeaf), (1, 1, VineLeaf), (2, 1, VineLeafVein), (3, 1, VineLeaf), (2, 2, VineLeaf), (3, 2, VineLeaf)
            };
            foreach ((int dx, int dy, Color color) in shape)
                Fill(b, x + (flip ? -dx : dx) * Px, y + (up ? -dy : dy) * Px, Px, Px, color);
        }

        /// <summary>A vine leaf growing out to the left or right of a side of the frame, leaning up or down.</summary>
        private static void DrawSideLeaf(SpriteBatch b, int x, int y, bool left, bool flip)
        {
            (int dy, int dx, Color color)[] shape =
            {
                (0, 0, VineLeafShade), (0, 1, VineLeaf), (0, 2, VineLeaf), (1, 1, VineLeaf), (1, 2, VineLeafVein), (1, 3, VineLeaf), (2, 2, VineLeaf), (2, 3, VineLeaf)
            };
            foreach ((int dy, int dx, Color color) in shape)
                Fill(b, x + (left ? -dx : dx) * Px, y + (flip ? -dy : dy) * Px, Px, Px, color);
        }

        /// <summary>One of the game's fall weather leaves, still.</summary>
        private static void DrawFallLeaf(SpriteBatch b, int x, int y, int frame, bool flip)
        {
            b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(352 + frame * 16, 1216, 16, 16), Color.White, 0f, Vector2.Zero, 3f, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 1f);
        }

        private static void Fill(SpriteBatch b, int x, int y, int width, int height, Color color) =>
            b.Draw(Game1.staminaRect, new Rectangle(x, y, width, height), color);

        /// <summary>A stable pseudo-random number for a position, so decorations look scattered but stay put.</summary>
        private static uint Hash(int value)
        {
            uint h = (uint)value * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            return h ^ (h >> 13);
        }
    }
}

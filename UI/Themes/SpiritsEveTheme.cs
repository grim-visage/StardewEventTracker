using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A dark theme for Spirit's Eve: the box in night purple with light text, and the title in pumpkin orange on the
    /// game's dark-metal banner, after a ghost bobbing at the banner's left end and a flickering jack-o'-lantern.
    /// </summary>
    internal sealed class SpiritsEveTheme : HudTheme
    {
        /// <summary>The dark-metal banner's left end, middle (stretched) and right end, from SpriteText's scroll style 2.</summary>
        private static readonly Rectangle BannerLeft = new(327, 281, 3, 17);
        private static readonly Rectangle BannerMiddle = new(330, 281, 1, 17);
        private static readonly Rectangle BannerRight = new(333, 281, 3, 17);

        /// <summary>The colour the parchment box is multiplied by, turning it night purple.</summary>
        private static readonly Color BoxTint = new(62, 44, 92);

        private static readonly Color TitleColor = new(255, 140, 30);

        /// <summary>The jack-o'-lantern item, drawn from its item data so a retexture shows.</summary>
        private const string LanternItem = "(O)746";

        private const int LanternScale = 3;

        /// <summary>How far the banner starts in from the box's left edge.</summary>
        private const int BannerInset = 36;

        /// <summary>The gap between the jack-o'-lantern and the title.</summary>
        private const int LanternGap = 10;

        /// <summary>Where the banner's content sits relative to the box's top edge; the banner itself starts 12px higher.</summary>
        private const int TextAbove = 40;

        /// <summary>The ghost's frames are 16x24 pixels; its first row is the animation used here.</summary>
        private const int GhostWidth = 16, GhostHeight = 24, GhostScale = 2;

        /// <summary>The gap between the ghost and the jack-o'-lantern.</summary>
        private const int GhostGap = 8;

        public override string Id => "spirits-eve";

        public override string Title => I18n.Get("hud.title");

        /// <summary>Light text for the dark box: pale lavender, with green and pumpkin orange for what's happening.</summary>
        public override HudPalette Palette { get; } = new(new Color(235, 225, 240), new Color(140, 230, 110), new Color(255, 165, 60), new Color(190, 175, 205));

        /// <summary>The banner starts 52px above the box.</summary>
        public override int TitleAbove => TextAbove + 12;

        /// <summary>Room for the banner, with the same margin on the right as on the left.</summary>
        public override int MinBoxWidth => BannerInset * 2 + this.BannerInnerWidth + 16;

        private static int LanternSize => 16 * LanternScale;

        private static int GhostSize => GhostWidth * GhostScale;

        /// <summary>The ghost, the jack-o'-lantern, then the title.</summary>
        private int BannerInnerWidth => GhostSize + GhostGap + LanternSize + LanternGap + this.TitleWidth;

        /// <summary>The title's words, drawn one by one so the gaps between them can be narrower than SpriteText's spaces.</summary>
        private string[] TitleWords => this.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>The gap between the title's words: half a SpriteText space.</summary>
        private static int WordGap => (SpriteText.getWidthOfString("a a") - SpriteText.getWidthOfString("aa")) / 2;

        private int TitleWidth
        {
            get
            {
                string[] words = this.TitleWords;
                return words.Sum(w => SpriteText.getWidthOfString(w)) + WordGap * Math.Max(0, words.Length - 1);
            }
        }

        public override Rectangle TitleArea(Rectangle box) =>
            new(box.X + BannerInset - 12, box.Y - TextAbove - 12, this.BannerInnerWidth + 28, BannerMiddle.Height * 4);

        public override void DrawBox(SpriteBatch b, Rectangle box, bool dragging)
        {
            Color tint = dragging ? new Color(95, 72, 135) : BoxTint;
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), box.X, box.Y, box.Width, box.Height, Fade(tint * 0.95f), 4f, drawShadow: false);
        }

        public override void DrawLine(SpriteBatch b, string text, Vector2 position, Color color) => DrawDarkLine(b, text, position, color);

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + BannerInset;
            int y = box.Y - TextAbove;
            int inner = this.BannerInnerWidth;
            DrawBanner(b, Game1.mouseCursors, BannerLeft, BannerMiddle, BannerRight, x, y, inner);

            int lanternX = x + GhostSize + GhostGap;
            DrawGlow(b, lanternX, y);

            ParsedItemData lantern = ItemRegistry.GetDataOrErrorItem(LanternItem);
            Texture2D texture = lantern.GetTexture();
            Rectangle source = lantern.GetSourceRect();
            b.Draw(texture, new Vector2(lanternX, y - 4), source, Fade(Color.White), 0f, Vector2.Zero, LanternScale, SpriteEffects.None, 1f);

            int wordX = lanternX + LanternSize + LanternGap;
            foreach (string word in this.TitleWords)
            {
                int width = SpriteText.getWidthOfString(word);
                SpriteText.drawString(b, word, wordX, y, width: width + 16, alpha: Opacity, color: TitleColor);
                wordX += width + WordGap;
            }

            DrawGhost(b, x, y - 4, Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0);
        }

        /// <summary>A ghost floating gently up and down, slightly see-through, at (x, y).</summary>
        private static void DrawGhost(SpriteBatch b, int x, int y, double now)
        {
            int frame = (int)(now / 450) % 4;
            float bob = (float)Math.Sin(now / 1200) * 3f;
            var source = new Rectangle(frame * GhostWidth, 0, GhostWidth, GhostHeight);
            b.Draw(Game1.content.Load<Texture2D>("Characters\\Monsters\\Ghost"), new Vector2(x, y + bob), source, Fade(Color.White * 0.85f), 0f, Vector2.Zero, GhostScale, SpriteEffects.None, 1f);
        }


        /// <summary>A flickering candle glow behind the jack-o'-lantern whose top-left is at (x, y).</summary>
        private static void DrawGlow(SpriteBatch b, int x, int y)
        {
            if (Game1.lantern == null)
                return;

            double t = (Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0);
            float flicker = 0.32f + 0.06f * (float)Math.Sin(t / 170) + 0.04f * (float)Math.Sin(t / 53);
            float size = LanternSize + 40;
            float scale = size / Game1.lantern.Width;
            var center = new Vector2(x + LanternSize / 2f, y - 4 + LanternSize / 2f);
            b.Draw(Game1.lantern, center, null, Fade(new Color(255, 150, 40) * flicker), 0f, new Vector2(Game1.lantern.Width / 2f, Game1.lantern.Height / 2f), scale, SpriteEffects.None, 1f);
        }
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A Joja product, in dark mode: the Joja logo and the title on the blue-metal banner the game uses for Joja's own
    /// text, over a box in Joja's Community Development form colours, darkened: a Joja-blue frame around deep navy,
    /// with the form's light-blue rule under the text.
    /// </summary>
    internal sealed class JojaTheme : HudTheme
    {
        /// <summary>The Joja logo and sunburst on the Community Development form (down to the j's tail), in the English texture.</summary>
        private static readonly Rectangle LogoSource = new(15, 6, 62, 30);

        /// <summary>The blue-metal banner's left end, middle (stretched) and right end, from SpriteText's scroll style 3.</summary>
        private static readonly Rectangle BannerLeft = new(86, 145, 3, 17);
        private static readonly Rectangle BannerMiddle = new(89, 145, 1, 17);
        private static readonly Rectangle BannerRight = new(92, 145, 3, 17);

        /// <summary>The banner's scale: the game's 4x across, and 5x tall so the whole logo fits inside its dark middle.</summary>
        private static readonly Vector2 BannerScale = new(4, 5);

        private const int LogoScale = 2;

        /// <summary>How far the banner's content starts in from the box's left edge.</summary>
        private const int BannerInset = 36;

        /// <summary>The space between the banner's ends and its content, and between the logo and the title.</summary>
        private const int BannerMargin = 4, LogoGap = 16;

        /// <summary>How far the banner's top sits above the box; it overlaps the box's frame below that.</summary>
        private const int BannerAbove = 66;

        /// <summary>The box's colours: the form's Joja-blue frame and light-blue rule, around deep navy.</summary>
        private static readonly Color Frame = new(93, 90, 158), Paper = new(32, 31, 56), Rule = new(137, 182, 255);

        /// <summary>The logo with the form's white paper made see-through, made from the game's texture on first use.</summary>
        private Texture2D? logo;

        /// <summary>Whether making the logo failed, so it isn't tried again every frame.</summary>
        private bool logoFailed;

        public override string Id => "joja";

        public override string Title => I18n.Get("hud.title.joja");

        /// <summary>Light text for the dark box.</summary>
        public override HudPalette Palette { get; } = new(new Color(225, 230, 255), new Color(130, 225, 140), new Color(255, 185, 80), new Color(160, 168, 210));

        public override int TitleAbove => BannerAbove;

        public override int MinBoxWidth => BannerInset + this.BannerInnerWidth + 40;

        private int LogoWidth => LogoSource.Width * LogoScale;

        /// <summary>The banner's middle: the logo, the gap and the title, with a margin each side.</summary>
        private int BannerInnerWidth => BannerMargin + this.LogoWidth + LogoGap + SpriteText.getWidthOfString(this.Title) + BannerMargin * 2;

        private static int BannerHeight => (int)(BannerMiddle.Height * BannerScale.Y);

        public override Rectangle TitleArea(Rectangle box)
        {
            int x = box.X + BannerInset;
            return new Rectangle(x - 16, box.Y - BannerAbove, this.BannerInnerWidth + 32, BannerHeight);
        }

        public override void DrawBox(SpriteBatch b, Rectangle box, bool dragging)
        {
            Texture2D pixel = Game1.staminaRect;
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), Color.Black * 0.27f);
            b.Draw(pixel, box, dragging ? Rule : Frame);
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8), Paper * 0.96f);
            b.Draw(pixel, new Rectangle(box.X + 8, box.Bottom - 14, box.Width - 16, 4), Rule);
        }

        public override void DrawLine(SpriteBatch b, string text, Vector2 position, Color color) => DrawDarkLine(b, text, position, color);

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + BannerInset;
            int top = box.Y - BannerAbove;
            int inner = this.BannerInnerWidth;
            Texture2D sheet = Game1.mouseCursors_1_6;

            // the game's banner (SpriteText scroll style 3), drawn taller than the game draws it
            b.Draw(sheet, new Vector2(x - 16, top), BannerLeft, Color.White, 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4, top), BannerMiddle, Color.White, 0f, Vector2.Zero, new Vector2(inner, BannerScale.Y), SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4 + inner, top), BannerRight, Color.White, 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);

            // centred in the banner's dark middle (rows 2-14 of 17)
            int logoX = x + BannerMargin;
            int logoY = top + (int)(2 * BannerScale.Y) + ((int)(13 * BannerScale.Y) - LogoSource.Height * LogoScale) / 2;
            if (this.GetLogo() is { } logo)
                b.Draw(logo, new Vector2(logoX, logoY), null, Color.White, 0f, Vector2.Zero, LogoScale, SpriteEffects.None, 1f);

            int textX = logoX + this.LogoWidth + LogoGap;
            SpriteText.drawString(b, this.Title, textX, top + 22, width: SpriteText.getWidthOfString(this.Title) + 16, color: Color.White);
        }

        /// <summary>The logo, or null if the texture couldn't be read (e.g. a mod replaced it with a smaller one).</summary>
        private Texture2D? GetLogo()
        {
            if (this.logo is { IsDisposed: false })
                return this.logo;
            if (this.logoFailed)
                return null;

            try
            {
                // always the English form: the Russian one spells the logo out (Джоджо) and is laid out differently
                Texture2D form = Game1.content.Load<Texture2D>("LooseSprites\\JojaCDForm", LocalizedContentManager.LanguageCode.en);
                var pixels = new Color[LogoSource.Width * LogoSource.Height];
                form.GetData(0, LogoSource, pixels, 0, pixels.Length);
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].R > 240 && pixels[i].G > 240 && pixels[i].B > 240)
                        pixels[i] = Color.Transparent;
                }

                var texture = new Texture2D(Game1.graphics.GraphicsDevice, LogoSource.Width, LogoSource.Height);
                texture.SetData(pixels);
                return this.logo = texture;
            }
            catch (Exception)
            {
                // draw the banner without it
                this.logoFailed = true;
                return null;
            }
        }
    }
}

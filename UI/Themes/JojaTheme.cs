using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A Joja product: the Joja logo and the title on the blue-metal banner the game uses for Joja's own text,
    /// floating above the parchment box.
    /// </summary>
    internal sealed class JojaTheme : HudTheme
    {
        /// <summary>The Joja logo and sunburst on the Community Development form, in the English texture.</summary>
        private static readonly Rectangle LogoSource = new(15, 6, 62, 26);

        /// <summary>The blue-metal banner's left end, middle (stretched) and right end, from SpriteText's scroll style 3.</summary>
        private static readonly Rectangle BannerLeft = new(86, 145, 3, 17);
        private static readonly Rectangle BannerMiddle = new(89, 145, 1, 17);
        private static readonly Rectangle BannerRight = new(92, 145, 3, 17);

        private const float BannerScale = 4f;
        private const int LogoScale = 2;

        /// <summary>How far the banner starts in from the box's left edge.</summary>
        private const int BannerInset = 36;

        /// <summary>The gap between the logo and the title.</summary>
        private const int LogoGap = 12;

        /// <summary>Where the banner's text sits relative to the box's top edge; the banner itself starts 12px higher.</summary>
        private const int TextAbove = 40;

        /// <summary>The logo with the form's white paper made see-through, made from the game's texture on first use.</summary>
        private Texture2D? logo;

        /// <summary>Whether making the logo failed, so it isn't tried again every frame.</summary>
        private bool logoFailed;

        public override string Id => "joja";

        public override string Title => I18n.Get("hud.title.joja");

        public override int TitleAbove => TextAbove + 12;

        public override int MinBoxWidth => BannerInset + this.BannerInnerWidth + 40;

        private int LogoWidth => LogoSource.Width * LogoScale;

        private int BannerInnerWidth => this.LogoWidth + LogoGap + SpriteText.getWidthOfString(this.Title);

        public override Rectangle TitleArea(Rectangle box)
        {
            int x = box.X + BannerInset;
            return new Rectangle(x - 12, box.Y - this.TitleAbove, this.BannerInnerWidth + 28, (int)(BannerMiddle.Height * BannerScale));
        }

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + BannerInset;
            int y = box.Y - TextAbove;
            int inner = this.BannerInnerWidth;

            // the banner, laid out like SpriteText.drawString lays out scroll style 3 around its text
            b.Draw(Game1.mouseCursors_1_6, new Vector2(x - 12, y - 12), BannerLeft, Color.White, 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);
            b.Draw(Game1.mouseCursors_1_6, new Vector2(x, y - 12), BannerMiddle, Color.White, 0f, Vector2.Zero, new Vector2(inner + 4, BannerScale), SpriteEffects.None, 1f);
            b.Draw(Game1.mouseCursors_1_6, new Vector2(x + inner + 4, y - 12), BannerRight, Color.White, 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);

            if (this.GetLogo() is { } logo)
                b.Draw(logo, new Vector2(x, y - 6), null, Color.White, 0f, Vector2.Zero, LogoScale, SpriteEffects.None, 1f);

            int textX = x + this.LogoWidth + LogoGap;
            SpriteText.drawString(b, this.Title, textX, y, width: SpriteText.getWidthOfString(this.Title) + 16, color: Color.White);
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

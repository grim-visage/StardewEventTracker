using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A Joja product, in dark mode: Joja's logo (the smiling "Joja.") and the title on the game's metal banner,
    /// recoloured in the blues of a Joja Cola can, over a box in the same colours: a Joja-blue frame around deep Joja
    /// navy, with a light-blue rule under the text. A row of status lights on the box blinks like an office computer's
    /// (the green one flashes when an event can happen now).
    /// </summary>
    internal sealed class JojaTheme : HudTheme
    {
        /// <summary>Joja's logo, "Joja." with a smile under it, on the sign above the movie theater's concession stand.</summary>
        private static readonly Rectangle LogoSource = new(72, 3, 20, 11);

        /// <summary>The metal banner (SpriteText's scroll style 3) in the game's texture: its left end, middle and right end side by side.</summary>
        private static readonly Rectangle BannerSource = new(86, 145, 9, 17);

        /// <summary>The banner's left end, middle (stretched) and right end, in the recoloured banner.</summary>
        private static readonly Rectangle BannerLeft = new(0, 0, 3, 17);
        private static readonly Rectangle BannerMiddle = new(3, 0, 1, 17);
        private static readonly Rectangle BannerRight = new(6, 0, 3, 17);

        /// <summary>The Joja Cola can's blues, from the darkest: navy, deep blue, Joja blue and light blue.</summary>
        private static readonly Color Navy = new(12, 10, 96), DeepBlue = new(26, 35, 216), JojaBlue = new(0, 113, 255), LightBlue = new(102, 193, 255);

        /// <summary>The banner's grey metal shades, darkest first, and the Joja blues each becomes.</summary>
        private static readonly Dictionary<Color, Color> BannerColors = new()
        {
            [new Color(26, 26, 43)] = Navy,
            [new Color(81, 81, 112)] = DeepBlue,
            [new Color(106, 106, 130)] = JojaBlue,
            [new Color(149, 135, 150)] = LightBlue
        };

        /// <summary>The banner's scale, the game's 4x; the logo fits inside its dark middle.</summary>
        private static readonly Vector2 BannerScale = new(4, 4);

        private const int LogoScale = 4;

        /// <summary>How far the banner's content starts in from the box's left edge.</summary>
        private const int BannerInset = 36;

        /// <summary>The space between the banner's ends and its content, and between the logo and the title.</summary>
        private const int BannerMargin = 4, LogoGap = 16;

        /// <summary>How far the banner's top sits above the box; it overlaps the box's frame below that.</summary>
        private const int BannerAbove = 49;

        /// <summary>The box's colours: a Joja-blue frame and light-blue rule around a navy a shade darker than the banner's.</summary>
        private static readonly Color Frame = JojaBlue, Paper = new(10, 12, 62), Rule = LightBlue;

        /// <summary>The status lights: colour, how long each blink lasts, and how often it's on, in percent.</summary>
        private static readonly (Color Color, double PeriodMs, int OnPercent)[] Lights =
        {
            (LightBlue, 2400, 85),                  // power: on, with the odd blink
            (new Color(130, 225, 140), 380, 45),    // activity: a busy flicker
            (new Color(255, 185, 80), 1300, 35),
            (LightBlue, 900, 55)
        };

        /// <summary>A status light's size, and the space between lights, in pixels.</summary>
        private const int LightSize = 6, LightGap = 8;

        /// <summary>The logo with the sign around it made see-through, made from the game's texture on first use.</summary>
        private Texture2D? logo;

        /// <summary>Whether making the logo failed, so it isn't tried again every frame.</summary>
        private bool logoFailed;

        /// <summary>The banner recoloured in Joja's blues, made from the game's texture on first use.</summary>
        private Texture2D? banner;

        /// <summary>Whether making the banner failed, so the game's own is drawn instead.</summary>
        private bool bannerFailed;

        public override string Id => "joja";

        public override string Title => I18n.Get("hud.title.joja");

        /// <summary>Light text for the dark box.</summary>
        public override HudPalette Palette { get; } = new(new Color(230, 238, 255), new Color(130, 225, 140), new Color(255, 185, 80), new Color(150, 180, 235));

        public override int TitleAbove => BannerAbove;

        public override int MinBoxWidth => BannerInset + this.BannerInnerWidth + 40;

        private int LogoWidth => LogoSource.Width * LogoScale;

        /// <summary>The banner's middle: the logo, the gap and the title, with a margin each side.</summary>
        private int BannerInnerWidth => BannerMargin + this.LogoWidth + LogoGap + this.TitleWidth + BannerMargin * 2;

        private static int BannerHeight => (int)(BannerMiddle.Height * BannerScale.Y);

        public override Rectangle TitleArea(Rectangle box)
        {
            int x = box.X + BannerInset;
            return new Rectangle(x - 16, box.Y - BannerAbove, this.BannerInnerWidth + 32, BannerHeight);
        }

        public override void DrawBox(SpriteBatch b, Rectangle box, bool dragging)
        {
            Texture2D pixel = Game1.staminaRect;
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width, box.Height), Fade(Color.Black * 0.27f));
            b.Draw(pixel, box, Fade(dragging ? Rule : Frame));
            b.Draw(pixel, new Rectangle(box.X + 4, box.Y + 4, box.Width - 8, box.Height - 8), Fade(Paper * 0.96f));
            int lightsWidth = Lights.Length * (LightSize + LightGap);
            b.Draw(pixel, new Rectangle(box.X + 8, box.Bottom - 14, box.Width - 16 - lightsWidth, 4), Fade(Rule));
        }

        public override void DrawLine(SpriteBatch b, string text, Vector2 position, Color color) => DrawDarkLine(b, text, position, color);

        public override void DrawTitle(SpriteBatch b, Rectangle box, HudState state)
        {
            int x = box.X + BannerInset;
            int top = box.Y - BannerAbove;
            int inner = this.BannerInnerWidth;
            Texture2D? recoloured = this.GetBanner();
            Texture2D sheet = recoloured ?? Game1.mouseCursors_1_6;
            Point offset = recoloured == null ? BannerSource.Location : Point.Zero;
            Rectangle Part(Rectangle part) => new(part.X + offset.X, part.Y + offset.Y, part.Width, part.Height);

            // the banner
            b.Draw(sheet, new Vector2(x - 16, top), Part(BannerLeft), Fade(Color.White), 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4, top), Part(BannerMiddle), Fade(Color.White), 0f, Vector2.Zero, new Vector2(inner, BannerScale.Y), SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4 + inner, top), Part(BannerRight), Fade(Color.White), 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);

            // centred in the banner's dark middle (rows 2-14 of 17)
            int logoX = x + BannerMargin;
            int logoY = top + (int)(2 * BannerScale.Y) + ((int)(13 * BannerScale.Y) - LogoSource.Height * LogoScale) / 2;
            if (this.GetLogo() is { } logo)
                b.Draw(logo, new Vector2(logoX, logoY), null, Fade(Color.White), 0f, Vector2.Zero, LogoScale, SpriteEffects.None, 1f);

            int textX = logoX + this.LogoWidth + LogoGap;
            this.DrawTitleText(b, textX, top + 13, Color.White);

            double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            DrawLights(b, box, now, state.AnyAvailableNow);
        }

        /// <summary>The row of status lights at the right end of the box's rule, each blinking in its own rhythm.</summary>
        private static void DrawLights(SpriteBatch b, Rectangle box, double now, bool alert)
        {
            int x = box.Right - 8 - Lights.Length * (LightSize + LightGap) + LightGap;
            int y = box.Bottom - 15;
            for (int i = 0; i < Lights.Length; i++)
            {
                (Color color, double period, int onPercent) = Lights[i];
                long slot = (long)(now / period);
                bool on = (Hash(slot, i) % 100) < onPercent;

                // the activity light flashes steadily while an event can happen right now
                if (alert && i == 1)
                    on = (long)(now / 300) % 2 == 0;

                var light = new Rectangle(x + i * (LightSize + LightGap), y, LightSize, LightSize);
                if (on && Game1.lantern != null)
                {
                    float scale = 30f / Game1.lantern.Width;
                    b.Draw(Game1.lantern, light.Center.ToVector2(), null, Fade(color * 0.35f), 0f, new Vector2(Game1.lantern.Width / 2f, Game1.lantern.Height / 2f), scale, SpriteEffects.None, 1f);
                }
                b.Draw(Game1.staminaRect, light, Fade(on ? color : color * 0.25f));
            }
        }

        /// <summary>A stable pseudo-random number for a light and a moment, so the blinking looks irregular but doesn't flicker between frames.</summary>
        private static uint Hash(long slot, int light)
        {
            uint h = (uint)slot * 2654435761u ^ (uint)(light + 1) * 40503u;
            h ^= h >> 15;
            h *= 2246822519u;
            return h ^ (h >> 13);
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
                // the English sheet: the Russian one has its own lettering
                Texture2D sheet = Game1.content.Load<Texture2D>("Maps\\MovieTheaterJoja_TileSheet", LocalizedContentManager.LanguageCode.en);
                var pixels = new Color[LogoSource.Width * LogoSource.Height];
                sheet.GetData(0, LogoSource, pixels, 0, pixels.Length);

                // the logo is drawn in pale blue on the sign's darker blue
                int kept = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].R + pixels[i].G + pixels[i].B > 600)
                        kept++;
                    else
                        pixels[i] = Color.Transparent;
                }

                // none or mostly light: a retexture moved the sign, so there's no logo to cut out here
                if (kept == 0 || kept > pixels.Length / 2)
                {
                    this.logoFailed = true;
                    return null;
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

        /// <summary>The banner in Joja's blues, or null to draw the game's own (e.g. a mod retextured it, or it couldn't be read).</summary>
        private Texture2D? GetBanner()
        {
            if (this.banner is { IsDisposed: false })
                return this.banner;
            if (this.bannerFailed)
                return null;

            try
            {
                var pixels = new Color[BannerSource.Width * BannerSource.Height];
                Game1.mouseCursors_1_6.GetData(0, BannerSource, pixels, 0, pixels.Length);
                int recoloured = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (BannerColors.TryGetValue(pixels[i], out Color blue))
                    {
                        pixels[i] = blue;
                        recoloured++;
                    }
                }

                // mostly other colours: a retexture, which is better drawn as it is
                if (recoloured < pixels.Length / 2)
                {
                    this.bannerFailed = true;
                    return null;
                }

                var texture = new Texture2D(Game1.graphics.GraphicsDevice, BannerSource.Width, BannerSource.Height);
                texture.SetData(pixels);
                return this.banner = texture;
            }
            catch (Exception)
            {
                this.bannerFailed = true;
                return null;
            }
        }
    }
}

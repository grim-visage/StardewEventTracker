using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A Joja product, in dark mode: the Joja logo and the title on the blue-metal banner the game uses for Joja's own
    /// text, over a box in Joja's Community Development form colours, darkened: a Joja-blue frame around deep navy,
    /// with the form's light-blue rule under the text. The logo's sunburst sparkles ray by ray, and a row of status
    /// lights on the box blinks like an office computer's (the green one flashes when an event can happen now).
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

        /// <summary>Where the sunburst starts in the logo (left of it is the word "Joja").</summary>
        private const int SunburstLeft = 44;

        /// <summary>Milliseconds for the sparkle to move on to the next ray of the sunburst.</summary>
        private const double SparkleStepMs = 260;

        /// <summary>The status lights: colour, how long each blink lasts, and how often it's on, in percent.</summary>
        private static readonly (Color Color, double PeriodMs, int OnPercent)[] Lights =
        {
            (new Color(137, 182, 255), 2400, 85),   // power: on, with the odd blink
            (new Color(130, 225, 140), 380, 45),    // activity: a busy flicker
            (new Color(255, 185, 80), 1300, 35),
            (new Color(137, 182, 255), 900, 55)
        };

        /// <summary>A status light's size, and the space between lights, in pixels.</summary>
        private const int LightSize = 6, LightGap = 8;

        /// <summary>The logo with the form's white paper made see-through, made from the game's texture on first use.</summary>
        private Texture2D? logo;

        /// <summary>The sunburst's rays, each a list of the logo's pixels, in order around the sun.</summary>
        private List<List<Point>> rays = new();

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
            Texture2D sheet = Game1.mouseCursors_1_6;

            // the game's banner (SpriteText scroll style 3), drawn taller than the game draws it
            b.Draw(sheet, new Vector2(x - 16, top), BannerLeft, Fade(Color.White), 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4, top), BannerMiddle, Fade(Color.White), 0f, Vector2.Zero, new Vector2(inner, BannerScale.Y), SpriteEffects.None, 1f);
            b.Draw(sheet, new Vector2(x - 4 + inner, top), BannerRight, Fade(Color.White), 0f, Vector2.Zero, BannerScale, SpriteEffects.None, 1f);

            // centred in the banner's dark middle (rows 2-14 of 17)
            int logoX = x + BannerMargin;
            int logoY = top + (int)(2 * BannerScale.Y) + ((int)(13 * BannerScale.Y) - LogoSource.Height * LogoScale) / 2;
            if (this.GetLogo() is { } logo)
                b.Draw(logo, new Vector2(logoX, logoY), null, Fade(Color.White), 0f, Vector2.Zero, LogoScale, SpriteEffects.None, 1f);

            int textX = logoX + this.LogoWidth + LogoGap;
            SpriteText.drawString(b, this.Title, textX, top + 22, width: SpriteText.getWidthOfString(this.Title) + 16, alpha: Opacity, color: Color.White);

            double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            this.DrawSparkle(b, logoX, logoY, now);
            DrawLights(b, box, now, state.AnyAvailableNow);
        }

        /// <summary>Brightens the sunburst's rays one after another, with a fading trail, so it seems to turn.</summary>
        private void DrawSparkle(SpriteBatch b, int logoX, int logoY, double now)
        {
            if (this.rays.Count == 0)
                return;

            double lead = now / SparkleStepMs % this.rays.Count;
            for (int i = 0; i < this.rays.Count; i++)
            {
                // how far behind the sparkle this ray is, around the circle
                double behind = (lead - i + this.rays.Count) % this.rays.Count;
                float glow = (float)Math.Max(0, 1 - behind / 2.5);
                if (glow <= 0)
                    continue;
                foreach (Point p in this.rays[i])
                    b.Draw(Game1.staminaRect, new Rectangle(logoX + p.X * LogoScale, logoY + p.Y * LogoScale, LogoScale, LogoScale), Fade(Color.White * (0.85f * glow)));
            }
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
                this.rays = FindRays(pixels);
                return this.logo = texture;
            }
            catch (Exception)
            {
                // draw the banner without it
                this.logoFailed = true;
                return null;
            }
        }

        /// <summary>The sunburst's rays: the separate clusters of pixels right of the word, in order around their centre.</summary>
        private static List<List<Point>> FindRays(Color[] pixels)
        {
            var ink = new HashSet<Point>();
            for (int y = 0; y < LogoSource.Height; y++)
            {
                for (int x = SunburstLeft; x < LogoSource.Width; x++)
                {
                    if (pixels[y * LogoSource.Width + x].A > 0)
                        ink.Add(new Point(x, y));
                }
            }
            if (ink.Count == 0)
                return new List<List<Point>>();

            // pixels touching each other (diagonals too) are one ray
            var rays = new List<List<Point>>();
            var seen = new HashSet<Point>();
            foreach (Point start in ink)
            {
                if (!seen.Add(start))
                    continue;
                var ray = new List<Point>();
                var stack = new Stack<Point>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    Point p = stack.Pop();
                    ray.Add(p);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var next = new Point(p.X + dx, p.Y + dy);
                            if (ink.Contains(next) && seen.Add(next))
                                stack.Push(next);
                        }
                    }
                }
                rays.Add(ray);
            }

            float centerX = (ink.Min(p => p.X) + ink.Max(p => p.X)) / 2f;
            float centerY = (ink.Min(p => p.Y) + ink.Max(p => p.Y)) / 2f;
            return rays
                .OrderBy(ray => Math.Atan2(ray.Average(p => p.Y) - centerY, ray.Average(p => p.X) - centerX))
                .ToList();
        }
    }
}

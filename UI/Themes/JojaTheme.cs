using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewEventTracker.UI.Themes
{
    /// <summary>
    /// A Joja product, in dark mode: the title on the game's metal banner, recoloured in the blues of a Joja Cola can,
    /// over a box in the same colours: a Joja-blue frame around deep Joja navy. Along the bottom, like an office
    /// computer's front panel, a row of status lights blinks (the green one flashes when an event can happen now), a
    /// light-blue rule runs, and the Joja logo sits in the corner, its sunburst sparkling ray by ray.
    /// </summary>
    internal sealed class JojaTheme : HudTheme
    {
        /// <summary>"JOJA" in thin capitals on the back of the Joja furniture's long bench: the bench's colour and the lettering's, nothing else.</summary>
        private static readonly Rectangle WordmarkSource = new(164, 98, 24, 6);

        /// <summary>The sunburst beside the Joja logo on the Community Development form, in the English texture.</summary>
        private static readonly Rectangle SunburstSource = new(63, 13, 14, 16);

        /// <summary>The wordmark is drawn at 3x, the same pixel size as the title; the sunburst at 1x, about as tall as the letters.</summary>
        private const int WordmarkScale = 3;

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

        /// <summary>The banner's scale, the game's 4x.</summary>
        private static readonly Vector2 BannerScale = new(4, 4);

        /// <summary>How far the banner's content starts in from the box's left edge.</summary>
        private const int BannerInset = 36;

        /// <summary>The space between the banner's ends and the title.</summary>
        private const int BannerMargin = 4;

        /// <summary>How far the banner's top sits above the box; it overlaps the box's frame below that.</summary>
        private const int BannerAbove = 49;

        /// <summary>The box's colours: a Joja-blue frame and light-blue rule around a navy a shade darker than the banner's.</summary>
        private static readonly Color Frame = JojaBlue, Paper = new(10, 12, 62), Rule = LightBlue;

        /// <summary>Milliseconds for the sparkle to move on to the next ray of the sunburst.</summary>
        private const double SparkleStepMs = 260;

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

        /// <summary>The space between the wordmark and the sunburst, and between the rule and what's either side of it.</summary>
        private const int LogoGap = 8, RuleGap = 10;

        /// <summary>The space between the box's left and right edges and the lights and logo.</summary>
        private const int FooterInset = 12;

        /// <summary>How far above the box's bottom edge the rule's bottom is; the lights and the logo sit on the same line.</summary>
        private const int FooterBaseline = 10;

        /// <summary>The wordmark and the sunburst side by side on a see-through background, made from the game's textures on first use.</summary>
        private Texture2D? logo;

        /// <summary>The sunburst's rays, each a list of the logo's pixels, in order around the sun.</summary>
        private List<List<Point>> rays = new();

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

        /// <summary>Room below the entries for the logo, which stands taller than the rule.</summary>
        public override int FooterInside => 12;

        public override int MinBoxWidth => Math.Max(BannerInset + this.BannerInnerWidth + 40, FooterInset * 2 + LightsWidth + RuleGap * 2 + 40 + LogoWidth);

        private static int WordmarkWidth => WordmarkSource.Width * WordmarkScale;

        private static int LogoWidth => WordmarkWidth + LogoGap + SunburstSource.Width;

        private static int LogoHeight => Math.Max(WordmarkSource.Height * WordmarkScale, SunburstSource.Height);

        private static int LightsWidth => Lights.Length * (LightSize + LightGap) - LightGap;

        /// <summary>The banner's middle: the title, with a margin each side.</summary>
        private int BannerInnerWidth => BannerMargin + this.TitleWidth + BannerMargin * 2;

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

            // between the lights and the logo, or to the right edge without the logo
            int ruleLeft = box.X + FooterInset + LightsWidth + RuleGap;
            int ruleRight = this.GetLogo() != null ? box.Right - FooterInset - LogoWidth - RuleGap : box.Right - FooterInset;
            b.Draw(pixel, new Rectangle(ruleLeft, box.Bottom - FooterBaseline - 4, ruleRight - ruleLeft, 4), Fade(Rule));
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

            this.DrawTitleText(b, x + BannerMargin, top + 13, Color.White);

            // the logo in the bottom-right corner, standing on the rule's line
            double now = Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? 0;
            if (this.GetLogo() is { } logo)
            {
                int logoX = box.Right - FooterInset - LogoWidth;
                int logoY = box.Bottom - FooterBaseline - LogoHeight;
                b.Draw(logo, new Vector2(logoX, logoY), Fade(Color.White));
                this.DrawSparkle(b, logoX, logoY, now);
            }
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
                    b.Draw(Game1.staminaRect, new Rectangle(logoX + p.X, logoY + p.Y, 1, 1), Fade(Color.White * (0.85f * glow)));
            }
        }

        /// <summary>The row of status lights at the left end of the box's rule, each blinking in its own rhythm.</summary>
        private static void DrawLights(SpriteBatch b, Rectangle box, double now, bool alert)
        {
            int x = box.X + FooterInset;
            int y = box.Bottom - FooterBaseline - 5;
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

        /// <summary>The logo, or null if the textures couldn't be read (e.g. a mod replaced one with a smaller one or moved the lettering).</summary>
        private Texture2D? GetLogo()
        {
            if (this.logo is { IsDisposed: false })
                return this.logo;
            if (this.logoFailed)
                return null;

            try
            {
                Texture2D furniture = Game1.content.Load<Texture2D>("TileSheets\\joja_furniture");
                var word = new Color[WordmarkSource.Width * WordmarkSource.Height];
                furniture.GetData(0, WordmarkSource, word, 0, word.Length);

                // the lettering is whatever isn't the bench's colour, which is the most common
                Color bench = word.GroupBy(c => c).OrderByDescending(g => g.Count()).First().Key;
                int letters = word.Count(c => c != bench);
                if (letters == 0 || letters > word.Length / 2)
                {
                    this.logoFailed = true;
                    return null;
                }

                // always the English form: the Russian one is laid out differently
                Texture2D form = Game1.content.Load<Texture2D>("LooseSprites\\JojaCDForm", LocalizedContentManager.LanguageCode.en);
                var burst = new Color[SunburstSource.Width * SunburstSource.Height];
                form.GetData(0, SunburstSource, burst, 0, burst.Length);
                for (int i = 0; i < burst.Length; i++)
                {
                    if (burst[i].R > 240 && burst[i].G > 240 && burst[i].B > 240)
                        burst[i] = Color.Transparent;
                }

                // the wordmark scaled up in the theme's light blue, then the sunburst centred beside it
                var pixels = new Color[LogoWidth * LogoHeight];
                int wordTop = (LogoHeight - WordmarkSource.Height * WordmarkScale) / 2;
                for (int i = 0; i < word.Length; i++)
                {
                    if (word[i] == bench)
                        continue;
                    int x = i % WordmarkSource.Width * WordmarkScale, y = wordTop + i / WordmarkSource.Width * WordmarkScale;
                    for (int dy = 0; dy < WordmarkScale; dy++)
                    {
                        for (int dx = 0; dx < WordmarkScale; dx++)
                            pixels[(y + dy) * LogoWidth + x + dx] = LightBlue;
                    }
                }
                var burstAt = new Point(WordmarkWidth + LogoGap, (LogoHeight - SunburstSource.Height) / 2);
                for (int i = 0; i < burst.Length; i++)
                    pixels[(burstAt.Y + i / SunburstSource.Width) * LogoWidth + burstAt.X + i % SunburstSource.Width] = burst[i];

                var texture = new Texture2D(Game1.graphics.GraphicsDevice, LogoWidth, LogoHeight);
                texture.SetData(pixels);
                this.rays = FindRays(burst)
                    .Select(ray => ray.Select(p => new Point(p.X + burstAt.X, p.Y + burstAt.Y)).ToList())
                    .ToList();
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

        /// <summary>The sunburst's rays: its separate clusters of pixels, in order around their centre.</summary>
        private static List<List<Point>> FindRays(Color[] pixels)
        {
            var ink = new HashSet<Point>();
            for (int y = 0; y < SunburstSource.Height; y++)
            {
                for (int x = 0; x < SunburstSource.Width; x++)
                {
                    if (pixels[y * SunburstSource.Width + x].A > 0)
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

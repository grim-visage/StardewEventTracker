using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewEventTracker.UI
{
    /// <summary>Text with the word "heart" or "hearts" drawn as the game's heart sprite, e.g. "1 more ♥" or "8-♥ event".</summary>
    internal static class HeartText
    {
        private static readonly Regex HeartWord = new(@"\b[Hh]earts?\b", RegexOptions.Compiled);

        /// <summary>The red heart from the game's Social tab.</summary>
        private static readonly Rectangle HeartSource = new(211, 428, 7, 6);

        private const float Scale = 3f;
        private const int HeartWidth = (int)(7 * Scale);

        /// <summary>The text split into runs of text and hearts.</summary>
        private static IEnumerable<(string Text, bool IsHeart)> Split(string text)
        {
            int at = 0;
            foreach (Match match in HeartWord.Matches(text))
            {
                if (match.Index > at)
                    yield return (text[at..match.Index], false);
                yield return (match.Value, true);
                at = match.Index + match.Length;
            }
            if (at < text.Length)
                yield return (text[at..], false);
        }

        /// <summary>How big the text is once its hearts are sprites.</summary>
        public static Vector2 Measure(SpriteFont font, string text)
        {
            if (!HeartWord.IsMatch(text))
                return font.MeasureString(text);

            float width = 0;
            foreach (string line in text.Split('\n'))
            {
                float lineWidth = 0;
                foreach ((string run, bool isHeart) in Split(line))
                    lineWidth += isHeart ? HeartWidth : font.MeasureString(run).X;
                width = Math.Max(width, lineWidth);
            }
            return new Vector2(width, font.MeasureString(text).Y);
        }

        /// <summary>Draws the text a run at a time with <paramref name="drawText"/>, and its hearts as sprites.</summary>
        /// <param name="alpha">How opaque the hearts are, to fade with the text.</param>
        public static void Draw(SpriteBatch b, SpriteFont font, string text, Vector2 position, float alpha, Action<string, Vector2> drawText)
        {
            if (!HeartWord.IsMatch(text))
            {
                drawText(text, position);
                return;
            }

            // wrapped text (from Game1.parseText) is drawn a line at a time, so each run knows where it starts
            float lineHeight = font.MeasureString("Ag").Y;
            float y = position.Y;
            foreach (string line in text.Split('\n'))
            {
                float x = position.X;
                foreach ((string run, bool isHeart) in Split(line))
                {
                    if (isHeart)
                    {
                        var at = new Vector2(x, y + (lineHeight - HeartSource.Height * Scale) / 2 - 2);
                        b.Draw(Game1.mouseCursors, at, HeartSource, Color.White * alpha, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0.9f);
                        x += HeartWidth;
                    }
                    else
                    {
                        drawText(run, new Vector2(x, y));
                        x += font.MeasureString(run).X;
                    }
                }
                y += font.LineSpacing;
            }
        }
    }
}

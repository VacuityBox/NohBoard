/*
Copyright (C) 2016 by Eric Bataille <e.c.p.bataille@gmail.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace ThoNohT.NohBoard.Extra
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;
    using ThoNohT.NohBoard.Hooking;
    using ThoNohT.NohBoard.Keyboard.ElementDefinitions;
    using ThoNohT.NohBoard.Keyboard.Styles;

    public class SKeys
    {
        #region Rendering

        /// <summary>
        /// SKeys MainForm paint function.
        /// </summary>
        public static void Paint(PaintEventArgs e)
        {
            e.Graphics.Clear(GlobalSettings.CurrentStyle.BackgroundColor);

            if (GlobalSettings.CurrentDefinition == null)
                return;

            // Get all the keys.
            KeyboardState.CheckKeyHolds(GlobalSettings.Settings.PressHold);
            var kbKeys = KeyboardState.PressedKeys;

            MouseState.CheckKeyHolds(GlobalSettings.Settings.PressHold);
            var mouseKeys = MouseState.PressedKeys.Select(k => (int)k).ToList();

            MouseState.CheckScrollAndMovement();
            var scrollCounts = MouseState.ScrollCounts;

            var allDefs = GetAllDefsSorted();

            float maxHeight = 0.0f;

            // Render keyboard and mouse.
            {
                PointF position = new PointF(0, 0);
                foreach (var def in allDefs)
                {
                    if (def is KeyboardKeyDefinition || def is MouseKeyDefinition)
                    {
                        SizeF size = Render(e.Graphics, def, allDefs, kbKeys, mouseKeys, scrollCounts, false, position);
                        position.X += size.Width;

                        if (maxHeight < size.Height)
                            maxHeight = size.Height;
                    }
                }
            }

            // Render scroll.
            {
                if (maxHeight <= 0.0f)
                    maxHeight = FindMaxHeight(e.Graphics, allDefs);

                PointF position = new PointF(0, maxHeight);
                foreach (var def in allDefs.OfType<MouseScrollDefinition>())
                {
                    SizeF size = Render(e.Graphics, def, allDefs, kbKeys, mouseKeys, scrollCounts, false, position);
                    position.X += size.Width;
                }
            }

            // Render mouse speed indicator.
            {
                PointF position = new PointF(0, 0);
                foreach (var def in allDefs.OfType<MouseSpeedIndicatorDefinition>())
                {
                    Render(e.Graphics, def, allDefs, kbKeys, mouseKeys, scrollCounts, false, position);
                }
            }
        }

        /// <summary>
        /// Renders a single element definition.
        /// </summary>
        /// <param name="g">The GDI+ surface to render on.</param>
        /// <param name="def">The element definition to render.</param>
        /// <param name="allDefs">The list of all element definition.</param>
        /// <param name="kbKeys">The list of keyboard keys that are pressed.</param>
        /// <param name="mouseKeys">The list of mouse keys that are pressed.</param>
        /// <param name="scrollCounts">The list of scroll counts.</param>
        /// <param name="alwaysRender">If <c>true</c>, the key will always render, regardless of whether it is
        /// different from the background.</param>
        /// <param name="position">Position at which element will be rendered.</param>
        private static SizeF Render(
            Graphics g,
            ElementDefinition def,
            List<ElementDefinition> allDefs,
            IReadOnlyList<int> kbKeys,
            List<int> mouseKeys,
            IReadOnlyList<int> scrollCounts,
            bool alwaysRender,
            PointF position)
        {
            if (def is KeyboardKeyDefinition kkDef)
            {
                var pressed = true;
                if (!kkDef.KeyCodes.Any() || !kkDef.KeyCodes.All(kbKeys.Contains)) pressed = false;

                if (kkDef.KeyCodes.Count == 1
                    && allDefs.OfType<KeyboardKeyDefinition>()
                        .Any(d => d.KeyCodes.Count > 1
                        && d.KeyCodes.All(kbKeys.Contains)
                        && d.KeyCodes.ContainsAll(kkDef.KeyCodes))) pressed = false;

                if (pressed || alwaysRender)
                    return RenderKeyboardKey(g, pressed, KeyboardState.ShiftDown, KeyboardState.CapsActive, kkDef, position);
            }
            if (def is MouseKeyDefinition mkDef)
            {
                var pressed = mouseKeys.Contains(mkDef.KeyCodes.Single());
                if (pressed || alwaysRender)
                    return RenderMouseKey(g, pressed, KeyboardState.ShiftDown, KeyboardState.CapsActive, mkDef, position);
            }
            if (def is MouseScrollDefinition msDef)
            {
                var scrollCount = scrollCounts[msDef.KeyCodes.Single()];
                if (scrollCount > 0 || alwaysRender) 
                    return RenderMouseScroll(g, scrollCount, msDef, position);
            }
            if (def is MouseSpeedIndicatorDefinition msiDef)
            {
                msiDef.Render(g, MouseState.AverageSpeed);
            }

            return new SizeF(0, 0);
        }

        /// <summary>
        /// Render keyboard key.
        /// </summary>
        private static SizeF RenderKeyboardKey(Graphics g, bool pressed, bool shift, bool capsLock, KeyboardKeyDefinition kkDef, PointF position)
        {
            var style = GlobalSettings.CurrentStyle.TryGetElementStyle<KeyStyle>(kkDef.Id)
                ?? GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var defaultStyle = GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var subStyle = pressed ? style?.Pressed ?? defaultStyle.Pressed : style?.Loose ?? defaultStyle.Loose;

            var txt = kkDef.GetText(shift, capsLock);
            var txtSize = g.MeasureString(txt, subStyle.Font);
            var boundaries = GetBoundaries(position, txtSize);

            // Draw the background
            var backgroundBrush = kkDef.GetBackgroundBrush(subStyle, pressed);
            g.FillPolygon(backgroundBrush, boundaries);

            // Draw the text
            g.DrawString(txt, subStyle.Font, new SolidBrush(subStyle.Text), position);

            // Draw the outline.
            if (subStyle.ShowOutline)
                g.DrawPolygon(new Pen(subStyle.Outline, subStyle.OutlineWidth), boundaries);

            return txtSize;
        }

        /// <summary>
        /// Render mouse key.
        /// </summary>
        private static SizeF RenderMouseKey(Graphics g, bool pressed, bool shift, bool capsLock, MouseKeyDefinition mkDef, PointF position)
        {
            var style = GlobalSettings.CurrentStyle.TryGetElementStyle<KeyStyle>(mkDef.Id)
                            ?? GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var defaultStyle = GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var subStyle = pressed ? style?.Pressed ?? defaultStyle.Pressed : style?.Loose ?? defaultStyle.Loose;
            
            var txtSize = g.MeasureString(mkDef.Text, subStyle.Font);
            var boundaries = GetBoundaries(position, txtSize);

            // Draw the background
            var backgroundBrush = mkDef.GetBackgroundBrush(subStyle, pressed);
            g.FillPolygon(backgroundBrush, boundaries);

            // Draw the text            
            g.DrawString(mkDef.Text, subStyle.Font, new SolidBrush(subStyle.Text), position);

            // Draw the outline.
            if (subStyle.ShowOutline)
                g.DrawPolygon(new Pen(subStyle.Outline, subStyle.OutlineWidth), boundaries);

            return txtSize;
        }

        /// <summary>
        /// Render mouse scroll.
        /// </summary>
        private static SizeF RenderMouseScroll(Graphics g, int scrollCount, MouseScrollDefinition msDef, PointF position)
        {
            var pressed = scrollCount > 0;
            var style = GlobalSettings.CurrentStyle.TryGetElementStyle<KeyStyle>(msDef.Id)
                            ?? GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var defaultStyle = GlobalSettings.CurrentStyle.DefaultKeyStyle;
            var subStyle = pressed ? style?.Pressed ?? defaultStyle.Pressed : style?.Loose ?? defaultStyle.Loose;

            var txt = pressed ? msDef.Text + " " + scrollCount.ToString() : msDef.Text;
            var txtSize = g.MeasureString(txt, subStyle.Font);
            var boundaries = GetBoundaries(position, txtSize);

            // Draw the background
            var backgroundBrush = msDef.GetBackgroundBrush(subStyle, pressed);
            g.FillPolygon(backgroundBrush, boundaries);

            // Draw the text
            g.DrawString(txt, subStyle.Font, new SolidBrush(subStyle.Text), position);

            // Draw the outline.
            if (subStyle.ShowOutline)
                g.DrawPolygon(new Pen(subStyle.Outline, 1), boundaries);

            return txtSize;
        }

        #endregion Rendering

        #region Helpers

        /// <summary>
        /// Get current definition elements sorted by key timestamp.
        /// </summary>
        private static List<ElementDefinition> GetAllDefsSorted()
        {
            var kbKeys = KeyboardState.PressedKeysWithTimestamp;
            var mouseKeys = MouseState.PressedKeysWithTimestamp;

            // Merge keyboard and mouse lists.
            var keysPair = new List<Tuple<int, long>>();
            keysPair.AddRange(kbKeys);
            foreach (var pair in mouseKeys)
                keysPair.Add(new Tuple<int, long>((int)pair.Item1, pair.Item2));

            // Create a list of keys sorted by timestamp.
            var keys = keysPair.OrderBy(x => x.Item2).Select(x => x.Item1).ToList();

            // Sort elements.
            var currentDefs = GlobalSettings.CurrentDefinition.Elements;
            var defs = new List<ElementDefinition>();
            foreach (var key in keys)
            {
                foreach (var def in currentDefs)
                {
                    if (def is KeyboardKeyDefinition kkDef)
                    {
                        if (kkDef.KeyCodes.Contains(key))
                            defs.Add(def);
                    }
                    if (def is MouseKeyDefinition mkDef)
                    {
                        if (mkDef.KeyCodes.Contains(key))
                            defs.Add(def);
                    }
                }
            }

            var rest = currentDefs.Where(x => defs.All(y => y.Id != x.Id)).ToList();
            defs.AddRange(rest);

            return defs;
        }

        /// <summary>
        /// Get the boundaries of element.
        /// </summary>
        private static PointF[] GetBoundaries(PointF pos, SizeF size)
        {
            return new PointF[4] {
                new PointF(pos.X, pos.Y),
                new PointF(pos.X + size.Width, pos.Y),
                new PointF(pos.X + size.Width, pos.Y + size.Height),
                new PointF(pos.X, pos.Y + size.Height)
            };
        }

        /// <summary>
        /// Find max height of keyboard/mouse element definition.
        /// This is used when no element has been rendered 
        /// and SKeys mode needs that information to renders scroll in second row.
        /// </summary>
        private static float FindMaxHeight(Graphics g, List<ElementDefinition> allDefs)
        {
            float maxHeight = 0.0f;

            foreach (var def in allDefs)
            {
                var text = "";
                if (def is KeyboardKeyDefinition kkDef)
                    text = kkDef.GetText(KeyboardState.ShiftDown, KeyboardState.CapsActive);
                else if (def is MouseKeyDefinition mkDef)
                    text = mkDef.Text;
                else
                    continue;

                var pressed = true;
                var style = GlobalSettings.CurrentStyle.TryGetElementStyle<KeyStyle>(def.Id)
                    ?? GlobalSettings.CurrentStyle.DefaultKeyStyle;
                var defaultStyle = GlobalSettings.CurrentStyle.DefaultKeyStyle;
                var subStyle = pressed ? style?.Pressed ?? defaultStyle.Pressed : style?.Loose ?? defaultStyle.Loose;

                var size = g.MeasureString(text, subStyle.Font);

                if (maxHeight < size.Height)
                    maxHeight = size.Height;
            }

            return maxHeight;
        }

        #endregion Helpers
    }
}

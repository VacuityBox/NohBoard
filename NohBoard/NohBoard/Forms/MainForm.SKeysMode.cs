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

namespace ThoNohT.NohBoard.Forms
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;
    using ThoNohT.NohBoard.Extra;
    using ThoNohT.NohBoard.Hooking;

    public partial class MainForm
    {
        /// <summary>
        /// Toggle SKeys mode.
        /// </summary>
        private void mnuToggleSKeysMode_Click(object sender, EventArgs e)
        {
            GlobalSettings.Settings.SKeysMode = this.mnuToggleSKeysMode.Checked;

            if (this.mnuToggleSKeysMode.Checked)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.ClientSize = new Size(GlobalSettings.Settings.SKeysWindowWidth, GlobalSettings.Settings.SKeysWindowHeight);

                // Disabled holding of state keys. So they are not displayed when they are active, but only when pressed.
                KeyboardState.HoldStateKeys = false;
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.ClientSize = new Size(GlobalSettings.CurrentDefinition.Width, GlobalSettings.CurrentDefinition.Height);

                // Restore for normal mode.
                KeyboardState.HoldStateKeys = true;
            }
        }

        /// <summary>
        /// Handles form resize (SKeys specific).
        /// </summary>
        private void MainForm_SKeys_ResizeEnd(object sender, EventArgs e)
        {
            if (this.mnuToggleSKeysMode.Checked)
            {
                GlobalSettings.Settings.SKeysWindowWidth = this.ClientSize.Width;
                GlobalSettings.Settings.SKeysWindowHeight = this.ClientSize.Height;
            }
        }
    }
}

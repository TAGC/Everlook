﻿//
//  EverlookPreferences.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;
using Everlook.Configuration;
using Everlook.Export.Model;
using Everlook.Export.Image;
using Everlook.Export.Audio;

namespace Everlook
{
	public partial class EverlookPreferences : Dialog
	{
		[UI] FileChooserButton GameDirectoryFileChooserButton;
		[UI] FileChooserButton DefaultExportDirectoryFileChooserButton;
		[UI] ComboBox DefaultModelExportFormatComboBox;
		[UI] ComboBox DefaultImageExportFormatComboBox;
		[UI] ComboBox DefaultAudioExportFormatComboBox;
		[UI] CheckButton KeepDirectoryStructureCheckButton;
		[UI] CheckButton SendStatsCheckButton;
		[UI] ColorButton ViewportColourButton;

		private readonly EverlookConfiguration Config = EverlookConfiguration.Instance;

		public static EverlookPreferences Create()
		{
			Builder builder = new Builder(null, "Everlook.interfaces.EverlookPreferences.glade", null);
			return new EverlookPreferences(builder, builder.GetObject("PreferencesDialog").Handle);
		}

		protected EverlookPreferences(Builder builder, IntPtr handle)
			: base(handle)
		{
			builder.Autoconnect(this);

			LoadPreferences();
		}

		private void LoadPreferences()
		{
			if (!String.IsNullOrEmpty(Config.GetGameDirectory()))
			{
				GameDirectoryFileChooserButton.SetFilename(Config.GetGameDirectory());
			}

			ViewportColourButton.Rgba = Config.GetViewportBackgroundColour();

			if (!String.IsNullOrEmpty(Config.GetDefaultExportDirectory()))
			{
				DefaultExportDirectoryFileChooserButton.SetFilename(Config.GetDefaultExportDirectory());
			}

			DefaultModelExportFormatComboBox.Active = (int)Config.GetDefaultModelFormat();
			DefaultImageExportFormatComboBox.Active = (int)Config.GetDefaultImageFormat();
			DefaultAudioExportFormatComboBox.Active = (int)Config.GetDefaultAudioFormat();
			KeepDirectoryStructureCheckButton.Active = Config.GetShouldKeepFileDirectoryStructure();
			SendStatsCheckButton.Active = Config.GetAllowSendAnonymousStats();
		}

		public void SavePreferences()
		{
			Config.SetGameDirectory(GameDirectoryFileChooserButton.Filename);
			Config.SetViewportBackgroundColour(ViewportColourButton.Rgba);
			Config.SetDefaultExportDirectory(DefaultExportDirectoryFileChooserButton.Filename);
			Config.SetDefaultModelFormat((ModelFormat)DefaultModelExportFormatComboBox.Active);
			Config.SetDefaultImageFormat((ImageFormat)DefaultImageExportFormatComboBox.Active);
			Config.SetDefaultAudioFormat((AudioFormat)DefaultAudioExportFormatComboBox.Active);
			Config.SetKeepFileDirectoryStructure(KeepDirectoryStructureCheckButton.Active);
			Config.SetAllowSendAnonymousStats(SendStatsCheckButton.Active);
		}
	}
}

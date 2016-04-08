﻿//
//  EverlookConfiguration.cs
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
using System.IO;
using IniParser;
using IniParser.Model;
using System.Collections.Generic;
using Everlook.Export.Audio;
using Everlook.Export.Image;
using Everlook.Export.Model;
using Gdk;

namespace Everlook.Configuration
{
	public class EverlookConfiguration
	{
		public static EverlookConfiguration Instance = new EverlookConfiguration();

		private readonly object ReadLock = new object();
		private readonly object WriteLock = new object();

		private EverlookConfiguration()
		{
			FileIniDataParser Parser = new FileIniDataParser();

			lock (ReadLock)
			{
				if (!File.Exists(GetConfigurationFilePath()))
				{
					Directory.CreateDirectory(Directory.GetParent(GetConfigurationFilePath()).FullName);
					File.Create(GetConfigurationFilePath()).Close();
					IniData data = Parser.ReadFile(GetConfigurationFilePath());

					data.Sections.AddSection("General");
					data.Sections.AddSection("Export");
					data.Sections.AddSection("Privacy");

					data["General"].AddKey("GameDirectory", "");
					data["General"].AddKey("ViewportBackgroundColour", "rgb(133, 146, 173)");

					data["Export"].AddKey("DefaultExportDirectory", "./Export");

					KeyData ModelExportKeyData = new KeyData("DefaultExportModelFormat");
					ModelExportKeyData.Value = "0";

					List<string> ModelExportKeyComments = new List<string>();
					ModelExportKeyComments.Add("Valid options: ");
					ModelExportKeyComments.Add("0: Collada");
					ModelExportKeyComments.Add("1: Wavefront OBJ");
					ModelExportKeyData.Comments = ModelExportKeyComments;

					data["Export"].AddKey(ModelExportKeyData);

					KeyData ImageExportKeyData = new KeyData("DefaultExportImageFormat");
					ImageExportKeyData.Value = "0";

					List<string> ImageExportKeyComments = new List<string>();
					ImageExportKeyComments.Add("Valid options: ");
					ImageExportKeyComments.Add("0: PNG");
					ImageExportKeyComments.Add("1: JPG");
					ImageExportKeyComments.Add("2: TGA");
					ImageExportKeyComments.Add("3: TIF");
					ImageExportKeyComments.Add("4: BMP");
					ImageExportKeyData.Comments = ImageExportKeyComments;

					data["Export"].AddKey(ImageExportKeyData);

					KeyData AudioExportKeyData = new KeyData("DefaultExportAudioFormat");
					AudioExportKeyData.Value = "0";

					List<string> AudioExportKeyComments = new List<string>();
					AudioExportKeyComments.Add("Valid options: ");
					AudioExportKeyComments.Add("0: WAV");
					AudioExportKeyComments.Add("1: MP3");
					AudioExportKeyComments.Add("2: OGG");
					AudioExportKeyComments.Add("3: FLAC");
					AudioExportKeyData.Comments = AudioExportKeyComments;

					data["Export"].AddKey(AudioExportKeyData);

					data["Export"].AddKey("KeepFileDirectoryStructure", "false");

					data["Privacy"].AddKey("AllowSendAnonymousStats", "true");

					lock (WriteLock)
					{
						WriteConfig(Parser, data);
					}
				}
				else
				{	
					/*
						This section is for updating old configuration files 
						with new sections introduced in updates.

						It's good practice to wrap each updating section in a
						small informational header with the date and change.
					*/

					IniData data = Parser.ReadFile(GetConfigurationFilePath());
				}
			}
		}

		public RGBA GetViewportBackgroundColour()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				RGBA viewportBackgroundColour = new RGBA();
				if (viewportBackgroundColour.Parse(data["General"]["ViewportBackgroundColour"]))
				{
					return viewportBackgroundColour;
				}
				else
				{
					viewportBackgroundColour.Parse("rgb(133, 146, 173)");
					return viewportBackgroundColour;
				}
			}
		}

		public void SetViewportBackgroundColour(RGBA colour)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["General"]["ViewportBackgroundColour"] = colour.ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public string GetGameDirectory()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				return data["General"]["GameDirectory"];
			}
		}

		public void SetGameDirectory(string GameDirectory)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["General"]["GameDirectory"] = GameDirectory;

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public string GetDefaultExportDirectory()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				string path = data["Export"]["DefaultExportDirectory"];
				if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
				{
					path += Path.DirectorySeparatorChar;
				}

				return path;
			}
		}

		public void SetDefaultExportDirectory(string ExportDirectory)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportDirectory"] = ExportDirectory;

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public ModelFormat GetDefaultModelFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				int modelFormat;
				if (int.TryParse(data["Export"]["DefaultExportModelFormat"], out modelFormat))
				{
					return (ModelFormat)modelFormat;
				}
				else
				{
					return ModelFormat.Collada;
				}
			}
		}

		public void SetDefaultModelFormat(ModelFormat modelFormat)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportModelFormat"] = ((int)modelFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public ImageFormat GetDefaultImageFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				int imageFormat;
				if (int.TryParse(data["Export"]["DefaultExportImageFormat"], out imageFormat))
				{
					return (ImageFormat)imageFormat;
				}
				else
				{
					return ImageFormat.PNG;
				}
			}
		}

		public void SetDefaultImageFormat(ImageFormat imageFormat)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportImageFormat"] = ((int)imageFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public AudioFormat GetDefaultAudioFormat()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				int audioFormat;
				if (int.TryParse(data["Export"]["DefaultExportAudioFormat"], out audioFormat))
				{
					return (AudioFormat)audioFormat;
				}
				else
				{
					return AudioFormat.WAV;
				}
			}
		}

		public void SetDefaultAudioFormat(AudioFormat audioFormat)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["DefaultExportAudioFormat"] = ((int)audioFormat).ToString();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public bool GetShouldKeepFileDirectoryStructure()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				bool keepDirectoryStructure;
				if (bool.TryParse(data["Export"]["KeepFileDirectoryStructure"], out keepDirectoryStructure))
				{
					return keepDirectoryStructure;
				}
				else
				{
					return false;
				}
			}
		}

		public void SetKeepFileDirectoryStructure(bool keepStructure)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Export"]["KeepFileDirectoryStructure"] = keepStructure.ToString().ToLower();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		public bool GetAllowSendAnonymousStats()
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				bool allowSendAnonymousStats;
				if (bool.TryParse(data["Privacy"]["AllowSendAnonymousStats"], out allowSendAnonymousStats))
				{
					return allowSendAnonymousStats;
				}
				else
				{
					return true;
				}
			}
		}

		public void SetAllowSendAnonymousStats(bool allowSendAnonymousStats)
		{
			lock (ReadLock)
			{
				FileIniDataParser Parser = new FileIniDataParser();
				IniData data = Parser.ReadFile(GetConfigurationFilePath());

				data["Privacy"]["AllowSendAnonymousStats"] = allowSendAnonymousStats.ToString().ToLower();

				lock (WriteLock)
				{
					WriteConfig(Parser, data);
				}
			}
		}

		/// <summary>
		/// Writes the config data to disk. This method is thread-blocking, and all write operations 
		/// are synchronized via lock(WriteLock).
		/// </summary>
		/// <param name="Parser">The parser dealing with the current data.</param>
		/// <param name="Data">The data which should be written to file.</param>
		private void WriteConfig(FileIniDataParser Parser, IniData Data)
		{
			Parser.WriteFile(GetConfigurationFilePath(), Data);			
		}

		private string GetConfigurationFilePath()
		{
			return String.Format("{0}{1}Everlook{1}everlook.ini", 
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				Path.DirectorySeparatorChar);
		}
	}
}

﻿using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Flow.Launcher.Core.ExternalPlugins.Environments
{
    internal abstract class AbstractPluginEnvironment
    {
        internal abstract string Language { get; }

        internal const string Environments = "Environments";

        internal abstract string EnvName { get; }

        internal abstract string EnvPath { get; }

        internal abstract string InstallPath { get; }

        internal abstract string ExecutablePath { get; }

        internal virtual string FileDialogFilter => string.Empty;

        internal  abstract string PluginsSettingsFilePath { get; set; }

        internal List<PluginMetadata> PluginMetadataList;

        internal PluginsSettings PluginSettings;

        internal AbstractPluginEnvironment(List<PluginMetadata> pluginMetadataList, PluginsSettings pluginSettings)
        {
            PluginMetadataList = pluginMetadataList;
            PluginSettings = pluginSettings;
        }
        //TODO: CHECK IF NEED TO RESET PATH AFTER FLOW UPDATE
        internal IEnumerable<PluginPair> Setup()
        {
            if (!PluginMetadataList.Any(o => o.Language.Equals(Language, StringComparison.OrdinalIgnoreCase)))
                return new List<PluginPair>();

            if (!string.IsNullOrEmpty(PluginsSettingsFilePath) && FilesFolders.FileExists(PluginsSettingsFilePath))
            {
                EnsureLatestInstalled(ExecutablePath, PluginsSettingsFilePath, EnvPath);

                return SetPathForPluginPairs(PluginsSettingsFilePath, Language);
            }

            if (MessageBox.Show($"Flow detected you have installed {Language} plugins, which " +
                                $"will require {EnvName} to run. Would you like to download {EnvName}? " +
                                Environment.NewLine + Environment.NewLine +
                                "Click no if it's already installed, " +
                                $"and you will be prompted to select the folder that contains the {EnvName} executable",
                    string.Empty, MessageBoxButtons.YesNo) == DialogResult.No)
            {
                var msg = $"Please select the {EnvName} executable";
                var selectedFile = string.Empty;

                selectedFile = GetFileFromDialog(msg, FileDialogFilter);

                if (!string.IsNullOrEmpty(selectedFile))
                    PluginsSettingsFilePath = selectedFile;

                // Nothing selected because user pressed cancel from the file dialog window
                if (string.IsNullOrEmpty(selectedFile))
                    InstallEnvironment();
            }
            else
            {
                InstallEnvironment();
            }

            if (FilesFolders.FileExists(PluginsSettingsFilePath))
            {
                return SetPathForPluginPairs(PluginsSettingsFilePath, Language);
            }
            else
            {
                MessageBox.Show(
                    $"Unable to set {Language} executable path, please try from Flow's settings (scroll down to the bottom).");
                Log.Error("PluginsLoader",
                    $"Not able to successfully set {EnvName} path, setting's plugin executable path variable is still an empty string.",
                    $"{Language}Environment");

                return new List<PluginPair>();
            }
        }

        internal abstract void InstallEnvironment();

        private void EnsureLatestInstalled(string expectedPath, string currentPath, string installedDirPath)
        {
            if (expectedPath == currentPath)
                return;

            FilesFolders.RemoveFolderIfExists(installedDirPath);

            InstallEnvironment();

        }

        private IEnumerable<PluginPair> SetPathForPluginPairs(string filePath, string languageToSet)
        {
            var pluginPairs = new List<PluginPair>();

            foreach (var metadata in PluginMetadataList)
            {
                if (metadata.Language.Equals(languageToSet, StringComparison.OrdinalIgnoreCase))
                {
                    pluginPairs.Add(new PluginPair
                    {
                        Plugin = new PythonPlugin(filePath),
                        Metadata = metadata
                    });
                }
            }

            return pluginPairs;
        }

        private string GetFileFromDialog(string title, string filter = "")
        {
            var dlg = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                Title = title,
                Filter = filter
            };

            var result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                return dlg.FileName;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}

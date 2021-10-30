﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin;
using Flow.Plugin.WindowsSettings.Classes;
using Flow.Plugin.WindowsSettings.Properties;

namespace Flow.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper class to easier work with results
    /// </summary>
    internal static class ResultHelper
    {
        private static IPublicAPI? _api;

        public static void Init(IPublicAPI api) => _api = api;

        /// <summary>
        /// Return a list with <see cref="Result"/>s, based on the given list.
        /// </summary>
        /// <param name="list">The original result list to convert.</param>
        /// <param name="query">Query for specific result List</param>
        /// <param name="iconPath">The path to the icon of each entry.</param>
        /// <returns>A list with <see cref="Result"/>.</returns>
        internal static List<Result> GetResultList(
            in IEnumerable<WindowsSetting> list,
            Query query,
            string iconPath)
        {
            var resultList = new List<Result>();
            foreach (var entry in list)
            {
                const int highScore = 20;
                const int midScore = 10;

                Result? result;
                Debug.Assert(_api != null, nameof(_api) + " != null");

                var nameMatch = _api.FuzzySearch(query.Search, entry.Name);

                if (nameMatch.IsSearchPrecisionScoreMet())
                {
                    var settingResult = NewSettingResult(nameMatch.Score + highScore);
                    settingResult.TitleHighlightData = nameMatch.MatchData;
                    result = settingResult;
                }
                else
                {
                    var areaMatch = _api.FuzzySearch(query.Search, entry.Area);
                    if (areaMatch.IsSearchPrecisionScoreMet())
                    {
                        var settingResult = NewSettingResult(areaMatch.Score + midScore);
                        settingResult.SubTitleHighlightData = areaMatch.MatchData.Select(x => x + 6).ToList();
                        result = settingResult;
                    }
                    else
                    {
                        result = entry.AltNames?
                            .Select(altName => _api.FuzzySearch(query.Search, altName))
                            .Where(match => match.IsSearchPrecisionScoreMet())
                            .Select(altNameMatch => NewSettingResult(altNameMatch.Score + midScore))
                            .FirstOrDefault();
                    }

                    if (result is null && entry.Keywords is not null)
                    {
                        string[] searchKeywords = query.SearchTerms;

                        if (searchKeywords
                            .All(x => entry
                                .Keywords
                                .SelectMany(x => x)
                                .Contains(x, StringComparer.CurrentCultureIgnoreCase))
                        )
                            result = NewSettingResult(midScore);
                    }
                }

                if (result is null)
                    continue;

                AddOptionalToolTip(entry, result);

                resultList.Add(result);

                Result NewSettingResult(int score) => new()
                {
                    Action = _ => DoOpenSettingsAction(entry),
                    IcoPath = iconPath,
                    SubTitle = $"{Resources.Area} \"{entry.Area}\" {Resources.SubtitlePreposition} {entry.Type}",
                    Title = entry.Name + entry.glyph,
                    ContextData = entry,
                    Score = score
                };

            }


            return resultList;
        }



        /// <summary>
        /// Add a tool-tip to the given <see cref="Result"/>, based o the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain informations for the tool-tip.</param>
        /// <param name="result">The <see cref="Result"/> that need a tool-tip.</param>
        private static void AddOptionalToolTip(WindowsSetting entry, Result result)
        {
            var toolTipText = new StringBuilder();

            toolTipText.AppendLine($"{Resources.Application}: {entry.Type}");
            toolTipText.AppendLine($"{Resources.Area}: {entry.Area}");

            if (entry.AltNames != null && entry.AltNames.Any())
            {
                var altList = entry.AltNames.Aggregate((current, next) => $"{current}, {next}");

                toolTipText.AppendLine($"{Resources.AlternativeName}: {altList}");
            }

            toolTipText.Append($"{Resources.Command}: {entry.Command}");

            if (!string.IsNullOrEmpty(entry.Note))
            {
                toolTipText.AppendLine(string.Empty);
                toolTipText.AppendLine(string.Empty);
                toolTipText.Append($"{Resources.Note}: {entry.Note}");
            }

            result.TitleToolTip = toolTipText.ToString();
            result.SubTitleToolTip = result.TitleToolTip;
        }

        /// <summary>
        /// Open the settings page of the given <see cref="IWindowsSetting"/>.
        /// </summary>
        /// <param name="entry">The <see cref="WindowsSetting"/> that contain the information to open the setting on command level.</param>
        /// <returns><see langword="true"/> if the settings could be opened, otherwise <see langword="false"/>.</returns>
        private static bool DoOpenSettingsAction(WindowsSetting entry)
        {
            ProcessStartInfo processStartInfo;

            var command = entry.Command;
            
            command = Environment.ExpandEnvironmentVariables(command);

            if (command.Contains(' '))
            {
                var commandSplit = command.Split(' ');
                var file = commandSplit.First();
                var arguments = command[file.Length..].TrimStart();

                processStartInfo = new ProcessStartInfo(file, arguments)
                {
                    UseShellExecute = false,
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo(command)
                {
                    UseShellExecute = true,
                };
            }

            try
            {
                Process.Start(processStartInfo);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("can't open settings", exception, typeof(ResultHelper));
                return false;
            }
        }
    }
}

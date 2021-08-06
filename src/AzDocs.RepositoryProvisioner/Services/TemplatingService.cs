using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AzDocs.RepositoryProvisioner.Services
{
    public class TemplatingService
    {
        private const string PLACEHOLDER_PATTERN = @"\[\[(\w+)\]\]";

        /// <summary>
        /// This method will loop through all files recursivly and replace the [[PLACEHOLDER]] placeholders in the filenames with their respective values.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="directoryInfo">Path to replace filenames in</param>
        public static void ReplacePlaceholdersInPathNames(ILogger log, DirectoryInfo directoryInfo, string componentName)
        {
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                if (Regex.Matches(fileInfo.Name, PLACEHOLDER_PATTERN).Count > 0)
                {
                    string newFileName = Regex.Replace(fileInfo.FullName, PLACEHOLDER_PATTERN, match => GetReplacementValue(match, componentName));
                    File.Move(fileInfo.FullName, newFileName);
                    log.LogInformation($"[TEMPLATING ENGINE] Renamed file {fileInfo.FullName} to {newFileName}");
                }
            }

            foreach (var subDirectoryInfo in directoryInfo.GetDirectories())
            {
                // Skip the .git folder
                if (subDirectoryInfo.Name == ".git")
                    continue;

                if (Regex.Matches(subDirectoryInfo.Name, PLACEHOLDER_PATTERN).Count > 0)
                {
                    var replacedSubDirectoryInfo = new DirectoryInfo(Regex.Replace(subDirectoryInfo.FullName, PLACEHOLDER_PATTERN, match => GetReplacementValue(match, componentName)));
                    Directory.Move(subDirectoryInfo.FullName, replacedSubDirectoryInfo.FullName);
                    log.LogInformation($"[TEMPLATING ENGINE] Renamed directory {subDirectoryInfo.FullName} to {replacedSubDirectoryInfo.FullName}");

                    ReplacePlaceholdersInPathNames(log, replacedSubDirectoryInfo, componentName);
                }
                else
                {
                    ReplacePlaceholdersInPathNames(log, subDirectoryInfo, componentName);
                }
            }
        }

        /// <summary>
        /// This method will loop through all files recursivly and replace the [[PLACEHOLDER]] placeholders in the filecontent with their respective values.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="directoryInfo">Path to replace filenames in</param>
        /// <param name="placeholderPattern">Placeholder pattern to search for</param>
        public static void ReplacePlaceholdersInFileContent(ILogger log, DirectoryInfo directoryInfo, string componentName)
        {
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                string originalFileContent = File.ReadAllText(fileInfo.FullName);
                if (Regex.Matches(originalFileContent, PLACEHOLDER_PATTERN).Count > 0)
                {
                    File.WriteAllText(fileInfo.FullName, Regex.Replace(originalFileContent, PLACEHOLDER_PATTERN, match => GetReplacementValue(match, componentName)));
                    log.LogInformation($"[TEMPLATING ENGINE] Replaced placeholders in {fileInfo.FullName}.");
                }
            }

            foreach (var subDirectoryInfo in directoryInfo.GetDirectories())
            {
                // Skip the .git folder
                if (subDirectoryInfo.Name == ".git")
                    continue;

                ReplacePlaceholdersInFileContent(log, subDirectoryInfo, componentName);
            }
        }

        private static Pluralize.NET.Pluralizer _pluralizer = new Pluralize.NET.Pluralizer();
        /// <summary>
        /// Gets the replacement values for placeholders
        /// </summary>
        /// <param name="match">The placeholder match</param>
        /// <param name="componentName">The generic component name which should come from the ProvisionRepositoryRequest</param>
        /// <returns>Replacement value</returns>
        private static string GetReplacementValue(Match match, string componentName)
        {
            string placeholderValue = match.Groups[1].Value;
            if (placeholderValue.StartsWith("GUID_"))
            {
                return GetReplacementGuid(placeholderValue).ToString();
            }

            switch (placeholderValue)
            {
                case "COMPONENTNAME":
                    {
                        return componentName;
                    }
                case "COMPONENTNAME_ToLower":
                    {
                        return componentName.ToLower();
                    }
                case "COMPONENTNAME_Plural":
                    {
                        return _pluralizer.Pluralize(componentName);
                    }
                case "COMPONENTNAME_Singular":
                    {
                        return _pluralizer.Singularize(componentName);
                    }
            }

            return match.Value;
        }

        static Dictionary<string, Guid> guidDictionary = new Dictionary<string, Guid>();
        /// <summary>
        /// Fetches the replacement GUID. Will create a new GUID when its the first time calling this guid. Will return earlier generated GUID's whenever it finds the given placeholder key.
        /// </summary>
        /// <param name="placeholderKey">The GUID placeholder key</param>
        /// <returns></returns>
        private static Guid GetReplacementGuid(string placeholderKey)
        {

            if (!guidDictionary.ContainsKey(placeholderKey))
                guidDictionary.Add(placeholderKey, Guid.NewGuid());

            return guidDictionary[placeholderKey];
        }
    }
}

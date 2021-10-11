using AzDocs.RepositoryProvisioner.Helpers;
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
            if (placeholderValue.StartsWith("PASSWORD_"))
            {
                return GetReplacementPassword(placeholderValue);
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
                case "COMPONENTNAME_Plural_ToLower":
                    {
                        return _pluralizer.Pluralize(componentName).ToLower();
                    }
                case "COMPONENTNAME_Singular_ToLower":
                    {
                        return _pluralizer.Singularize(componentName).ToLower();
                    }
            }

            return match.Value;
        }

        static Dictionary<string, Guid> _guidDictionary = new Dictionary<string, Guid>();
        /// <summary>
        /// Fetches the replacement GUID. Will create a new GUID when its the first time calling this guid. Will return earlier generated GUID's whenever it finds the given placeholder key.
        /// </summary>
        /// <param name="placeholderKey">The GUID placeholder key</param>
        /// <returns></returns>
        private static Guid GetReplacementGuid(string placeholderKey)
        {
            if (!_guidDictionary.ContainsKey(placeholderKey))
                _guidDictionary.Add(placeholderKey, Guid.NewGuid());

            return _guidDictionary[placeholderKey];
        }

        static Dictionary<string, string> _passwordDictionary = new Dictionary<string, string>();
        /// <summary>
        /// Fetches teh replacement password. Will create a new password when its the first time calling this replacement key. Will return earlier generated passwords whenever it finds the given placeholder key.
        /// Format: `PASSWORD_[TotalLength]_[NumberOfNonAlphanumericChars]`; can be appended with _ (underscore) and any text to create a reusable password, eg: `PASSWORD_10_3_pwd1`.
        /// </summary>
        /// <param name="placeholderKey">The password placeholder key</param>
        /// <returns></returns>
        private static string GetReplacementPassword(string placeholderKey)
        {
            if (!_passwordDictionary.ContainsKey(placeholderKey))
            {
                var length = 10;
                var numberOfNonAlphanumericCharacters = 4;
                var keySplit = placeholderKey.Split('_');
                if (keySplit.Length >= 3)
                {
                    if (int.TryParse(keySplit[1], out int newLength) && newLength >= 5 && newLength <= 128)
                        length = newLength;

                    if (int.TryParse(keySplit[2], out int newNumberOfNonAlphanumericCharacters) && newNumberOfNonAlphanumericCharacters <= length && newNumberOfNonAlphanumericCharacters >= 0)
                        numberOfNonAlphanumericCharacters = newNumberOfNonAlphanumericCharacters;
                }

                _passwordDictionary.Add(placeholderKey, PasswordHelper.Generate(length, numberOfNonAlphanumericCharacters));
            }

            return _passwordDictionary[placeholderKey];
        }
    }
}

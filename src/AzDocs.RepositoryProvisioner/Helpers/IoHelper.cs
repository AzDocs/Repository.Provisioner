using System.IO;

namespace AzDocs.RepositoryProvisioner.Helpers
{
    public static class IoHelper
    {
        /// <summary>
        /// Remove the rootDir path from path to create relative paths
        /// </summary>
        /// <param name="path">The path to trim</param>
        /// <param name="rootDir">The rootdir where path should reside in</param>
        /// <returns></returns>
        public static string TrimRootDir(this string path, DirectoryInfo rootDir)
        {
            if (string.IsNullOrWhiteSpace(path) || rootDir == null)
                return null;

            return path.Replace(rootDir.FullName, "").TrimStart('\\');
        }
    }
}

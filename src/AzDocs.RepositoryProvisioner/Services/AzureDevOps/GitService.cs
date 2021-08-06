using AzDocs.RepositoryProvisioner.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzDocs.RepositoryProvisioner.Services.AzureDevOps
{
    public class GitService
    {
        /// <summary>
        /// Commit & Push the made changes to the new repository
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="newRepositoryLocalPath">The local path where the new repository is checked out</param>
        /// <param name="templateRepository">The template GitRepository object</param>
        /// <param name="personalAccessToken">The personal access token which has read/write access to the repo's</param>
        public static void GitCommitAndPush(ILogger log, DirectoryInfo newRepositoryLocalPath, GitRepository templateRepository, string personalAccessToken, string repositoryAuthorName, string repositoryAuthorEmail)
        {
            log.LogInformation($"[GIT CHECKIN] Starting the committing process...");
            using (var repo = new Repository(newRepositoryLocalPath.FullName))
            {
                log.LogInformation($"[GIT CHECKIN] Adding all files to GIT");
                GitAddAllFiles(log, newRepositoryLocalPath, newRepositoryLocalPath, repo);
                repo.Index.Write();
                log.LogInformation($"[GIT CHECKIN] Added all files to GIT");

                log.LogInformation($"[GIT CHECKIN] Creating GIT commit");
                // Create the committer's signature and commit
                Signature author = new Signature(repositoryAuthorName, repositoryAuthorEmail, DateTime.Now);
                Signature committer = author;

                // Commit to the repository
                Commit commit = repo.Commit($"Initial filling from {templateRepository.Name}", author, committer);
                log.LogInformation($"[GIT CHECKIN] Created GIT commit");

                log.LogInformation($"[GIT CHECKIN] Pushing repository to origin/main");
                PushOptions options = new PushOptions();
                options.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "username", Password = personalAccessToken };
                repo.Network.Push(repo.Branches["main"], options);
                log.LogInformation($"[GIT CHECKIN] Pushed repository to origin/main");
            }
            log.LogInformation($"[GIT CHECKIN] Successfully provisioned the new repository.");

            // ================================================ IMPORTING THE pipeline-orchestrator.yml FILE IN YAML PIPELINES ================================================
            log.LogInformation($"[IMPORT PIPELINE] Starting to import Pipeline into Azure DevOps");
        }

        /// <summary>
        /// Clone a repository to the local disk
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="repository">The GitRepository object to clone</param>
        /// <param name="localPath">The local path to clone the repository to</param>
        /// <param name="personalAccessToken">The personal access token which has read access to the repo</param>
        public static void CloneRepository(ILogger log, GitRepository repository, string localPath, string personalAccessToken)
        {
            // Clone the template repository
            log.LogInformation($"[GIT CLONE] Cloning repo {repository.Name}");
            var cloneOptions = new CloneOptions();
            cloneOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = "username", Password = personalAccessToken };
            Repository.Clone(repository.RemoteUrl, localPath, cloneOptions);
            log.LogInformation($"[GIT CLONE] Cloned repo {repository.Name}");
        }


        /// <summary>
        /// Copies all content (except .git folder) to the target repo directory
        /// </summary>
        /// <param name="tempSourceDirectory">Source directory</param>
        /// <param name="targetDirectory">Target directory</param>
        public static void CopyRepositoryContent(ILogger log, DirectoryInfo tempSourceDirectory, DirectoryInfo targetDirectory)
        {
            log.LogInformation($"[COPY TEMPLATE] Copying template files to the new repository folder...");

            // Make sure the target folder exists
            if (!targetDirectory.Exists)
                targetDirectory.Create();

            foreach (var fileInfo in tempSourceDirectory.GetFiles())
            {
                // Copy the file to the new repo
                fileInfo.MoveTo(Path.Combine(targetDirectory.FullName, fileInfo.Name), true);
            }

            foreach (var subDirectoryInfo in tempSourceDirectory.GetDirectories())
            {
                // Skip the .git folder
                if (subDirectoryInfo.Name == ".git")
                    continue;

                // Move the subdirectory to the new path
                subDirectoryInfo.MoveTo(Path.Combine(targetDirectory.FullName, subDirectoryInfo.Name));
            }

            log.LogInformation($"[COPY TEMPLATE] Copied template files to the new repository folder");
        }

        /// <summary>
        /// Prints all files & directories inside a directory (except the .git folder)
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="directoryInfo">The directory to print its contents</param>
        public static void LogDirectoryContents(ILogger log, DirectoryInfo directoryInfo)
        {
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                log.LogInformation($"[FILE] {fileInfo.FullName}");
            }

            foreach (var subDirectoryInfo in directoryInfo.GetDirectories())
            {
                // Skip the .git folder
                if (subDirectoryInfo.Name == ".git")
                    continue;

                log.LogInformation($"[DIRECTORY] {subDirectoryInfo.FullName}");
                LogDirectoryContents(log, subDirectoryInfo);
            }
        }

        /// <summary>
        /// Get both the information about the template repository & new repository
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="provisionRepositoryRequest">The ProvisionRepositoryRequest with all repository request information.</param>
        /// <param name="azureDevOpsBaseUrl">The Base URL for the Azure DevOps instance to use.</param>
        /// <param name="personalAccessToken">The personal access token which has read access to the repo</param>
        /// <returns></returns>
        public async static Task<(GitRepository templateRepository, GitRepository newRepository)> GetTemplateAndNewRepositories(ILogger log, ProvisionRepositoryRequest provisionRepositoryRequest, string azureDevOpsBaseUrl, string personalAccessToken)
        {
            var repositories = await new VssConnection(new Uri(azureDevOpsBaseUrl), new VssBasicCredential(string.Empty, personalAccessToken)).GetClient<GitHttpClient>().GetRepositoriesAsync(provisionRepositoryRequest.AzureDevOpsTeamProjectId);
            var newRepository = repositories.Where(x => x.Name == provisionRepositoryRequest.RepositoryName).SingleOrDefault();
            var templateRepository = repositories.Where(x => x.Name == string.Format("{0}.Templates.{1}", provisionRepositoryRequest.NewRepositoryProjectName, provisionRepositoryRequest.NewRepositoryType)).SingleOrDefault();

            if (templateRepository == null)
            {
                log.LogInformation($"[INITIALIZATION] No matching template repository found. Stopping provisioning.");
                throw new NotFoundException("No template repository found for this repo.");
            }

            log.LogInformation($"[INITIALIZATION] Found template repository: {templateRepository.Name}");
            log.LogInformation($"[INITIALIZATION] Template repository RemoteUrl: {templateRepository.RemoteUrl}");

            if (newRepository == null)
            {
                log.LogError($"[INITIALIZATION] Something went wrong. Can't find newly created repository. Exiting.");
                throw new NullReferenceException("Newly created repository could not be found.");
            }

            log.LogInformation($"[INITIALIZATION] Found new repository: {newRepository.Name}");
            log.LogInformation($"[INITIALIZATION] New repository RemoteUrl: {newRepository.RemoteUrl}");

            return (templateRepository, newRepository);
        }

        /// <summary>
        /// This method stages all the files inside the GIT repository to be able to commit them.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="baseRepositoryLocalPathDirectoryInfo">The base path for the repository files</param>
        /// <param name="repositoryLocalPathDirectoryInfo">The base path for the repository files</param>
        /// <param name="repo">Repository object for the target repository to commit to</param>
        private static void GitAddAllFiles(ILogger log, DirectoryInfo baseRepositoryLocalPathDirectoryInfo, DirectoryInfo repositoryLocalPathDirectoryInfo, Repository repo)
        {
            foreach (var fileInfo in repositoryLocalPathDirectoryInfo.GetFiles())
            {
                // Stage the file
                string relativeFilePath = fileInfo.FullName.Replace(baseRepositoryLocalPathDirectoryInfo.FullName, "").TrimStart('\\');
                log.LogInformation($"[GIT] Adding {relativeFilePath}");
                repo.Index.Add(relativeFilePath);
            }

            foreach (var subDirectoryInfo in repositoryLocalPathDirectoryInfo.GetDirectories())
            {
                // Skip the .git folder
                if (subDirectoryInfo.Name == ".git")
                    continue;

                // Recursivly iterate through the subdirectories
                GitAddAllFiles(log, baseRepositoryLocalPathDirectoryInfo, subDirectoryInfo, repo);
            }
        }
    }
}

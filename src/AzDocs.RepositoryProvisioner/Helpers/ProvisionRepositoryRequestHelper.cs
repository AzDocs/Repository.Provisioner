using AzDocs.RepositoryProvisioner.Models;
using LibGit2Sharp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AzDocs.RepositoryProvisioner.Helpers
{
    public static class ProvisionRepositoryRequestHelper
    {
        /// <summary>
        /// This method parses & verify's the information from the HTTP request. It will return a filled ProvisionRepositoryRequest object.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="request">The HttpRequest from the Function</param>
        /// <returns></returns>
        public static async Task<ProvisionRepositoryRequest> ParseRequest(ILogger log, HttpRequest request)
        {
            log.LogInformation("[INITIALIZATION] ProvisionRepository request received. Processing...");

            dynamic data = JsonConvert.DeserializeObject(await new StreamReader(request.Body).ReadToEndAsync());
            var provisionRepositoryRequest = new ProvisionRepositoryRequest(
                    (string)data?.eventType,
                    (string)data?.resource?.refUpdates?[0]?.oldObjectId,
                    (string)data?.resource?.refUpdates?[0]?.newObjectId,
                    (string)data?.resource?.repository?.name,
                    (string)data?.resource?.repository?.project?.name,
                    (string)data?.resource?.repository?.project?.id,
                    (Guid)data?.resource?.repository?.id
                );

            if (provisionRepositoryRequest.EventType != "git.push")
                throw new ArgumentException("You are sending the wrong event to this function.");

            if (provisionRepositoryRequest.OldCommitId != "0000000000000000000000000000000000000000")
            {
                log.LogInformation($"[INITIALIZATION] Just another commit to existing repository \"{provisionRepositoryRequest.RepositoryName}\". Skipping this one! (Reference Commit ID: {provisionRepositoryRequest.NewCommitId}).");
                throw new NotFoundException("Nothing to do. Just another commit to an existing repository.");
            }

            log.LogInformation($"[INITIALIZATION] New repository \"{provisionRepositoryRequest.RepositoryName}\" found.");

            (provisionRepositoryRequest.NewRepositoryProjectName, provisionRepositoryRequest.NewRepositoryType, provisionRepositoryRequest.NewRepositoryComponentName) = ParseRepositoryName(provisionRepositoryRequest.RepositoryName);

            return provisionRepositoryRequest;
        }

        /// <summary>
        /// Parses the Repository name and returns their respective values
        /// </summary>
        /// <param name="repositoryName">The repositoryname</param>
        /// <returns></returns>
        private static (string projectName, string repositoryType, string componentName) ParseRepositoryName(string repositoryName)
        {
            string[] splittedRepositoryName = repositoryName.Split('.');
            if (splittedRepositoryName.Length != 3)
            {
                throw new ArgumentException("Repository has to consist out of 3 elements separated by dots; <ProjectName>.<RepositoryType>.<ComponentName>");
            }

            return (splittedRepositoryName[0], splittedRepositoryName[1], splittedRepositoryName[2]);
        }
    }
}

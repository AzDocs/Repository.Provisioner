using AzDocs.RepositoryProvisioner.Helpers;
using AzDocs.RepositoryProvisioner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AzDocs.RepositoryProvisioner.Services.AzureDevOps
{
    public class PipelineService
    {
        /// <summary>
        /// Creates the pipeline in the Azure DevOps Pipeline component. It will import the pipeline-orchestrator.yml from the given repository and enable it in the pipelines module.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="azureDevOpsBaseUrl">The base url for the Azure DevOps instance</param>
        /// <param name="provisionRepositoryRequest">The ProvisionRepositoryRequest</param>
        /// <param name="newRepository">The GitRepository object for the new repository</param>
        /// <param name="repoRootDir">The directory where the repository is located.</param>
        /// <param name="personalAccessToken">The personal access token which gives access to the Azure DevOps instance</param>
        /// <param name="buildQueueName">The default build queue name (eg. 'Hosted Ubuntu 1604')</param>
        /// <param name="pipelineYamlFilePath">The yaml file to create the pipeline(s) for</param>
        /// <param name="rootDirBuildQueueNamePostfix">Optional pipeline name postfix for the root dir yaml file</param>
        public static void CreatePipelines(ILogger log, string azureDevOpsBaseUrl, ProvisionRepositoryRequest provisionRepositoryRequest, GitRepository newRepository, DirectoryInfo repoRootDir, string personalAccessToken, string buildQueueName, string pipelineYamlFilePath, string rootDirBuildQueueNamePostfix = null)
        {
            var pipelineYamlFilePaths = FindAllRelevantPipelineYamls(repoRootDir, pipelineYamlFilePath);
            foreach (var yamlFilePath in pipelineYamlFilePaths)
            {
                var pipelineName = provisionRepositoryRequest.RepositoryName;
                if (!string.IsNullOrWhiteSpace(yamlFilePath.ParentDirectoryName)) // If the yaml file is in a subdir, use the foldername as postfix
                    pipelineName += $".{yamlFilePath.ParentDirectoryName.TrimStart('.')}";
                else if (!string.IsNullOrWhiteSpace(rootDirBuildQueueNamePostfix)) // if the yaml file is in the rootDir and a postfix is set, use that
                    pipelineName += $".{rootDirBuildQueueNamePostfix.TrimStart('.')}";

                CreatePipeline(log, azureDevOpsBaseUrl, provisionRepositoryRequest, newRepository, personalAccessToken, buildQueueName, yamlFilePath.Path, pipelineName);
            }
        }

        /// <summary>
        /// Creates the pipeline in the Azure DevOps Pipeline component. It will import the pipeline-orchestrator.yml from the given repository and enable it in the pipelines module.
        /// </summary>
        /// <param name="log">ILogger instance</param>
        /// <param name="azureDevOpsBaseUrl">The base url for the Azure DevOps instance</param>
        /// <param name="provisionRepositoryRequest">The ProvisionRepositoryRequest</param>
        /// <param name="newRepository">The GitRepository object for the new repository</param>
        /// <param name="personalAccessToken">The personal access token which gives access to the Azure DevOps instance</param>
        /// <param name="buildQueueName">The default build queue name (eg. 'Hosted Ubuntu 1604')</param>
        /// <param name="pipelineYamlFilePath">The yaml file to create the pipeline for</param>
        public static void CreatePipeline(ILogger log, string azureDevOpsBaseUrl, ProvisionRepositoryRequest provisionRepositoryRequest, GitRepository newRepository, string personalAccessToken, string buildQueueName, string pipelineYamlFilePath, string pipelineName)
        {
            var restClient = new RestClient(azureDevOpsBaseUrl);

            RestRequest restRequest = new RestRequest($"/{provisionRepositoryRequest.AzureDevOpsTeamProjectName}/_apis/build/definitions", Method.POST);
            log.LogInformation($"[IMPORT PIPELINE] Rest API Request URL: {azureDevOpsBaseUrl}{restRequest.Resource}");

            // Build the JSON payload
            string json = JsonConvert.SerializeObject(new
            {
                project = provisionRepositoryRequest.AzureDevOpsTeamProjectName,
                name = pipelineName,
                //queueStatus = "Enabled",
                queue = new
                {
                    name = buildQueueName,
                },
                triggers = new[] { new {
                    settingsSourceType = 2,
                    triggerType = "continuousIntegration"
                } },
                repository = new
                {
                    url = newRepository.RemoteUrl,
                    defaultBranch = "main",
                    id = provisionRepositoryRequest.RepositoryId.ToString(),
                    type = "TfsGit"
                },
                process = new
                {
                    yamlFilename = pipelineYamlFilePath,
                    type = 2
                },
                path = "\\",
                type = "build"
            });

            // Get meta information ready
            restRequest.Parameters.Clear();
            restRequest.AddParameter("application/json", json, ParameterType.RequestBody);
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{personalAccessToken}"));
            restRequest.AddHeader("Accept", "application/json; api-version=5.1");
            restRequest.AddHeader("Authorization", $"Basic {token}");

            // Do actual request & log result
            log.LogInformation($"[IMPORT PIPELINE] Posting JSON to import pipeline: {json}");
            var response = restClient.Post(restRequest);
            log.LogInformation($"[IMPORT PIPELINE] Statuscode returned from Azure DevOps API; {response.StatusCode}");
            log.LogInformation($"[IMPORT PIPELINE] Content returned from Azure DevOps API; {response.Content}");

            log.LogInformation("Finished provisioning the new repository! Enjoy!");
        }

        /// <summary>
        /// Get all relevant pipeline yamls (ie. all files that match the same filename as pipelineYamlFile)
        /// If pipelineYamlFile contains path chars (eg. ./filename.yml), this will return a single entry containing the pipelineYamlFile as path
        /// </summary>
        /// <param name="rootDir">The root dir to search in (and all subdirs)</param>
        /// <param name="pipelineYamlFile">The filename to search for</param>
        /// <returns></returns>
        public static IEnumerable<(string Path, string ParentDirectoryName)> FindAllRelevantPipelineYamls(DirectoryInfo rootDir, string pipelineYamlFile)
        {
            if (Path.GetFileName(pipelineYamlFile) != pipelineYamlFile)
                return new[] { (pipelineYamlFile, "") };

            var allYamlFiles = rootDir.GetFiles($"{pipelineYamlFile}", SearchOption.AllDirectories);
            var result = allYamlFiles.Select(x => (x.FullName.TrimRootDir(rootDir), x.DirectoryName.TrimRootDir(rootDir)));

            return result;
        }
    }
}

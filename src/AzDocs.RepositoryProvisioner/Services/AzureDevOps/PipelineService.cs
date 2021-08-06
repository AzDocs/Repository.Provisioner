using AzDocs.RepositoryProvisioner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
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
        /// <param name="personalAccessToken">The personal access token which gives access to the Azure DevOps instance</param>
        /// <param name="buildQueueName">The build queue name</param>
        public static void CreatePipeline(ILogger log, string azureDevOpsBaseUrl, ProvisionRepositoryRequest provisionRepositoryRequest, GitRepository newRepository, string personalAccessToken, string buildQueueName, string pipelineYamlFilePath)
        {
            var restClient = new RestClient(azureDevOpsBaseUrl);

            RestRequest restRequest = new RestRequest($"/{provisionRepositoryRequest.AzureDevOpsTeamProjectName}/_apis/build/definitions", Method.POST);
            log.LogInformation($"[IMPORT PIPELINE] Rest API Request URL: {azureDevOpsBaseUrl}{restRequest.Resource}");

            // Build the JSON payload
            string json = JsonConvert.SerializeObject(new
            {
                project = provisionRepositoryRequest.AzureDevOpsTeamProjectName,
                name = provisionRepositoryRequest.RepositoryName, // Use RepositoryName as Build Definition Name
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
    }
}

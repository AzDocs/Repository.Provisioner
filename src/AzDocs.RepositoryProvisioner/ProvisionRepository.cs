using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using LibGit2Sharp;
using AzDocs.RepositoryProvisioner.Services.AzureDevOps;
using AzDocs.RepositoryProvisioner.Services;
using AzDocs.RepositoryProvisioner.Models;
using AzDocs.RepositoryProvisioner.Helpers;

namespace AzDocs.RepositoryProvisioner
{
    public static class ProvisionRepository
    {
        [FunctionName("ProvisionRepository")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            var configurationBuilder = new ConfigurationBuilder()
               .SetBasePath(context.FunctionAppDirectory)
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables()
               .Build();

            try
            {
                // Get Configuration
                var azureDevOpsOrganizationName = configurationBuilder["AzureDevOps.OrganizationName"];
                var personalAccessToken = configurationBuilder["AzureDevOps.PersonalAccessToken"];
                var pipelineYamlFilePath = configurationBuilder["AzureDevOps.Repository.YamlPipelineFilePath"];
                var repositoryAuthorName = configurationBuilder["AzureDevOps.Repository.Author.Name"];
                var repositoryAuthorEmail = configurationBuilder["AzureDevOps.Repository.Author.Email"];
                var pipelineYamlBuildAgentQueue = configurationBuilder["AzureDevOps.Pipeline.BuildAgentQueueName"];
                var pipelineYamlDefaultPipelineNamePostfix = configurationBuilder["AzureDevOps.Pipeline.DefaultPipelineNamePostfix"];

                var azureDevOpsBaseUrl = $"https://dev.azure.com/{azureDevOpsOrganizationName}";

                // Parse & verify the incoming request
                ProvisionRepositoryRequest provisionRepositoryRequest = await ProvisionRepositoryRequestHelper.ParseRequest(log, req);

                // Fetch GIT repos information
                (GitRepository templateRepository, GitRepository newRepository) = await GitService.GetTemplateAndNewRepositories(log, provisionRepositoryRequest, azureDevOpsBaseUrl, personalAccessToken);

                // Clone the template repository
                var templateRepositoryLocalPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                GitService.CloneRepository(log, templateRepository, templateRepositoryLocalPath, personalAccessToken);

                // Clone the newly created repository
                var newRepositoryLocalPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                GitService.CloneRepository(log, newRepository, newRepositoryLocalPath, personalAccessToken);

                // Copying template files to the new repo
                var tempTemplateRepositoryLocalPathDirectoryInfo = new DirectoryInfo(templateRepositoryLocalPath);
                var newRepositoryLocalPathDirectoryInfo = new DirectoryInfo(newRepositoryLocalPath);
                GitService.CopyRepositoryContent(log, tempTemplateRepositoryLocalPathDirectoryInfo, newRepositoryLocalPathDirectoryInfo);

                // Run the template engine - Replace placeholders in file & directory names
                TemplatingService.ReplacePlaceholdersInPathNames(log, newRepositoryLocalPathDirectoryInfo, provisionRepositoryRequest.NewRepositoryComponentName);

                // Run the template engine - Replace placeholders in filecontent
                TemplatingService.ReplacePlaceholdersInFileContent(log, newRepositoryLocalPathDirectoryInfo, provisionRepositoryRequest.NewRepositoryComponentName);

                // Print the final contents (to be able to check in the function logs)
                GitService.LogDirectoryContents(log, newRepositoryLocalPathDirectoryInfo);

                // Start the git commit & push for the new repository                
                GitService.GitCommitAndPush(log, newRepositoryLocalPathDirectoryInfo, templateRepository, personalAccessToken, repositoryAuthorName, repositoryAuthorEmail);

                // Setup the Azure DevOps Pipeline(s) (import all pipeline-orchestrator.yml's)
                PipelineService.CreatePipelines(log, azureDevOpsBaseUrl, provisionRepositoryRequest, newRepository, newRepositoryLocalPathDirectoryInfo, personalAccessToken, pipelineYamlBuildAgentQueue, pipelineYamlFilePath, pipelineYamlDefaultPipelineNamePostfix);

                // Return 200 OK
                return new OkResult();
            }
            catch (NotFoundException)
            {
                // Valid. So return OK.
                // This is used when there is no template repository found for example, which is ok.
                return new OkResult();
            }
            catch (Exception ex)
            {
                // Log the Exception
                log.LogError(ex, ex.Message);

                // We need to return Ok or the Service Hook will stop working from the Azure DevOps side of things.
                return new OkObjectResult(ex.Message);
            }
        }
    }
}

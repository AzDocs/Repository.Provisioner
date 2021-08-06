using System;
using System.Collections.Generic;
using System.Text;

namespace AzDocs.RepositoryProvisioner.Models
{
    /// <summary>
    /// Model for the Provision Repository Request
    /// </summary>
    public class ProvisionRepositoryRequest
    {
        public string EventType { get; set; }
        public string OldCommitId { get; set; }
        public string NewCommitId { get; set; }
        public string RepositoryName { get; set; }
        public string AzureDevOpsTeamProjectName { get; set; }
        public string AzureDevOpsTeamProjectId { get; set; }
        public Guid RepositoryId { get; set; }
        public string NewRepositoryProjectName { get; internal set; }
        public string NewRepositoryType { get; internal set; }
        public string NewRepositoryComponentName { get; internal set; }

        public ProvisionRepositoryRequest(string eventType, string oldCommitId, string newCommitId, string repositoryName, string azureDevOpsTeamProjectName, string azureDevOpsTeamProjectId, Guid repositoryId)
        {
            EventType = eventType;
            OldCommitId = oldCommitId;
            NewCommitId = newCommitId;
            RepositoryName = repositoryName;
            AzureDevOpsTeamProjectName = azureDevOpsTeamProjectName;
            AzureDevOpsTeamProjectId = azureDevOpsTeamProjectId;
            RepositoryId = repositoryId;
        }
    }
}

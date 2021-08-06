# AzDocs Repository.Provisioner
Create template repo's and provision new repo's with these templates using this Azure Function. Including replacement of placeholders &amp; GUID management for project files &amp; automatic devops pipelines.

The function relies heavily on a naming standarization. It expects your repositoryname to consist out of a generic platform/company name, a repository type and the componentname. It should be called something like `MyPlatform.MyRepoType.MyComponent`. The template repository used for this example will be `MyPlatform.Templates.MyRepoType`.
It will then copy the template repo, replace placeholders and commit the output to the newly created repository. In this way, it becomes painless to create new repositories for, for example, your microservices. The pipeline will also be automatically added to the Azure DevOps Pipelines module. This gets you right into developing features!

As with other AzDocs components, this will only work in combination with Azure & Azure DevOps.

# Environment variables needed
| Variable name | Example value | Description |
| ------------- | ------------- | ------------- |
| `AzureDevOps.OrganizationName` | `mydevopsorg` | The organization name of your Azure DevOps instance. |
| `AzureDevOps.PersonalAccessToken` | `xfn2q7kdv9sypb2d3mvqtgdyx7xsba926q5rat2mn62ynj7347vf` | Your Personal Access Token which has read&write access to the repositories you want to use this provisioner for. |
| `AzureDevOps.Repository.YamlPipelineFilePath` | `pipeline-orchestrator.yml` | The path to your pipeline YAML. |
| `AzureDevOps.Repository.Author.Name` | `John Doe` | Your displayname to be used in the GIT commit while provisioning the repositories. |
| `AzureDevOps.Repository.Author.Email` | `johndoe@company.com` | Your e-mailadress to be used in the GIT commit while provisioning the repositories. |
| `AzureDevOps.Pipeline.BuildAgentQueueName` | `Hosted Ubuntu 1604` | The buildqueue name. For now we recommend using `Hosted Ubuntu 1604` as the value. Other values seem to have bugs. You can override the real buildqueue in your YAML pipeline. |

# How to install
1. Deploy this Azure Function
2. Add a `Service Hook` (with the type `Webhook`) in your Azure DevOps TeamProject with a trigger on `Code Pushes` to `Any` repository, branch or member of a group.
3. Create a template repo `Test.Templates.Microservice`
4. Put some files inside the `Test.Templates.Microservice` repo. Make sure to put `[[COMPONENTNAME]]` somewhere in the filenames or in the file contents.
5. Make sure to also put in a YAML Pipeline into the repo (we use `pipeline-orchestrator.yml` for this in the root of the repository).
6. Create a new repo `Test.Microservice.MyFirstMicroservice` (make sure to commit a readme or anything, so that there will be a code push initialized) and wait a few seconds.
7. Refresh the page and you will see the processed files from the `Test.Templates.Microservice` repository in your new `Test.Microservice.MyFirstMicroservice` repository!
8. Go to `Pipelines` --> `All` and find your new pipeline imported!
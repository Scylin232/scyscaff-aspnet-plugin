using CliWrap;
using System.Reflection;
using ScyScaff.Core.Models.Plugins;
using ScyScaff.Docker.Enums;
using ScyScaff.Docker.Models.Plugins;
using ScyScaff.Docker.Models.Builder;

namespace ScyScaffPlugin.AspNet;

public class AspNetFramework : IFrameworkTemplatePlugin, ITemplateGenerationEvents, IDockerCompatible
{
    public string FrameworkName => "aspnet-ddd";
    
    public string[] SupportedAuth { get; } = { "auth0" };
    public string[] SupportedDatabases { get; } = { "postgresql" };
    public string[] SupportedFlags { get; } = { "Metrics", "Logging" };

    public string GetTemplateTreePath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TemplateTree\\");
    
    public async Task OnServiceGenerationEnded(DirectoryInfo serviceDirectory)
    {
        string[] projectsPath =
        {
            "API/API.csproj",
            "Application/Application.csproj",
            "Domain/Domain.csproj",
            "Infrastructure/Infrastructure.csproj",
            "SharedKernel/SharedKernel.csproj"
        };
        
        await Cli.Wrap("dotnet")
            .WithArguments(args => args
                .Add("new")
                .Add("sln")
                .Add("--name")
                .Add(serviceDirectory.Name))
            .WithWorkingDirectory(serviceDirectory.FullName)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        foreach (string projectPath in projectsPath)
            await Cli.Wrap("dotnet")
                .WithArguments(args => args
                    .Add("sln")
                    .Add($"{serviceDirectory.Name}.sln")
                    .Add("add")
                    .Add(projectPath))
                .WithWorkingDirectory(serviceDirectory.FullName)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
    }
    
    public IEnumerable<DockerComposeService> GetComposeServices(string projectName, string serviceName, int serviceIndex)
    {
        List<DockerComposeService> dockerComposeServices = new();

        int databasePort = 5000 + serviceIndex;
        int servicePort = 5001 + serviceIndex;
        
        DockerComposeService databaseService = new DockerComposeService
        {
            Type = DockerComposeServiceType.Database,
            Image = "postgres:16.1",
            ContainerName = $"{serviceName.ToLower()}-database",
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "POSTGRES_DB", $"{serviceName}Database" },
                { "POSTGRES_USER", $"{serviceName}Login" },
                { "POSTGRES_PASSWORD", $"{serviceName}Password" },
                { "PGDATA", "/data/postgres" },
                { "PGPORT", databasePort.ToString() }
            },
            Volumes = new Dictionary<string, string>
            {
                { $"{serviceName.ToLower()}-database-volume", "/data/postgres" }
            },
            Ports = new Dictionary<int, int?>
            {
                { databasePort, databasePort }
            },
            ExtraProperties = "healthcheck:\n   test: [ \"CMD-SHELL\", \"pg_isready\" ]\n   interval: 10s\n timeout: 5s\n   retries: 5"
        };
        
        DockerComposeService frameworkService = new DockerComposeService
        {
            Type = DockerComposeServiceType.Framework,
            Build = new ComposeBuild { Context = $"./{projectName}.{serviceName}/API", Dockerfile = "Dockerfile" }, 
            ContainerName = $"{serviceName.ToLower()}-service",
            Dependencies = new Dictionary<string, ComposeDependency>
            {
                { databaseService.ContainerName, new ComposeDependency { Condition = "service_healthy" } }
            },
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "ASPNETCORE_URLS", $"http://+:{servicePort}" },
                { "CONNECTION_STRING_DEVELOPMENT", $"Host={databaseService.ContainerName};Database;Port={databasePort};Database={serviceName}Database;Username={serviceName}Login;Password={serviceName}Password;IncludeErrorDetail=true;" }
            },
            Ports = new Dictionary<int, int?>
            {
                { servicePort, servicePort }
            }
        };
        
        dockerComposeServices.Add(databaseService);
        dockerComposeServices.Add(frameworkService);
        
        return dockerComposeServices;
    }
    
    // We don't need that:
    public Task OnServiceGenerationStarted(DirectoryInfo serviceDirectory) => Task.CompletedTask;
}
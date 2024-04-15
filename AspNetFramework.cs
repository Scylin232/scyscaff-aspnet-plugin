using System.IO.Abstractions;
using CliWrap;
using System.Reflection;
using ScyScaff.Core.Models.Events;
using ScyScaff.Core.Models.Parser;
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
    public Dictionary<string, string[]> SupportedFlags { get; } = new()
    {
        { "Logging", new[] { "elk" } },
        { "Metrics", new[] { "prometheus" } }
    };

    public string GetTemplateTreePath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TemplateTree\\");
    
    public async Task OnServiceGenerationEnded(IDirectoryInfo serviceDirectory)
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
            .ExecuteAsync();

        foreach (string projectPath in projectsPath)
        {
            await Cli.Wrap("dotnet")
                .WithArguments(args => args
                    .Add("sln")
                    .Add($"{serviceDirectory.Name}.sln")
                    .Add("add")
                    .Add(projectPath))
                .WithWorkingDirectory(serviceDirectory.FullName)
                .ExecuteAsync();
        }
    }
    
    public IEnumerable<DockerComposeService> GetComposeServices(string projectName, ScaffolderService? service, string serviceName, int serviceIndex)
    {
        List<DockerComposeService> dockerComposeServices = new();

        if (service is null) return dockerComposeServices;
        
        // Service index * Number of Docker services to add.
        int portOffset = serviceIndex * 2;
        
        int servicePort = 5000 + portOffset;
        int databasePort = 5001 + portOffset;

        DockerComposeService? databaseService = null;
        
        if (service.Database == "postgresql")
            databaseService = new DockerComposeService
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
                ExtraProperties = "healthcheck:\n   test: [ \"CMD-SHELL\", \"pg_isready\" ]\n   interval: 10s\n   timeout: 5s\n   retries: 5"
            };

        if (databaseService is null) return dockerComposeServices;
        
        DockerComposeService frameworkService = new DockerComposeService
        {
            Type = DockerComposeServiceType.Framework,
            Build = new ComposeBuild { Context = $"./{projectName}.{serviceName}/", Dockerfile = "Dockerfile" }, 
            ContainerName = $"{serviceName.ToLower()}-service",
            Dependencies = new Dictionary<string, ComposeDependency>
            {
                { databaseService.ContainerName!, new ComposeDependency { Condition = "service_healthy" } }
            },
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "ASPNETCORE_URLS", $"http://+:{servicePort}" },
                { "CONNECTION_STRING_DEVELOPMENT", $"Host={databaseService.ContainerName};Port={databasePort};Database={serviceName}Database;Username={serviceName}Login;Password={serviceName}Password;IncludeErrorDetail=true;" }
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
    public Task OnServiceGenerationStarted(IDirectoryInfo serviceDirectory) => Task.CompletedTask;
}
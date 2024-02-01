using CliWrap;
using System.Reflection;
using ScyScaff.Core.Models.Plugins;
using ScyScaff.Docker.Models.Plugins;
using ScyScaff.Docker.Models.Builder;

namespace ScyScaffPlugin.AspNet;

public class AspNetFramework : IFrameworkTemplatePlugin, ITemplateGenerationEvents, IDockerCompatible
{
    public string FrameworkName => "aspnet-ddd";
    
    public string[] SupportedAuth { get; } = { "auth0" };
    public string[] SupportedDatabases { get; } = { "postgresql" };

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
        
        DockerComposeService databaseService = new DockerComposeService
        {
            Image = "postgres:16.1",
            ContainerName = $"{serviceName.ToLower()}-database",
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "POSTGRES_DB", $"{serviceName}Database" },
                { "POSTGRES_USER", $"{serviceName}Login" },
                { "POSTGRES_PASSWORD", $"{serviceName}Password" },
                { "PGDATA", "/data/postgres" },
                { "PGPORT", (5000 + serviceIndex).ToString() }
            },
            Volumes = new Dictionary<string, string>
            {
                { $"{serviceName.ToLower()}-database-volume", "/data/postgres" }
            },
            Ports = new Dictionary<int, int?>
            {
                { 5000 + serviceIndex, 5000 + serviceIndex }
            },
            ExtraProperties = "healthcheck:\n   test: [ \"CMD-SHELL\", \"pg_isready\" ]\n   interval: 10s\n timeout: 5s\n   retries: 5"
        };
        
        DockerComposeService frameworkService = new DockerComposeService
        {
            Build = new ComposeBuild { Context = $"./{projectName}.{serviceName}/API", Dockerfile = "Dockerfile" }, 
            ContainerName = $"{serviceName.ToLower()}-service",
            Dependencies = new Dictionary<string, ComposeDependency>
            {
                { databaseService.ContainerName, new ComposeDependency { Condition = "service_healthy" } }
            },
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "ASPNETCORE_ENVIRONMENT", "Development" },
                { "ASPNETCORE_URLS", $"http://+:{5001 + serviceIndex}" },
                { "CONNECTION_STRING_DEVELOPMENT", $"Host={databaseService.ContainerName};Database;Port=5000;Database={serviceName}Database;Username={serviceName}Login;Password={serviceName}Password;IncludeErrorDetail=true;" }
            },
            Ports = new Dictionary<int, int?>
            {
                { 5001 + serviceIndex, 5001 + serviceIndex }
            }
        };
        
        dockerComposeServices.Add(databaseService);
        dockerComposeServices.Add(frameworkService);
        
        return dockerComposeServices;
    }
    
    // We don't need that:
    public Task OnServiceGenerationStarted(DirectoryInfo serviceDirectory) => Task.CompletedTask;
}
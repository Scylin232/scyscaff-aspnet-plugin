using CliWrap;
using System.Reflection;
using ScyScaff.Core.Models.Plugins;

namespace ScyScaffPlugin.AspNet;

public class AspNetFramework : IFrameworkPlugin, IServiceGenerationEvents
{
    public string FrameworkName => "aspnet";
    
    public string[] SupportedAuth { get; } = { "keycloak" };
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
    
    // We don't need that:
    public Task OnServiceGenerationStarted(DirectoryInfo serviceDirectory) => Task.CompletedTask;
}
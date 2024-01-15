using System.Reflection;
using ScyScaff.Core.Models.Plugins;

namespace ScyScaffPlugin.AspNet;

public class AspNetFramework : IFrameworkPlugin
{
    public string FrameworkName => "aspnet";
    
    public string[] SupportedAuth { get; } = { "keycloak" };
    public string[] SupportedDatabases { get; } = { "postgresql" };

    public string GetTemplateTreePath() =>
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TemplateTree\\");
}
using System.Reflection;
using System.Runtime.CompilerServices;
using Relay.Conversations;
using Relay.Delivery;
using Relay.Identity;
using Relay.Presence;
using Xunit;

namespace Relay.Architecture.Tests;

/// <summary>Modules talk only through their public Contracts, wired by the API. These tests fail if that decays.</summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly[] Modules =
    [
        typeof(IdentityModule).Assembly,
        typeof(ConversationsModule).Assembly,
        typeof(DeliveryModule).Assembly,
        typeof(PresenceModule).Assembly,
    ];

    private static string NameOf(Assembly a) => a.GetName().Name!;

    private static string ModuleOf(Assembly a) => NameOf(a)["Relay.".Length..];

    [Fact]
    public void ThereAreFourModules()
    {
        Assert.Equal(new[] { "Conversations", "Delivery", "Identity", "Presence" }, Modules.Select(ModuleOf).Order().ToArray());
    }

    [Fact]
    public void NoModuleAssemblyReferencesAnotherModule()
    {
        var moduleNames = Modules.Select(NameOf).ToHashSet();
        foreach (var module in Modules)
        {
            var bad = module.GetReferencedAssemblies().Select(r => r.Name!).Where(moduleNames.Contains).ToList();
            Assert.True(bad.Count == 0, $"{NameOf(module)} references {string.Join(", ", bad)}");
        }
    }

    [Fact]
    public void NoModuleProjectReferencesAnotherProject()
    {
        foreach (var csproj in Directory.GetFiles(Path.Combine(RepoRoot(), "src", "Modules"), "*.csproj", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("ProjectReference", File.ReadAllText(csproj), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OnlyContractsAndTheModuleEntryArepublic()
    {
        foreach (var module in Modules)
        {
            var name = ModuleOf(module);
            var leaks = module.GetExportedTypes()
                .Where(t => t.Namespace != $"Relay.{name}.Contracts" && t.FullName != $"Relay.{name}.{name}Module")
                .Select(t => t.FullName)
                .ToList();
            Assert.True(leaks.Count == 0, $"Public non-contract types in {name}: {string.Join(", ", leaks)}");
        }
    }

    [Fact]
    public void InternalsAreVisibleOnlyToTheTestProject()
    {
        foreach (var module in Modules)
        {
            var friends = module.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).ToArray();
            Assert.Equal(new[] { "Relay.Tests" }, friends);
        }
    }

    [Fact]
    public void TheApiOnlyUsesModuleContracts()
    {
        var api = typeof(Relay.Api.RelayApp).Assembly;
        var used = api.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Select(f => f.FieldType)
                .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)))
            .Where(t => Modules.Contains(t.Assembly))
            .ToList();

        Assert.All(used, t => Assert.True(t.IsPublic, $"API depends on non-public module type {t.FullName}"));
    }

    private static string RepoRoot([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
}

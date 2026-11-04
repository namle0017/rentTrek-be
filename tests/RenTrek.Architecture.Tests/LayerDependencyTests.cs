using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Mono.Cecil;
using NetArchTest.Rules;
using RenTrek.DAL.Data;

namespace RenTrek.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly _api = typeof(API.Common.ApiResponse<>).Assembly;
    private static readonly Assembly _bll = typeof(BLL.DependencyInjection).Assembly;
    private static readonly Assembly _dal = typeof(AppDbContext).Assembly;

    [Fact]
    public void ApiTypesDoNotDependOnDalExceptGlobalProgramCompositionRoot()
    {
        // Program is in the global namespace. All named API namespaces, including
        // identity/filter/extension helpers and controller method bodies, are checked by IL.
        AssertSuccessful(Types.InAssembly(_api).That().ResideInNamespaceStartingWith("RenTrek.API")
            .ShouldNot().HaveDependencyOn("RenTrek.DAL").GetResult());
        // Inspect the complete metadata table too: lambda/async state-machine
        // references must not disappear when a type-based library skips generated types.
        using var module = ModuleDefinition.ReadModule(_api.Location);
        var violations = module.GetTypeReferences()
            .Where(type => type.Namespace.StartsWith("RenTrek.DAL", StringComparison.Ordinal)
                && type.FullName != "RenTrek.DAL.DependencyInjection")
            .Select(type => type.FullName).Distinct().ToArray();
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void BllDoesNotDependOnPresentationOrDirectDataAccess()
    {
        // EF IQueryable async extensions are explicitly allowed by conventions §6.8.
        AssertSuccessful(Types.InAssembly(_bll).ShouldNot().HaveDependencyOnAny(
            "RenTrek.API", "Microsoft.AspNetCore", "RenTrek.DAL.Data",
            "RenTrek.DAL.Repositories.Implementations", "RenTrek.DAL.UnitOfWorks.UnitOfWork").GetResult());
    }

    [Fact]
    public void DalDoesNotDependOnUpperLayers()
    {
        AssertSuccessful(Types.InAssembly(_dal).ShouldNot().HaveDependencyOnAny(
            "RenTrek.API", "RenTrek.BLL", "Microsoft.AspNetCore").GetResult());
    }

    [Fact]
    public void ProjectReferencesRespectDocumentedGraph()
    {
        var allowed = new Dictionary<string, string[]>
        {
            ["RenTrek.API"] = ["RenTrek.BLL", "RenTrek.DAL"],
            ["RenTrek.BLL"] = ["RenTrek.DAL"],
            ["RenTrek.DAL"] = []
        };
        var projects = Directory.GetFiles(Path.Combine(SourceConventionsTests.RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(allowed.Keys.Order(), projects.Select(Path.GetFileNameWithoutExtension).Order());
        foreach (var project in projects)
        {
            var references = XDocument.Load(project).Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')));
            Assert.Equal(allowed[Path.GetFileNameWithoutExtension(project)].Order(), references.Order());
        }
    }

    [Fact]
    public void ControllersAndServicesInjectInterfacesAcrossLayers()
    {
        var controllers = _api.GetTypes().Where(type => IsInNamespace(type.Namespace, "RenTrek.API.Controllers"));
        var services = _bll.GetTypes().Where(type => IsInNamespace(type.Namespace, "RenTrek.BLL.Services.Implementations"));
        var violations = new List<string>();
        foreach (var type in controllers.Concat(services).Where(type => type.IsClass && !type.IsNested))
        {
            foreach (var parameter in type.GetConstructors().SelectMany(ctor => ctor.GetParameters()))
            {
                var dependency = parameter.ParameterType;
                if ((dependency.Assembly == _bll || dependency.Assembly == _dal) && !dependency.IsInterface)
                    violations.Add($"{type.FullName}: inject interface instead of {dependency.FullName}");
                if (services.Contains(type) && IsInNamespace(dependency.Namespace, "RenTrek.DAL.Repositories.Interfaces"))
                    violations.Add($"{type.FullName}: inject IUnitOfWork instead of repository ({dependency.Name})");
            }
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ServiceContractsAndHttpDtosDoNotExposeEntities()
    {
        var contracts = _bll.GetTypes().Where(type => IsInNamespace(type.Namespace, "RenTrek.BLL.Services.Interfaces"));
        var dtos = _bll.GetTypes().Where(type => type.Namespace?.StartsWith("RenTrek.BLL.DTOs.", StringComparison.Ordinal) == true
            && !type.Namespace.EndsWith(".Internal", StringComparison.Ordinal));
        var violations = contracts.SelectMany(type => type.GetMethods())
            .Where(method => ContainsEntity(method.ReturnType))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name} returns Entity")
            .Concat(contracts.SelectMany(type => type.GetMethods())
                .SelectMany(method => method.GetParameters())
                .Where(parameter => ContainsEntity(parameter.ParameterType))
                .Select(parameter => $"{parameter.Member.DeclaringType?.Name}.{parameter.Member.Name} accepts Entity parameter {parameter.Name}"))
            .Concat(dtos.SelectMany(type => type.GetProperties())
                .Where(property => ContainsEntity(property.PropertyType))
                .Select(property => $"{property.DeclaringType?.Name}.{property.Name} exposes Entity"));
        Assert.True(!violations.Any(), string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void AsyncMethodsUseAsyncSuffixAndNeverAsyncVoid()
    {
        var methods = new[] { _api, _bll, _dal }.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && !method.IsDefined(typeof(CompilerGeneratedAttribute), false));
        var violations = methods.Where(method =>
            ((typeof(Task).IsAssignableFrom(method.ReturnType) || method.ReturnType == typeof(ValueTask)
                || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                && !method.Name.EndsWith("Async", StringComparison.Ordinal))
            || (method.ReturnType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute), false)))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}");
        Assert.True(!violations.Any(), string.Join(Environment.NewLine, violations));
    }

    private static bool ContainsEntity(Type type)
        => (type.Namespace == "RenTrek.DAL.Entities" && !type.IsEnum)
           || (type.HasElementType && ContainsEntity(type.GetElementType()!))
           || type.GetGenericArguments().Any(ContainsEntity);

    private static bool IsInNamespace(string? actual, string expected)
        => actual == expected || actual?.StartsWith(expected + ".", StringComparison.Ordinal) == true;

    private static void AssertSuccessful(TestResult result)
        => Assert.True(result.IsSuccessful, string.Join(Environment.NewLine,
            result.FailingTypeNames ?? []));
}

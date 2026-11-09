using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RenTrek.Architecture.Tests;

public sealed class SourceConventionsTests
{
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RenTrek.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Run architecture tests from a checkout of RenTrek.");
        }
    }

    [Fact]
    public void SourceFilesUseFileScopedNamespaceMatchingDirectoryAndOnePrimaryType()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();
            var relative = Path.GetRelativePath(Path.Combine(RepositoryRoot, "src"), file).Replace('\\', '/');
            if (relative == "RenTrek.API/Program.cs")
                continue;
            var expected = relative[..relative.LastIndexOf('/')].Replace('/', '.');
            var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToArray();
            if (namespaces.Length != 1 || namespaces[0] is not FileScopedNamespaceDeclarationSyntax || namespaces[0].Name.ToString() != expected)
                violations.Add($"{relative}: expected file-scoped namespace {expected}");
            var primaryTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(type => type.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax).ToArray();
            // Partial class được tách theo phần: Type.Part.cs
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (primaryTypes.Length != 1 || (primaryTypes[0].Identifier.ValueText != fileName
                && !(primaryTypes[0].Modifiers.Any(SyntaxKind.PartialKeyword)
                    && fileName.StartsWith(primaryTypes[0].Identifier.ValueText + ".", StringComparison.Ordinal))))
                violations.Add($"{relative}: one primary type with matching file name required");
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ControllersDoNotCatchExceptionsAndRepositoriesDoNotSaveChanges()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles())
        {
            var path = file.Replace('\\', '/');
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            if (path.Contains("/RenTrek.API/Controllers/", StringComparison.Ordinal) && root.DescendantNodes().OfType<CatchClauseSyntax>().Any())
                violations.Add($"{Path.GetRelativePath(RepositoryRoot, file)}: controller must delegate errors to middleware");
            if (path.Contains("/RenTrek.DAL/Repositories/", StringComparison.Ordinal))
            {
                foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var name = call.Expression switch
                    {
                        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        _ => ""
                    };
                    if (name is "SaveChanges" or "SaveChangesAsync")
                        violations.Add($"{Path.GetRelativePath(RepositoryRoot, file)}: SaveChanges belongs to UnitOfWork");
                }
            }
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LayerTypesLiveInDocumentedNamespaces()
    {
        var violations = new List<string>();
        foreach (var assembly in new[] { typeof(API.Common.ApiResponse<>).Assembly, typeof(BLL.DependencyInjection).Assembly, typeof(DAL.DependencyInjection).Assembly })
        {
            foreach (var type in assembly.GetTypes().Where(type => type.IsPublic && !type.IsNested))
            {
                var name = type.Name.Split('`')[0];
                var ns = type.Namespace ?? "";
                string? expected = null;
                if (name.EndsWith("Controller", StringComparison.Ordinal)) expected = "RenTrek.API.Controllers";
                if (name.EndsWith("Validator", StringComparison.Ordinal)) expected = "RenTrek.BLL.Validators.";
                if (name.EndsWith("Profile", StringComparison.Ordinal) && type.BaseType?.FullName == "AutoMapper.Profile") expected = "RenTrek.BLL.Mappings";
                if ((name.EndsWith("Request", StringComparison.Ordinal) || name.EndsWith("Response", StringComparison.Ordinal)) && name != "ApiResponse") expected = "RenTrek.BLL.DTOs.";
                if (name.EndsWith("Repository", StringComparison.Ordinal)) expected = type.IsInterface ? "RenTrek.DAL.Repositories.Interfaces" : "RenTrek.DAL.Repositories.Implementations";
                // 02-ARCHITECTURE: controller nằm phẳng trong Controllers/, không chia folder con.
                if (name.EndsWith("Controller", StringComparison.Ordinal) && ns != "RenTrek.API.Controllers")
                    violations.Add($"{type.FullName}: expected exactly RenTrek.API.Controllers");
                else if (expected is not null && !ns.StartsWith(expected, StringComparison.Ordinal))
                    violations.Add($"{type.FullName}: expected {expected}");
            }
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> SourceFiles()
        => Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Replace('\\', '/').Split('/').Any(part => part is "obj" or "bin"));
}

using System.Runtime.CompilerServices;

namespace RenTrek.BLL.Tests;

/// <summary>
/// Giữ quy ước DTO (01-CONVENTIONS.md): mọi DTO nằm trong DTOs/{Module}/{Requests|Responses|Internal}/ và có hậu tố theo vai trò.
/// </summary>
public sealed class DtoNamingConventionTests
{
    private const string DtoNamespace = "RenTrek.BLL.DTOs.";

    private static readonly Dictionary<string, string[]> SuffixesByRole = new()
    {
        ["Requests"] = ["Request"],
        ["Responses"] = ["Response"],
        ["Internal"] = ["Input", "Result", "Item", "Option", "Kind"]
    };

    [Fact]
    public void Dtos_LiveInRoleFolder_AndUseRoleSuffix()
    {
        var types = DtoTypes().ToList();
        Assert.NotEmpty(types);

        var violations = types
            .Select(Check)
            .Where(message => message is not null)
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<Type> DtoTypes()
        => typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(DtoNamespace, StringComparison.Ordinal) == true)
            .Where(type => !type.IsNested && !type.IsDefined(typeof(CompilerGeneratedAttribute), false));

    private static string? Check(Type type)
    {
        // RenTrek.BLL.DTOs.{Module}.{Role}
        var parts = type.Namespace!.Substring(DtoNamespace.Length).Split('.');
        if (parts.Length != 2 || !SuffixesByRole.TryGetValue(parts[1], out var suffixes))
            return $"{type.FullName}: phải nằm trong DTOs/{{Module}}/{{{string.Join('|', SuffixesByRole.Keys)}}}/";

        var name = type.Name.Split('`')[0];
        return suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal))
            ? null
            : $"{type.FullName}: trong {parts[1]}/ tên phải kết thúc bằng {string.Join(" / ", suffixes)}";
    }
}

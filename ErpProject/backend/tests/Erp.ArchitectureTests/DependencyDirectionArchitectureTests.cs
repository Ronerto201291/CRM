using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Erp.ArchitectureTests;

/// <summary>
/// Guardarraíl reforzado (ADR-0018 #13) tras una regresión real: un commit añadió
/// "Erp.Infrastructure → Modules/*/Application" de nuevo (vía un archivo nuevo que
/// consumía IBillingDbContext/IExpensesDbContext directamente) en el mismo cambio que
/// declaraba el ítem como "Corregido", sin que ningún test lo detectara antes del commit.
///
/// Esta clase comprueba la regla en DOS capas independientes, para que un desarrollador
/// (o un agente) no pueda saltársela con un simple "no reconstruir/no correr tests":
///   1. A nivel de archivo .csproj (ProjectReference declarada) — no depende de que el
///      ensamblado esté cargado ni de que el tipo se use de verdad; basta con abrir el
///      repo en texto plano.
///   2. A nivel de ensamblado cargado (GetReferencedAssemblies) — la comprobación previa
///      que ya existía en ControllerArchitectureTests, ahora ampliada a los tres
///      ensamblados "core" (Erp.Domain, Erp.Application, Erp.Infrastructure), no solo
///      Erp.Infrastructure.
///
/// Regla: ninguno de los tres proyectos "core" puede referenciar (ni en el .csproj ni en
/// el ensamblado compilado) ningún proyecto `Modules/*/Application`. El core puede
/// depender de `Modules/*/Domain` cuando ADR-0001 lo permite explícitamente (p. ej. para
/// `Ignore&lt;T&gt;()` de EF Core), pero nunca de la capa Application de un módulo — esa
/// dependencia solo puede vivir en `Erp.Api` (composition root) o en otro módulo.
/// </summary>
public class DependencyDirectionArchitectureTests
{
    private static readonly string[] CoreCsprojRelativePaths =
    [
        "Erp.Domain/Erp.Domain.csproj",
        "Erp.Application/Erp.Application.csproj",
        "Erp.Infrastructure/Erp.Infrastructure.csproj",
    ];

    private static readonly string[] CoreAssemblyNames =
    [
        "Erp.Domain",
        "Erp.Application",
        "Erp.Infrastructure",
    ];

    [Fact]
    public void CoreCsproj_DoesNotReferenceModuleApplicationProjects()
    {
        var backendDir = FindBackendDirectory();
        var violations = new List<string>();

        foreach (var relativePath in CoreCsprojRelativePaths)
        {
            var csprojPath = Path.Combine(backendDir, relativePath);
            if (!File.Exists(csprojPath))
            {
                violations.Add($"No se encontró {relativePath} en {backendDir} — revisa CoreCsprojRelativePaths si el proyecto se movió.");
                continue;
            }

            var content = File.ReadAllText(csprojPath);
            var matches = Regex.Matches(
                content,
                @"ProjectReference\s+Include=""[^""]*Modules[\\/][^""]*[\\/]Application[\\/][^""]*\.csproj""",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches)
                violations.Add($"{relativePath}: {match.Value}");
        }

        Assert.True(violations.Count == 0,
            "Ningún proyecto core (Erp.Domain/Erp.Application/Erp.Infrastructure) puede tener "
            + "un <ProjectReference> a Modules/*/Application/*.csproj (ADR-0018 #13). "
            + "Si un servicio del core necesita datos de un módulo, define un puerto en "
            + "Erp.Application/Common/Interfaces/ e impleméntalo dentro del módulo "
            + "(Modules/<Módulo>/Infrastructure/Services/), nunca al revés:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void CoreAssemblies_DoNotReferenceModuleApplicationAssemblies()
    {
        var violations = new List<string>();

        foreach (var assemblyName in CoreAssemblyNames)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName);
            if (assembly is null)
                continue; // no cargado en este proceso de test; lo cubre el test de .csproj de arriba.

            var badRefs = assembly.GetReferencedAssemblies()
                .Where(r => r.Name?.Contains(".Modules.", StringComparison.Ordinal) == true
                         && r.Name.EndsWith(".Application", StringComparison.Ordinal))
                .Select(r => $"{assemblyName} → {r.Name}");

            violations.AddRange(badRefs);
        }

        Assert.True(violations.Count == 0,
            "Ningún ensamblado core puede referenciar *.Modules.*.Application (ADR-0018 #13):\n"
            + string.Join("\n", violations));
    }

    private static string FindBackendDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Erp.Infrastructure")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "No se pudo localizar el directorio 'backend/' subiendo desde " + AppContext.BaseDirectory);

        return dir.FullName;
    }
}

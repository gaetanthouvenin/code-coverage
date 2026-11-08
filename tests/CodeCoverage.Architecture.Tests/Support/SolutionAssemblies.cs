// ------------------------------------------------------------------------------------------------
// <copyright file="SolutionAssemblies.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CodeCoverage.Architecture.Tests.Support;

// The production assemblies, loaded by name: a project still empty has no type to name with typeof.
internal static class SolutionAssemblies
{
  #region Fields

  public const string RootNamespace = "CodeCoverage";

  public const string DomainName = RootNamespace + ".Domain";

  public const string ApplicationName = RootNamespace + ".Application";

  public const string InfrastructureName = RootNamespace + ".Infrastructure";

  public const string PostgreSqlName = RootNamespace + ".Infrastructure.PostgreSql";

  public const string SqlServerName = RootNamespace + ".Infrastructure.SqlServer";

  public const string ApiName = RootNamespace + ".Api";

  public const string WebName = RootNamespace + ".Web";

  public const string UploaderName = RootNamespace + ".Uploader";

  #endregion

  #region Properties

  public static Assembly DomainAssembly => Load(DomainName);

  public static Assembly ApplicationAssembly => Load(ApplicationName);

  public static Assembly InfrastructureAssembly => Load(InfrastructureName);

  public static Assembly PostgreSqlAssembly => Load(PostgreSqlName);

  public static Assembly SqlServerAssembly => Load(SqlServerName);

  public static Assembly ApiAssembly => Load(ApiName);

  public static Assembly WebAssembly => Load(WebName);

  public static Assembly UploaderAssembly => Load(UploaderName);

  public static IReadOnlyList<Assembly> Production =>
    [
      DomainAssembly,
      ApplicationAssembly,
      InfrastructureAssembly,
      PostgreSqlAssembly,
      SqlServerAssembly,
      ApiAssembly,
      WebAssembly,
      UploaderAssembly,
    ];

  #endregion

  #region Methods

  // The projects of the solution an assembly was compiled against.
  public static IReadOnlyList<string> SolutionReferences(Assembly assembly) =>
    ReferencesStartingWith(assembly, RootNamespace + ".");

  // The compiled references of an assembly whose name starts with a prefix.
  public static IReadOnlyList<string> ReferencesStartingWith(Assembly assembly, string prefix) =>
    assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

  // The references that are not part of the shared framework (Microsoft.NETCore.App).
  public static IReadOnlyList<string> NonFrameworkReferences(Assembly assembly)
  {
    var runtime = RuntimeEnvironment.GetRuntimeDirectory();

    return assembly.GetReferencedAssemblies()
                   .Select(a => a.Name!)
                   .Where(n => !File.Exists(Path.Combine(runtime, n + ".dll")))
                   .Order(StringComparer.Ordinal)
                   .ToList();
  }

  // The types written by hand: neither the compiler or source-generator ones nor <Module>.
  public static IReadOnlyList<Type> TypesOf(Assembly assembly)
  {
    Type[] types;

    try
    {
      types = assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
      types = [.. exception.Types.OfType<Type>()];
    }

    return types.Where(t => !t.Name.StartsWith('<')
                            && !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                            && !t.IsDefined(typeof(GeneratedCodeAttribute), inherit: false))
                .ToList();
  }

  private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

  #endregion
}

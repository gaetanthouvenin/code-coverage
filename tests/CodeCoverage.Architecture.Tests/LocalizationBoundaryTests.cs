// ------------------------------------------------------------------------------------------------
// <copyright file="LocalizationBoundaryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Architecture.Tests.Support;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// Only the console is translated (ADR-015): the Api, the uploader, the logs and the lower layers stay in English.
public sealed class LocalizationBoundaryTests
{
  #region Fields

  private const string LocalizationPrefix = "Microsoft.Extensions.Localization";

  private static readonly string[] Skipped = ["bin", "obj", "TestResults", "node_modules"];

  #endregion

  #region Methods

  [Fact]
  public void OnlyTheWeb_ShouldReferenceLocalizationAndContainResources()
  {
    var violations = new List<string>();

    foreach (var assembly in Production.Where(static a => a != WebAssembly))
    {
      var name = assembly.GetName().Name!;

      violations.AddRange(ReferencesStartingWith(assembly, LocalizationPrefix).Select(r => $"{name}: compiled reference {r}"));
      violations.AddRange(SourceFiles.PackageReferences(name)
                                     .Where(static p => p.StartsWith(LocalizationPrefix, StringComparison.Ordinal))
                                     .Select(p => $"{name}: PackageReference {p}"));
      violations.AddRange(ResxFiles(name).Select(f => $"{name}: resource file {f}"));
    }

    violations.ShouldBeEmpty("Only the Web translates: no localization and no .resx outside it (ADR-015)");
    ReferencesStartingWith(WebAssembly, LocalizationPrefix).ShouldNotBeEmpty("the Web translates through IStringLocalizer");
    ResxFiles(WebName).ShouldNotBeEmpty("the Web keeps its resources in Resources/*.resx");
  }

  [Fact]
  public void NoProjectOutsideTheWeb_ShouldEmbedSatelliteResources()
  {
    var violations = new List<string>();

    foreach (var assembly in Production.Where(static a => a != WebAssembly))
    {
      var name = assembly.GetName().Name!;

      violations.AddRange(assembly.GetManifestResourceNames()
                                  .Where(static r => r.EndsWith(".resources", StringComparison.Ordinal))
                                  .Select(r => $"{name}: embedded resources {r}"));
      violations.AddRange(SatelliteAssemblies(assembly).Select(s => $"{name}: satellite assembly {s}"));
    }

    violations.ShouldBeEmpty("No project outside the Web carries translated resources (ADR-015)");
  }

  private static List<string> ResxFiles(string project)
  {
    var folder = Path.Combine(SourceFiles.Root, "src", project);

    return Directory.EnumerateFiles(folder, "*.resx", SearchOption.AllDirectories)
                    .Select(SourceFiles.Relative)
                    .Where(static f => !f.Split('/').Any(s => Skipped.Contains(s, StringComparer.OrdinalIgnoreCase)))
                    .Order(StringComparer.Ordinal)
                    .ToList();
  }

  // The satellite assemblies ({culture}/{name}.resources.dll) next to the assembly in the test output.
  private static List<string> SatelliteAssemblies(Assembly assembly)
  {
    var folder = Path.GetDirectoryName(assembly.Location)!;
    var file = assembly.GetName().Name + ".resources.dll";

    return Directory.EnumerateDirectories(folder)
                    .Where(d => File.Exists(Path.Combine(d, file)))
                    .Select(d => Path.GetFileName(d) + "/" + file)
                    .Order(StringComparer.Ordinal)
                    .ToList();
  }

  #endregion
}

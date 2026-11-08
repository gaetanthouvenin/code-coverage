// ------------------------------------------------------------------------------------------------
// <copyright file="LayerDependencyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using NetArchTest.Rules;

using CodeCoverage.Architecture.Tests.Support;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// The dependencies point to the centre (ARCHITECTURE §3.2). Each rule is checked on the .csproj,
// on the compiled references of the assembly and on the types (NetArchTest).
public sealed class LayerDependencyTests
{
  #region Fields

  private const string TestsProject = RootNamespace + ".Architecture.Tests";

  private static readonly string[] Hosts = [ApiName, WebName, UploaderName];

  private static readonly string[] Technologies =
  [
    "Microsoft.AspNetCore",
    "Microsoft.EntityFrameworkCore",
    "Microsoft.Data.SqlClient",
    "Npgsql",
  ];

  #endregion

  #region Methods

  [Fact]
  public void Domain_ShouldNotDependOnAnyProjectOrPackage()
  {
    var violations = new List<string>();

    violations.AddRange(SourceFiles.ProjectReferences(DomainName).Select(p => $"ProjectReference {p}"));
    violations.AddRange(SourceFiles.PackageReferences(DomainName).Select(p => $"PackageReference {p}"));
    violations.AddRange(NonFrameworkReferences(DomainAssembly).Select(r => $"compiled reference {r}"));
    violations.AddRange(TypesDependingOn(DomainAssembly, [ApplicationName, InfrastructureName, .. Hosts]));

    violations.ShouldBeEmpty($"{DomainName} only uses the BCL: no project, no package (ARCHITECTURE §3.2)");
  }

  [Fact]
  public void Application_ShouldOnlyDependOnDomain()
  {
    var violations = new List<string>();

    violations.AddRange(SourceFiles.ProjectReferences(ApplicationName)
                                   .Where(p => p != DomainName)
                                   .Select(p => $"ProjectReference {p}"));
    violations.AddRange(SolutionReferences(ApplicationAssembly)
                        .Where(r => r != DomainName)
                        .Select(r => $"compiled reference {r}"));
    violations.AddRange(TypesDependingOn(ApplicationAssembly, [InfrastructureName, .. Hosts, .. Technologies]));

    violations.ShouldBeEmpty($"{ApplicationName} only depends on the Domain, never on a technology (ARCHITECTURE §3.2)");
  }

  [Fact]
  public void Infrastructure_ShouldNotDependOnHosts()
  {
    var violations = new List<string>();
    var layers = new (string Name, Assembly Assembly, string[] Allowed)[]
    {
      (InfrastructureName, InfrastructureAssembly, [ApplicationName, DomainName]),
      (PostgreSqlName, PostgreSqlAssembly, [InfrastructureName]),
      (SqlServerName, SqlServerAssembly, [InfrastructureName]),
    };

    foreach (var (name, assembly, allowed) in layers)
    {
      violations.AddRange(SourceFiles.ProjectReferences(name)
                                     .Where(p => !allowed.Contains(p))
                                     .Select(p => $"{name}: ProjectReference {p}"));
      violations.AddRange(SolutionReferences(assembly)
                          .Where(r => Hosts.Contains(r))
                          .Select(r => $"{name}: compiled reference {r}"));
      violations.AddRange(TypesDependingOn(assembly, Hosts).Select(v => $"{name}: {v}"));
    }

    violations.ShouldBeEmpty("the Infrastructure never depends on the Api, the Web or the Uploader (ARCHITECTURE §3.2)");
  }

  [Fact]
  public void Uploader_ShouldReferenceNoSolutionProject()
  {
    var violations = new List<string>();

    violations.AddRange(SourceFiles.ProjectReferences(UploaderName).Select(p => $"ProjectReference {p}"));
    violations.AddRange(SolutionReferences(UploaderAssembly).Select(r => $"compiled reference {r}"));

    violations.ShouldBeEmpty($"{UploaderName} is standalone: its only coupling with the server is the HTTP contract");
  }

  [Fact]
  public void MediatorSourceGenerator_ShouldOnlyBeReferencedByApplication()
  {
    // One generated mediator per deployed artifact (ADR-001): a second generator in a host would
    // produce a second mediator next to the Application one.
    var referencing = Production.Select(a => a.GetName().Name!)
                                .Where(p => SourceFiles.PackageReferences(p).Contains("Mediator.SourceGenerator"))
                                .ToList();

    referencing.ShouldBe([ApplicationName], "only the Application references Mediator.SourceGenerator (ADR-001)");
  }

  [Fact]
  public void TypesDependingOn_WithForbiddenNamespace_ShouldNameTheOffendingType()
  {
    // Negative check of the helper every rule above relies on: this very class uses NetArchTest.
    var violations = TypesDependingOn(typeof(LayerDependencyTests).Assembly, ["NetArchTest.Rules"]);

    violations.ShouldContain($"type {typeof(LayerDependencyTests).FullName} depends on NetArchTest.Rules");
  }

  [Fact]
  public void ProjectReferences_OnProjectWithReferences_ShouldReportEachOfThem()
  {
    // Negative check of the .csproj parsing: a wrong path or element name would return nothing
    // and every ProjectReference rule above would pass silently.
    var violations = SourceFiles.ProjectReferences(ApplicationName).Select(p => $"ProjectReference {p}").ToList();
    var production = Production.Select(a => a.GetName().Name!).ToList();

    violations.ShouldBe([$"ProjectReference {DomainName}"]);
    SourceFiles.ProjectReferences(TestsProject, "tests").ShouldBe(production, ignoreOrder: true);
    Should.Throw<ShouldAssertException>(() => violations.ShouldBeEmpty("no project"))
          .Message.ShouldContain($"ProjectReference {DomainName}");
  }

  [Fact]
  public void PackageReferences_OnProjectWithPackages_ShouldReportEachOfThem()
  {
    // This test project names exactly two packages, a stable sample for the parsing.
    var violations = SourceFiles.PackageReferences(TestsProject, "tests").Select(p => $"PackageReference {p}").ToList();

    violations.ShouldBe(["PackageReference Microsoft.CodeAnalysis.CSharp", "PackageReference NetArchTest.Rules"]);
    Should.Throw<ShouldAssertException>(() => violations.ShouldBeEmpty("no package"))
          .Message.ShouldContain("PackageReference NetArchTest.Rules");
  }

  [Fact]
  public void ReferencesStartingWith_OnCompiledReferences_ShouldKeepOnlyThePrefixedOnes()
  {
    // SolutionReferences filters the compiled references on the root namespace through this helper.
    var assembly = typeof(LayerDependencyTests).Assembly;
    var violations = ReferencesStartingWith(assembly, "NetArchTest.").Select(r => $"compiled reference {r}").ToList();

    violations.ShouldBe(["compiled reference NetArchTest.Rules"]);
    SolutionReferences(assembly).ShouldBeEmpty();
    Should.Throw<ShouldAssertException>(() => violations.ShouldBeEmpty("no reference"))
          .Message.ShouldContain("compiled reference NetArchTest.Rules");
  }

  [Fact]
  public void NonFrameworkReferences_OnAssemblyWithPackages_ShouldReportThePackagesOnly()
  {
    var references = NonFrameworkReferences(typeof(LayerDependencyTests).Assembly);
    var violations = references.Select(r => $"compiled reference {r}").ToList();

    references.ShouldContain("NetArchTest.Rules");
    references.ShouldContain("Microsoft.CodeAnalysis.CSharp");
    references.ShouldContain("Shouldly");
    references.ShouldNotContain("System.Runtime");
    references.ShouldNotContain("System.Linq");
    Should.Throw<ShouldAssertException>(() => violations.ShouldBeEmpty("BCL only"))
          .Message.ShouldContain("compiled reference NetArchTest.Rules");
  }

  private static List<string> TypesDependingOn(Assembly assembly, IEnumerable<string> forbidden)
  {
    var violations = new List<string>();

    foreach (var ns in forbidden)
    {
      var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn(ns).GetResult();

      violations.AddRange((result.FailingTypeNames ?? []).Select(t => $"type {t} depends on {ns}"));
    }

    return violations;
  }

  #endregion
}

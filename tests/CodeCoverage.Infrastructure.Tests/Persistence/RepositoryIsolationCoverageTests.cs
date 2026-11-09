// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryIsolationCoverageTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Microsoft.EntityFrameworkCore;

using NetArchTest.Rules;

using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;
using CodeCoverage.Infrastructure.Tests.Support.Isolation;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

// Every class that reaches an organization table through the context must have a test class holding
// two organizations (ADR-013). NetArchTest reads the IL, so a table reached only inside a method body
// counts, and the compiler's state machines are counted for the class that wrote the async method.
// No server needed: only the model and the assemblies are read.
public sealed class RepositoryIsolationCoverageTests
{
  #region Fields

  private const string UnusedConnection = "Server=localhost;Database=unused";

  private const string ProductionScope = "CodeCoverage.Infrastructure";

  private const string TestsScope = "CodeCoverage.Infrastructure.Tests";

  private const string SampleScope = TestsScope + ".Support.Isolation";

  #endregion

  #region Methods

  [Fact]
  public void EveryOwnedRepository_ShouldHaveTwoOrganizationTests()
  {
    using var context = DatabaseFixture.CreateContext(Enum.GetValues<DatabaseProvider>()[0], UnusedConnection);
    var owned = context.Model.GetEntityTypes()
                       .Where(static e => TableClassification.OrganizationOwned.Contains(e.GetTableName()!))
                       .Select(static e => e.ClrType.FullName!)
                       .ToArray();

    owned.ShouldNotBeEmpty("the rule has nothing to read without an organization table");

    Uncovered(typeof(CodeCoverageDbContext).Assembly, ProductionScope, typeof(CodeCoverageDbContext), owned, typeof(TwoOrganizationDatabase))
      .ShouldBeEmpty("a class reading an organization table needs a *Tests class that names it and uses TwoOrganizationDatabase (ADR-013)");
  }

  [Fact]
  public void Uncovered_SampleRepositories_ShouldNameOnlyTheOnesWithoutTwoOrganizationTests()
  {
    var uncovered = Uncovered(
      typeof(RepositoryIsolationCoverageTests).Assembly,
      SampleScope,
      typeof(SampleDbContext),
      [typeof(SampleRecord).FullName!],
      typeof(TwoOrganizationDatabase)
    );

    uncovered.ShouldBe(
      [
        typeof(SingleOrganizationSampleRepository).FullName!,
        typeof(UncoveredSampleRepository).FullName!,
      ],
      ignoreOrder: true
    );
  }

  private static List<string> Uncovered(Assembly production, string scope, Type context, string[] owned, Type fixture)
  {
    var tests = typeof(RepositoryIsolationCoverageTests).Assembly;

    // This class names the fixture and the samples, and proves nothing about them.
    var withFixture = Dependents(tests, TestsScope, [fixture.FullName!]).Where(static c => c != typeof(RepositoryIsolationCoverageTests).FullName).ToList();

    return Dependents(production, scope, owned)
           .Intersect(Dependents(production, scope, [context.FullName!]))
           .Where(t => t != context.FullName)
           .Where(t => !Dependents(tests, TestsScope, Names(production, t))
                        .Intersect(withFixture)
                        .Any(static c => c.EndsWith("Tests", StringComparison.Ordinal)))
           .Order(StringComparer.Ordinal)
           .ToList();
  }

  // A test class may hold the implementation or only its port.
  private static string[] Names(Assembly production, string type) =>
    [
      type,
      .. production.GetType(type)!
                   .GetInterfaces()
                   .Where(static i => i.Namespace?.StartsWith("CodeCoverage.Domain", StringComparison.Ordinal) == true)
                   .Select(static i => i.FullName!),
    ];

  // The top-level classes of the scope that depend on one of the types.
  private static List<string> Dependents(Assembly assembly, string scope, string[] dependencies) =>
    (Types.InAssembly(assembly)
          .That()
          .ResideInNamespaceStartingWith(scope)
          .ShouldNot()
          .HaveDependencyOnAny(dependencies)
          .GetResult()
          .FailingTypeNames ?? [])
    .Select(static n => n.Split('/', '+')[0])
    .Distinct(StringComparer.Ordinal)
    .ToList();

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="ModelSnapshotTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence;

// The model guard: the model of each provider must match the snapshot of its migrations assembly,
// so that a configuration changed without "dotnet ef migrations add" fails the tests. No server
// needed: the comparison is between the model and the snapshot.
public sealed class ModelSnapshotTests
{
  #region Fields

  // Never opened: building the model and reading the snapshot do not reach the server.
  private const string UnusedConnection = "Server=localhost;Database=unused";

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_ForEachProvider_ShouldHaveNoPendingChanges(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);

    context.Database.HasPendingModelChanges()
           .ShouldBeFalse(
             $"the {provider} model changed without a migration: run dotnet ef migrations add for both providers"
           );
  }

  // Proves the guard above can fail: an entity added to the model without a migration is detected.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_WithUnmigratedEntity_ShouldHavePendingChanges(DatabaseProvider provider)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(provider, UnusedConnection)
           .ReplaceService<IModelCustomizer, UnmigratedEntityModelCustomizer>();

    using var context = new CodeCoverageDbContext(options.Options, new OrganizationContext());

    context.Database.HasPendingModelChanges().ShouldBeTrue();
  }

  #endregion
}

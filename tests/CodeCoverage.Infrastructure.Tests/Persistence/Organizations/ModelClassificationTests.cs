// ------------------------------------------------------------------------------------------------
// <copyright file="ModelClassificationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Organizations;

// No server needed: only the model is read.
public sealed class ModelClassificationTests
{
  #region Fields

  private const string UnusedConnection = "Server=localhost;Database=unused";

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_EveryEntity_ShouldBeClassifiedOnce(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);
    IReadOnlySet<string>[] classes =
      [TableClassification.Global, TableClassification.OrganizationOwned, TableClassification.Operational];

    var tables = context.Model.GetEntityTypes().Select(static e => e.GetTableName().ShouldNotBeNull()).ToList();

    foreach (var table in tables)
    {
      classes.Count(c => c.Contains(table)).ShouldBe(1, $"{table} must be in exactly one class");
    }

    classes.SelectMany(static c => c).ShouldBe(tables, ignoreOrder: true);
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_OwnedEntity_ShouldHaveNamedFilter(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);

    foreach (var entity in context.Model.GetEntityTypes())
    {
      var table = entity.GetTableName().ShouldNotBeNull();

      if (TableClassification.OrganizationOwned.Contains(table))
      {
        entity.GetDeclaredQueryFilters().Single().Key.ShouldBe(OrganizationOwnedExtensions.FilterName, table);
        var column = entity.FindProperty(OrganizationOwnedExtensions.ColumnName).ShouldNotBeNull(table);
        column.IsShadowProperty().ShouldBeTrue(table);
        column.IsNullable.ShouldBeFalse(table);
      }
      else
      {
        entity.GetDeclaredQueryFilters().ShouldBeEmpty(table);
      }
    }
  }

  // The acceptance of the micro-feature: the unique index serves the foreign key, and the convention adds no other.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_OrganizationForeignKey_ShouldBeServedByTheUniqueIndex(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, UnusedConnection);
    var withForeignKey = context.Model.GetEntityTypes()
                                .Where(static e => e.GetForeignKeys().Any(static k => k.PrincipalEntityType.ClrType == typeof(Organization)))
                                .ToList();

    withForeignKey.ShouldNotBeEmpty();

    foreach (var entity in withForeignKey)
    {
      var indexes = entity.GetIndexes()
                          .Where(static i => i.Properties[0].Name == OrganizationOwnedExtensions.ColumnName)
                          .ToList();

      indexes.ShouldHaveSingleItem(entity.DisplayName()).IsUnique.ShouldBeTrue();
      indexes[0].Properties.Count.ShouldBeGreaterThan(1);
    }
  }

  #endregion
}

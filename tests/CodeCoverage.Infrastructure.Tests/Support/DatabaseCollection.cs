// ------------------------------------------------------------------------------------------------
// <copyright file="DatabaseCollection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Shares the database containers between the test classes: starting SQL Server takes seconds.
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
  #region Fields

  public const string Name = "Database";

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="ExclusiveDatabaseCollection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// Runs alone, with its own containers: a test that measures the memory of the process must not see the other tests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExclusiveDatabaseCollection : ICollectionFixture<DatabaseFixture>
{
  #region Fields

  public const string Name = "ExclusiveDatabase";

  #endregion
}

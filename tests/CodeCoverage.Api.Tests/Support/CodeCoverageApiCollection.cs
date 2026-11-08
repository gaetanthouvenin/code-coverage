// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageApiCollection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Tests.Support;

// Shares one host between the test classes: the static Serilog logger cannot be frozen by two
// hosts started in parallel.
[CollectionDefinition(Name)]
public sealed class CodeCoverageApiCollection : ICollectionFixture<CodeCoverageApiFactory>
{
  #region Fields

  public const string Name = "CodeCoverageApi";

  #endregion
}

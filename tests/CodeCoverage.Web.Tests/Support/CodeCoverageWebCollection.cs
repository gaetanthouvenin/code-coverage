// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageWebCollection.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Support;

// Shares one host between the test classes: the static Serilog logger cannot be frozen by two
// hosts started in parallel.
[CollectionDefinition(Name)]
public sealed class CodeCoverageWebCollection : ICollectionFixture<CodeCoverageWebFactory>
{
  #region Fields

  public const string Name = "CodeCoverageWeb";

  #endregion
}

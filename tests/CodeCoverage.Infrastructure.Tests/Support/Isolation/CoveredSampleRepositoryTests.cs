// ------------------------------------------------------------------------------------------------
// <copyright file="CoveredSampleRepositoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support.Isolation;

// Holds no test: read by RepositoryIsolationCoverageTests as a class with two organizations.
public static class CoveredSampleRepositoryTests
{
  #region Properties

  public static Type Subject => typeof(CoveredSampleRepository);

  public static Type Fixture => typeof(TwoOrganizationDatabase);

  #endregion
}

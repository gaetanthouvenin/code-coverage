// ------------------------------------------------------------------------------------------------
// <copyright file="ExitCodePolicyTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Tests;

public sealed class ExitCodePolicyTests
{
  #region Methods

  [Theory]
  [InlineData(true, false, 0)]
  [InlineData(true, true, 0)]
  [InlineData(false, false, 0)]
  [InlineData(false, true, 1)]
  public void Resolve_ShouldExitOneOnlyOnFailureWithFailOnError(
    bool succeeded,
    bool failOnError,
    int expected)
  {
    ExitCodePolicy.Resolve(succeeded, failOnError).ShouldBe(expected);
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="DetectedContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;

namespace CodeCoverage.Uploader.Tests.Ci;

// The forge names and aliases the uploader reads, as the server reads the forge field of an upload.
public sealed class DetectedContextTests
{
  #region Methods

  [Theory]
  [InlineData("github", "github")]
  [InlineData("gh", "github")]
  [InlineData("GitHub", "github")]
  [InlineData("bitbucket", "bitbucket")]
  [InlineData("bb", "bitbucket")]
  [InlineData("azure-devops", "azure-devops")]
  [InlineData(" Azure-DevOps ", "azure-devops")]
  [InlineData("gitlab", "gitlab")]
  [InlineData("gl", "gitlab")]
  public void NormalizeForge_KnownNameOrAlias_ShouldGiveTheForgeSegment(string value, string expected)
  {
    DetectedContext.NormalizeForge(value).ShouldBe(expected);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("azuredevops")]
  [InlineData("gitea")]
  public void NormalizeForge_NoForgeNamed_ShouldBeNull(string? value)
  {
    DetectedContext.NormalizeForge(value).ShouldBeNull();
  }

  #endregion
}

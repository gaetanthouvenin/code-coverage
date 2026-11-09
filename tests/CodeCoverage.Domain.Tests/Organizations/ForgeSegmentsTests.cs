// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeSegmentsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class ForgeSegmentsTests
{
  #region Methods

  [Theory]
  [InlineData("github", Forge.GitHub)]
  [InlineData("GH", Forge.GitHub)]
  [InlineData("bitbucket", Forge.Bitbucket)]
  [InlineData("bb", Forge.Bitbucket)]
  [InlineData("azure-devops", Forge.AzureDevOps)]
  [InlineData("gitlab", Forge.GitLab)]
  [InlineData("gl", Forge.GitLab)]
  public void TryParse_KnownSegment_ShouldGiveTheForge(string segment, Forge expected)
  {
    ForgeSegments.TryParse(segment, out var forge).ShouldBeTrue();
    forge.ShouldBe(expected);
  }

  [Theory]
  [InlineData("svn")]
  [InlineData("")]
  [InlineData(null)]
  public void TryParse_UnknownSegment_ShouldFail(string? segment)
  {
    ForgeSegments.TryParse(segment, out _).ShouldBeFalse();
  }

  [Theory]
  [InlineData(Forge.GitHub, "github")]
  [InlineData(Forge.AzureDevOps, "azure-devops")]
  public void ToSegment_ShouldWriteTheSegmentOfAddresses(Forge forge, string expected)
  {
    ForgeSegments.ToSegment(forge).ShouldBe(expected);
  }

  #endregion
}

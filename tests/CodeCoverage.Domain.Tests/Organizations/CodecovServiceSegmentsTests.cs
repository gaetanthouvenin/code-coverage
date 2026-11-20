// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovServiceSegmentsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class CodecovServiceSegmentsTests
{
  #region Methods

  [Fact]
  public void TryParse_Gh_ShouldReturnGitHub()
  {
    CodecovServiceSegments.TryParse("gh", out var forge).ShouldBeTrue();
    forge.ShouldBe(Forge.GitHub);
  }

  [Theory]
  [InlineData("github")]
  [InlineData("GitHub")]
  [InlineData("GH")]
  public void TryParse_GitHubAlias_ShouldReturnGitHub(string segment)
  {
    CodecovServiceSegments.TryParse(segment, out var forge).ShouldBeTrue();
    forge.ShouldBe(Forge.GitHub);
  }

  [Theory]
  [InlineData("azure-devops")]
  [InlineData("svn")]
  [InlineData("")]
  [InlineData(null)]
  public void TryParse_NotACodecovService_ShouldFail(string? segment)
  {
    CodecovServiceSegments.TryParse(segment, out var forge).ShouldBeFalse();
    forge.ShouldBe(default);
  }

  [Fact]
  public void TryGetSegment_GitHub_ShouldWriteGithub()
  {
    CodecovServiceSegments.TryGetSegment(Forge.GitHub, out var segment).ShouldBeTrue();
    segment.ShouldBe("github");
  }

  [Fact]
  public void TryGetSegment_AzureDevOps_ShouldHaveNoRow()
  {
    CodecovServiceSegments.TryGetSegment(Forge.AzureDevOps, out var segment).ShouldBeFalse();
    segment.ShouldBeNull();
  }

  #endregion
}

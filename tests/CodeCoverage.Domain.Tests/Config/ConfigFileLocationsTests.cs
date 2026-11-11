// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigFileLocationsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Tests.Config;

public sealed class ConfigFileLocationsTests
{
  #region Methods

  [Theory]
  [InlineData("codecov.yml")]
  [InlineData(".codecov.yml")]
  [InlineData("codecov.yaml")]
  [InlineData(".codecov.yaml")]
  [InlineData(".github/codecov.yml")]
  [InlineData(".github/.codecov.yaml")]
  [InlineData("dev/codecov.yml")]
  public void IsConfigPath_SearchedLocation_ShouldBeTrue(string path)
  {
    ConfigFileLocations.IsConfigPath(path).ShouldBeTrue();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Codecov.yml")]
  [InlineData("docs/codecov.yml")]
  [InlineData(".github/workflows/codecov.yml")]
  [InlineData("/codecov.yml")]
  [InlineData("codecov.yml/")]
  [InlineData("codecov.yml.bak")]
  [InlineData(".GitHub/codecov.yml")]
  public void IsConfigPath_AnythingElse_ShouldBeFalse(string? path)
  {
    ConfigFileLocations.IsConfigPath(path).ShouldBeFalse();
  }

  [Fact]
  public void Locations_ShouldKeepTheOrderTheyAreTriedIn()
  {
    ConfigFileLocations.FileNames.ShouldBe(["codecov.yml", ".codecov.yml", "codecov.yaml", ".codecov.yaml"]);
    ConfigFileLocations.Directories.ShouldBe([".github", "dev"]);
  }

  #endregion
}

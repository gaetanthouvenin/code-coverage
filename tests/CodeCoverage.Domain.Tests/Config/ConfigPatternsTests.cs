// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigPatternsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

public sealed class ConfigPatternsTests
{
  #region Methods

  [Fact]
  public void ReadBranches_ShouldUseTheBranchRecognition()
  {
    var warnings = new List<ConfigWarning>();

    var patterns = ConfigPatterns.ReadBranches(Texts("main", "feature/*"), "comment.branches", warnings);

    patterns[0].Match("main").ShouldBeTrue();
    patterns[0].Match("main2").ShouldBeFalse();
    patterns[1].Match("feature/a/b").ShouldBeTrue();
    warnings.ShouldBeEmpty();
  }

  [Fact]
  public void ReadBranches_UnsupportedPattern_ShouldWarnAndStayInTheList()
  {
    var warnings = new List<ConfigWarning>();

    var patterns = ConfigPatterns.ReadBranches(Texts("(?=a)"), "comment.branches", warnings);

    patterns.ShouldHaveSingleItem().IsSupported.ShouldBeFalse();
    warnings.ShouldBe([new ConfigWarning(ConfigWarning.UnsupportedPattern, "comment.branches[0]", "(?=a)")]);
  }

  [Fact]
  public void ReadPaths_NullOrAbsent_ShouldBeEmptyWithoutWarning()
  {
    var warnings = new List<ConfigWarning>();

    ConfigPatterns.ReadPaths(null, "paths", warnings).ShouldBeEmpty();
    ConfigPatterns.ReadPaths(ConfigNode.Null, "paths", warnings).ShouldBeEmpty();
    warnings.ShouldBeEmpty();
  }

  [Fact]
  public void ReadPaths_NotAList_ShouldWarn()
  {
    var warnings = new List<ConfigWarning>();

    ConfigPatterns.ReadPaths(Text("src/"), "paths", warnings).ShouldBeEmpty();
    warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "paths", "src/")]);
  }

  #endregion
}

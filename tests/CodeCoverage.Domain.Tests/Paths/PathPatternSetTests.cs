// ------------------------------------------------------------------------------------------------
// <copyright file="PathPatternSetTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

// The branch cases come from test_match of Codecov's libs/shared/tests/unit/utils/test_match.py.
public sealed class PathPatternSetTests
{
  #region Methods

  [Fact]
  public void Includes_WithNoPattern_ShouldIncludeEverything()
  {
    new PathPatternSet([]).Includes("src/a.cs").ShouldBeTrue();
  }

  [Fact]
  public void Includes_WithOnlyNegatives_ShouldIncludeOthers()
  {
    var set = Paths("!tests");

    set.Includes("src/a.cs").ShouldBeTrue();
    set.Includes("tests/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void Includes_WithPositives_ShouldRequireOneToMatch()
  {
    var set = Paths("src", "lib");

    set.Includes("lib/a.cs").ShouldBeTrue();
    set.Includes("tests/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void Includes_WhenPositiveAndNegativeMatch_ShouldExclude()
  {
    Paths("src", "!src/generated").Includes("src/generated/a.cs").ShouldBeFalse();
  }

  [Theory]
  [InlineData(new[] { "branch*" }, "branch", true)]
  [InlineData(new[] { "features/.*", "develop" }, "features/a", true)]
  [InlineData(new[] { "feature/.*", "patch.*" }, "patch-1", true)]
  [InlineData(new[] { "main" }, "main", true)]
  [InlineData(new[] { ".*", "!patch" }, "patch", false)]
  [InlineData(new[] { ".*", "!patch" }, "main", true)]
  [InlineData(new[] { "!patch" }, "patch", false)]
  [InlineData(new[] { "!patch" }, "main", true)]
  [InlineData(new[] { "^!patch" }, "main", true)]
  [InlineData(new[] { ".*", "!patch.*" }, "patch-a", false)]
  [InlineData(new[] { "!wip.*", ".*coverage.*" }, "go-coverage", true)]
  [InlineData(new[] { "!patch", "main" }, "main", true)]
  [InlineData(new[] { "featur$" }, "feature", false)]
  public void Includes_Branches_ShouldFollowCodecov(string[] patterns, string branch, bool expected)
  {
    var set = new PathPatternSet(patterns.Select(PathPattern.ParseBranch));

    set.Includes(branch).ShouldBe(expected);
  }

  [Fact]
  public void Includes_InvertedIgnore_ShouldExcludeWhatItMatches()
  {
    var set = new PathPatternSet([PathPattern.Parse("tests").Invert()]);

    set.Includes("tests/a.cs").ShouldBeFalse();
    set.Includes("src/a.cs").ShouldBeTrue();
  }

  [Fact]
  public void Includes_NegatedIgnoreEntry_ShouldBecomeAPositive()
  {
    var set = new PathPatternSet([PathPattern.Parse("!src").Invert()]);

    set.Includes("src/a.cs").ShouldBeTrue();
    set.Includes("tests/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void Includes_WithOnlyARefusedPositive_ShouldIncludeNothing()
  {
    Paths("(?=x)y").Includes("xy").ShouldBeFalse();
  }

  [Fact]
  public void Includes_WithARefusedNegative_ShouldExcludeNothing()
  {
    Paths("!(?=x)y").Includes("xy").ShouldBeTrue();
  }

  [Fact]
  public void Patterns_ShouldKeepEveryPattern()
  {
    Paths("src", "!tests").Patterns.Select(pattern => pattern.Source).ShouldBe(["src", "!tests"]);
  }

  [Fact]
  public void Ctor_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new PathPatternSet(null!));
  }

  [Fact]
  public void Includes_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => Paths("src").Includes(null!));
  }

  private static PathPatternSet Paths(params string[] patterns)
  {
    return new PathPatternSet(patterns.Select(PathPattern.Parse));
  }

  #endregion
}

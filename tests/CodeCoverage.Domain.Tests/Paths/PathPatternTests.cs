// ------------------------------------------------------------------------------------------------
// <copyright file="PathPatternTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics;

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

// The expected values come from Codecov's own tests: TestPathPatternSchemaField,
// TestPatternTypeDetermination, TestGlobToRegexTranslation and TestUserGivenBranchRegex of
// libs/shared/tests/unit/validation/test_helpers.py.
public sealed class PathPatternTests
{
  #region Methods

  [Theory]
  [InlineData("a/**/b", "a/path/b", true)]
  [InlineData("a/**/b", "a/path/path2/b", true)]
  [InlineData("a/**/b", "a/path/b/some_file.py", false)]
  [InlineData("a/**/b", "a/c", false)]
  [InlineData("Snapshots/**", "Snapshots/Snapshots/ViewController.swift", true)]
  [InlineData("test/**/*.*", "test/unit/presenters/goal_sparkline_test.rb", true)]
  public void Match_Glob_DoubleStar_ShouldCrossDirectories(
    string pattern,
    string path,
    bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("a/**/b", "a/b")]
  [InlineData("**/*bundle", "root.bundle")]
  public void Match_Glob_DoubleStarSlash_ShouldRequireADirectory(string pattern, string path)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBeFalse();
  }

  [Theory]
  [InlineData("tests/*", "tests/file_1.py", true)]
  [InlineData("tests/*", "tests/deep/file_1.py", false)]
  [InlineData("a/*/b", "a/path/b", true)]
  [InlineData("a/*/b", "a/path/b/file_2.py", false)]
  [InlineData("a/*/b", "a/b", false)]
  [InlineData("a/*/b", "a/path/path2/b", false)]
  [InlineData("*.cs", "a.cs", true)]
  [InlineData("*.cs", "src/a.cs", false)]
  public void Match_Glob_Star_ShouldStayInADirectory(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("**/*bundle", "app/Bundle/ignoreme.bundle", true)]
  [InlineData("**/*bundle", "tests/test_bundle/sample_bundle", true)]
  [InlineData("**/*bundle", "app/Bundle/mybundle.py", false)]
  [InlineData("a/*/*b*.ts", "a/folder/test_b_test.ts", true)]
  [InlineData("a/*/*b*.ts", "a/b.ts", false)]
  public void Match_Glob_ShouldBeAnchoredAtBothEnds(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Fact]
  public void Match_Glob_QuestionMark_ShouldCrossDirectories()
  {
    PathPattern.Parse("a/*?c").Match("a/x/c").ShouldBeTrue();
  }

  [Theory]
  [InlineData("src/*[a-c].cs", "src/xb.cs", true)]
  [InlineData("src/*[a-c].cs", "src/xd.cs", false)]
  [InlineData("src/*[!a].cs", "src/xb.cs", true)]
  [InlineData("src/*[!a].cs", "src/xa.cs", false)]
  [InlineData("src/*[", "src/x[", true)]
  public void Match_Glob_CharacterClass_ShouldFollowShellRules(
    string pattern,
    string path,
    bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("src/*.cs$", "src/a.cs$", true)]
  [InlineData("src/*.cs$", "src/a.cs", false)]
  public void Match_GlobWithDollar_ShouldTreatItAsALiteral(
    string pattern,
    string path,
    bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("[a-z]+/test_.*", "perro/test_folder.py", true)]
  [InlineData("[a-z]+/test_.*", "cachorro/tes_folder.py", false)]
  [InlineData("[a-z]+/test_.*", "cachorro/what/test_folder.py", false)]
  [InlineData("[a-z]+/test_.*", "[/a/b", false)]
  [InlineData(@"src/.*\.cs$", "lib/src/a.cs", false)]
  public void Match_Regex_ShouldBeAnchored(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Fact]
  public void Match_Regex_ShouldNotBeAnchoredAtTheEnd()
  {
    PathPattern.Parse(@"src/.*\.cs").Match("src/a.cshtml").ShouldBeTrue();
  }

  [Theory]
  [InlineData("path/.*/folder", "path/a/b/folder", true)]
  [InlineData("path/[a-z]*/folder", "path//folder", true)]
  [InlineData("*/[a-z]*/folder", "x/ab/folder", true)]
  [InlineData("*/[a-z]*/folder", "x//folder", false)]
  public void Match_AmbiguousPattern_ShouldBeRecognizedLikeCodecov(
    string pattern,
    string path,
    bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("*.*", "a.b", true)]
  [InlineData("*.*", "x/a.b", false)]
  [InlineData("(a$", "(a$", true)]
  [InlineData("(a$", "a", false)]
  [InlineData("(?P<x>a)$", "(?P<x>a)$", true)]
  public void Match_InvalidRegex_ShouldFallBackToGlob(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("a/b", "a/b", true)]
  [InlineData("a/b", "a/b/file_1.py", true)]
  [InlineData("a/b", "c/a/b", false)]
  [InlineData("a/b", "a/path/b", false)]
  [InlineData("path/to/folder", "path/to/folder/more_path/some_file.py", true)]
  public void Match_Prefix_ShouldMatchSubpaths(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Fact]
  public void Match_Prefix_ShouldNotRequireASegmentBoundary()
  {
    PathPattern.Parse("src/foo").Match("src/foobar/a.cs").ShouldBeTrue();
  }

  [Theory]
  [InlineData("a.b", "axb", true)]
  [InlineData("a?b", "b/c", true)]
  [InlineData("src/(a|b)", "src/b/c", true)]
  [InlineData("^src/a", "src/a/b", true)]
  [InlineData("^src/a", "x/src/a", false)]
  public void Match_Prefix_ShouldNotBeEscaped(string pattern, string path, bool expected)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("src/(a", "src/(a/b", true)]
  [InlineData("src/(a", "src/a", false)]
  [InlineData("a)|(?:b", "a)|(?:b/c", true)]
  [InlineData("a)|(?:b", "b", false)]
  public void Match_InvalidPrefix_ShouldBeLiteral(string pattern, string path, bool expected)
  {
    var result = PathPattern.Parse(pattern);

    result.IsSupported.ShouldBeTrue();
    result.Match(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("./src/register-test-globals.ts", "src/register-test-globals.ts")]
  [InlineData("./test/*.cc", "test/test_SW_Markov.cc")]
  [InlineData("!./test/*.cc", "test/test_SW_Markov.cc")]
  public void Match_LeadingDotSlash_ShouldBeIgnored(string pattern, string path)
  {
    PathPattern.Parse(pattern).Match(path).ShouldBeTrue();
  }

  [Theory]
  [InlineData("!path/to/folder", true)]
  [InlineData("!!path/to/folder", true)]
  [InlineData("path/to/folder", false)]
  [InlineData("path!/to/folder", false)]
  public void Parse_ShouldReadTheNegation(string pattern, bool expected)
  {
    PathPattern.Parse(pattern).IsNegative.ShouldBe(expected);
  }

  [Fact]
  public void Match_NegativePattern_ShouldStillReportWhatItMatches()
  {
    var pattern = PathPattern.Parse("!path/to/folder");

    pattern.Match("path/to/folder/file_2.py").ShouldBeTrue();
    pattern.Match("path/folder").ShouldBeFalse();
  }

  [Fact]
  public void Parse_ShouldKeepTheSource()
  {
    PathPattern.Parse("!./src/*").Source.ShouldBe("!./src/*");
  }

  [Theory]
  [InlineData(@"(a)\1$")]
  [InlineData("foo(?=bar)$")]
  [InlineData("foo(?!bar)$")]
  [InlineData("(?<=foo)bar$")]
  [InlineData("(?<!foo)bar$")]
  [InlineData("(?>a+)b$")]
  [InlineData("(?=x)y")]
  [InlineData("(a{1000}){1000}")]
  [InlineData("src/*[z-a].cs")]
  public void Parse_RefusedPattern_ShouldBeUnsupportedAndMatchNothing(string pattern)
  {
    var result = PathPattern.Parse(pattern);

    result.IsSupported.ShouldBeFalse();
    result.Match("foobar").ShouldBeFalse();
    result.Match(pattern).ShouldBeFalse();
  }

  [Theory]
  [InlineData("(a+)+$")]
  [InlineData("(a|aa)+$")]
  [InlineData("(.*)*b$")]
  [InlineData("([a-z]*)*b$")]
  [InlineData("(a+)+b")]
  public void Match_CatastrophicPattern_ShouldFinishQuickly(string pattern)
  {
    var parsed = PathPattern.Parse(pattern);
    var path = new string('a', 5000) + "!";
    var stopwatch = Stopwatch.StartNew();

    var result = parsed.Match(path);

    stopwatch.Stop();
    parsed.IsSupported.ShouldBeTrue();
    result.ShouldBeFalse();
    stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
  }

  [Theory]
  [InlineData("main", "main", true)]
  [InlineData("main", "main-2", false)]
  [InlineData("main", "my-main", false)]
  [InlineData("  main ", "main", true)]
  [InlineData("apple*", "apple", true)]
  [InlineData("apple*", "apple/x/y", true)]
  [InlineData("feature/*", "feature/a/b", true)]
  [InlineData("*foo", "x/foo", true)]
  [InlineData("*foo", "foox", false)]
  [InlineData("*", "x/y", true)]
  [InlineData("", "x/y", true)]
  [InlineData(".*", "x/y", true)]
  [InlineData("^ma", "ma", true)]
  [InlineData("^ma", "main", false)]
  [InlineData("ma.*", "main", true)]
  [InlineData("featur$", "feature", false)]
  public void ParseBranch_ShouldFollowBranchRules(string pattern, string branch, bool expected)
  {
    PathPattern.ParseBranch(pattern).Match(branch).ShouldBe(expected);
  }

  [Theory]
  [InlineData("!patch", "patch")]
  [InlineData("^!patch", "patch")]
  [InlineData("!!patch", "patch")]
  [InlineData("!wip.*", "wip-1")]
  [InlineData("!wip*", "wip-1")]
  public void ParseBranch_Negation_ShouldBeRead(string pattern, string branch)
  {
    var result = PathPattern.ParseBranch(pattern);

    result.IsNegative.ShouldBeTrue();
    result.Match(branch).ShouldBeTrue();
  }

  [Theory]
  [InlineData("(a")]
  [InlineData("a(?=b)")]
  public void ParseBranch_InvalidOrRefused_ShouldBeUnsupported(string pattern)
  {
    var result = PathPattern.ParseBranch(pattern);

    result.IsSupported.ShouldBeFalse();
    result.Match(pattern).ShouldBeFalse();
  }

  [Fact]
  public void ParseBranch_CatastrophicPattern_ShouldFinish()
  {
    PathPattern.ParseBranch("(a+)+").Match(new string('a', 5000) + "!").ShouldBeFalse();
  }

  [Fact]
  public void Invert_ShouldFlipTheNegationAndKeepTheMatch()
  {
    var positive = PathPattern.Parse("src");
    var negative = positive.Invert();

    negative.IsNegative.ShouldBeTrue();
    negative.Source.ShouldBe("src");
    negative.Match("src/a.cs").ShouldBeTrue();
    negative.Invert().IsNegative.ShouldBeFalse();
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => PathPattern.Parse(null!));
  }

  [Fact]
  public void ParseBranch_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => PathPattern.ParseBranch(null!));
  }

  [Fact]
  public void Match_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => PathPattern.Parse("a").Match(null!));
  }

  #endregion
}

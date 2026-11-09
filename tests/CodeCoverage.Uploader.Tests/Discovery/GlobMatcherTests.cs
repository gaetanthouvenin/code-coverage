// ------------------------------------------------------------------------------------------------
// <copyright file="GlobMatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Discovery;

namespace CodeCoverage.Uploader.Tests.Discovery;

public sealed class GlobMatcherTests
{
  #region Methods

  [Theory]
  [InlineData("lcov.info", "lcov.info", true)]
  [InlineData("lcov.info", "a/lcov.info", false)]
  [InlineData("*.lcov", "app.lcov", true)]
  [InlineData("*.lcov", ".lcov", true)]
  [InlineData("*.lcov", "a/app.lcov", false)]
  [InlineData("*cobertura*.xml", "my.cobertura.report.xml", true)]
  [InlineData("a*b*c", "aXXbYYc", true)]
  [InlineData("a*b*c", "aXXbYY", false)]
  [InlineData("a?c", "abc", true)]
  [InlineData("a?c", "ac", false)]
  [InlineData("a?c", "a/c", false)]
  public void IsMatch_WithinOneSegment_ShouldFollowTheWildcards(
    string glob,
    string path,
    bool expected)
  {
    new GlobMatcher(glob).IsMatch(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("**/lcov.info", "lcov.info", true)]
  [InlineData("**/lcov.info", "a/b/c/lcov.info", true)]
  [InlineData("**/lcov.info", "a/lcov.info/b", false)]
  [InlineData("a/**/lcov.info", "a/lcov.info", true)]
  [InlineData("a/**/lcov.info", "a/x/y/lcov.info", true)]
  [InlineData("a/**/lcov.info", "b/x/lcov.info", false)]
  [InlineData("**/a/**/b/**/c.xml", "x/a/y/b/z/c.xml", true)]
  [InlineData("**/a/**/b/**/c.xml", "x/b/y/a/z/c.xml", false)]
  [InlineData("**/**/c.xml", "c.xml", true)]
  [InlineData("a/**", "a/b/c", true)]
  [InlineData("a/**", "a", false)]
  [InlineData("**", "a/b", true)]
  public void IsMatch_WithAnySegments_ShouldCrossDirectories(
    string glob,
    string path,
    bool expected)
  {
    new GlobMatcher(glob).IsMatch(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("cov[ae]r.xml", "covar.xml", true)]
  [InlineData("cov[ae]r.xml", "covor.xml", false)]
  [InlineData("[a-c].xml", "b.xml", true)]
  [InlineData("[a-c].xml", "d.xml", false)]
  [InlineData("[!a-c].xml", "d.xml", true)]
  [InlineData("[!a-c].xml", "b.xml", false)]
  [InlineData("[]a].xml", "].xml", true)]
  [InlineData("[^a].xml", "^.xml", true)]
  [InlineData("[z-a].xml", "m.xml", false)]
  [InlineData("[a-].xml", "-.xml", true)]
  [InlineData("[ab.xml", "[ab.xml", true)]
  [InlineData("[ab.xml", "a.xml", false)]
  public void IsMatch_WithCharacterClass_ShouldFollowCodecov(
    string glob,
    string path,
    bool expected)
  {
    new GlobMatcher(glob).IsMatch(path).ShouldBe(expected);
  }

  [Theory]
  [InlineData("LCOV.info", "lcov.info")]
  [InlineData("*.LCOV", "app.lcov")]
  [InlineData("JACOCO*.xml", "jacoco.xml")]
  public void IsMatch_OtherCase_ShouldNeverMatchOnAnyPlatform(string glob, string path)
  {
    new GlobMatcher(glob).IsMatch(path).ShouldBeFalse();
  }

  [Theory]
  [InlineData(@"a\**\lcov.info", "a/b/lcov.info")]
  [InlineData("./a/lcov.info", "a/lcov.info")]
  [InlineData("a//lcov.info", "a/lcov.info")]
  public void IsMatch_OtherSpellingOfThePattern_ShouldBeTheSame(string glob, string path)
  {
    new GlobMatcher(glob).IsMatch(path).ShouldBeTrue();
  }

  [Fact]
  public void IsMatch_ManyWildcardsOnALongName_ShouldNotBacktrackExponentially()
  {
    var glob = string.Concat(Enumerable.Repeat("*a", 50)) + "*b";
    var name = new string('a', 5000);

    new GlobMatcher(glob).IsMatch(name).ShouldBeFalse();
  }

  #endregion
}

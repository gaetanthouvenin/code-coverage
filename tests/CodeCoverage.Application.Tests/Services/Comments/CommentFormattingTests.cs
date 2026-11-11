// ------------------------------------------------------------------------------------------------
// <copyright file="CommentFormattingTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services.Comments;

using static CodeCoverage.Application.Tests.Services.Comments.CommentSamples;

namespace CodeCoverage.Application.Tests.Services.Comments;

public sealed class CommentFormattingTests
{
  #region Methods

  [Theory]
  [InlineData("81.23184", "81.23%")]
  [InlineData("100", "100.00%")]
  [InlineData("0", "0.00%")]
  [InlineData("0.004", "<0.01%")]
  [InlineData(null, "?")]
  public void Percent_ShouldFollowThePrecisionOfTheConfiguration(string? value, string expected)
  {
    CommentFormatting.Percent(Parse(value), Config()).ShouldBe(expected);
  }

  [Theory]
  [InlineData("0.33", "+0.33%")]
  [InlineData("-3", "-3.00%")]
  [InlineData("0", "ø")]
  [InlineData("0.004", "+<0.01%")]
  // coverage.round is "down" by default: a fall, however small, rounds to the next hundredth below.
  [InlineData("-0.004", "-0.01%")]
  [InlineData(null, "?")]
  public void Delta_ShouldBeSignedAndNameNoImpact(string? value, string expected)
  {
    CommentFormatting.Delta(Parse(value), Config()).ShouldBe(expected);
  }

  [Theory]
  [InlineData(0, "0 lines")]
  [InlineData(1, "1 line")]
  [InlineData(2, "2 lines")]
  [InlineData(15100, "15100 lines")]
  public void Count_ShouldAgreeWithItsNoun(int value, string expected)
  {
    CommentFormatting.Count(value, "line").ShouldBe(expected);
  }

  [Fact]
  public void ShortSha_ShouldKeepSevenCharacters()
  {
    CommentFormatting.ShortSha(HeadSha).ShouldBe("e4f5a6b");
  }

  [Fact]
  public void Link_ShouldWriteTheTextAloneWithoutAnAddress()
  {
    CommentFormatting.Link("`e4f5a6b`", "https://coverage.example.com/x").ShouldBe("[`e4f5a6b`](https://coverage.example.com/x)");
    CommentFormatting.Link("`e4f5a6b`", null).ShouldBe("`e4f5a6b`");
  }

  [Theory]
  [InlineData("src/Api/Foo.cs", "src/Api/Foo.cs")]
  [InlineData("src/my_file.cs", "src/my\\_file.cs")]
  [InlineData("a|b", "a\\|b")]
  [InlineData("[x](y)", "\\[x\\](y)")]
  [InlineData("`code` *bold* ~gone~ <b>", "\\`code\\` \\*bold\\* \\~gone\\~ \\<b\\>")]
  [InlineData("back\\slash", "back\\\\slash")]
  [InlineData("two\nlines", "two lines")]
  public void Escape_ShouldLeaveNothingForMarkdownToRead(string text, string expected)
  {
    CommentFormatting.Escape(text).ShouldBe(expected);
  }

  private static decimal? Parse(string? value) =>
    value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

  #endregion
}

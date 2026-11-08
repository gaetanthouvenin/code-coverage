// ------------------------------------------------------------------------------------------------
// <copyright file="GitShaTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.Tests.Git;

public sealed class GitShaTests
{
  #region Fields

  private const string Lower = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

  private const string Upper = "A94A8FE5CCB19BA61C4C0873D391E987982FBBD3";

  #endregion

  #region Methods

  [Fact]
  public void Parse_FullSha_ShouldKeepTheValue()
  {
    GitSha.Parse(Lower).Value.ShouldBe(Lower);
  }

  [Fact]
  public void Parse_UpperCase_ShouldBeLowered()
  {
    GitSha.Parse(Upper).Value.ShouldBe(Lower);
  }

  [Fact]
  public void Equals_DifferentCases_ShouldBeTrue()
  {
    GitSha.Parse(Upper).ShouldBe(GitSha.Parse(Lower));
  }

  [Fact]
  public void ToString_ShouldReturnTheValue()
  {
    GitSha.Parse(Upper).ToString().ShouldBe(Lower);
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitSha.Parse(null!));
  }

  [Theory]
  [InlineData("")]
  [InlineData("a94a8fe")]
  [InlineData("a94a8fe5ccb19ba61c4c0873d391e987982fbbd33")]
  [InlineData("g94a8fe5ccb19ba61c4c0873d391e987982fbbd3")]
  [InlineData("a94a8fe5ccb19ba61c4c0873d391e98/../bbd3")]
  public void Parse_NotAFullSha_ShouldThrow(string value)
  {
    Should.Throw<ArgumentException>(() => GitSha.Parse(value));
  }

  [Fact]
  public void TryParse_FullSha_ShouldReturnTheSha()
  {
    GitSha.TryParse(Upper, out var sha).ShouldBeTrue();

    sha.ShouldBe(GitSha.Parse(Lower));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("a94a8fe")]
  [InlineData("g94a8fe5ccb19ba61c4c0873d391e987982fbbd3")]
  public void TryParse_NotAFullSha_ShouldReturnFalse(string? value)
  {
    GitSha.TryParse(value, out var sha).ShouldBeFalse();

    sha.ShouldBeNull();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="GitShaListConverterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence.Conventions;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class GitShaListConverterTests
{
  #region Fields

  private const string First = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

  private const string Second = "b94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

  #endregion

  #region Methods

  [Fact]
  public void ConvertToProvider_ShouldWriteAnArrayOfTexts()
  {
    var converter = new GitShaListConverter();

    converter.ConvertToProvider(new[] { GitSha.Parse(First), GitSha.Parse(Second) }).ShouldBe($"""["{First}","{Second}"]""");
  }

  [Fact]
  public void ConvertFromProvider_ShouldReadTheShasInOrder()
  {
    var converter = new GitShaListConverter();

    converter.ConvertFromProvider($"""["{Second}","{First}"]""").ShouldBeAssignableTo<IReadOnlyList<GitSha>>()
             .ShouldBe([GitSha.Parse(Second), GitSha.Parse(First)]);
  }

  [Fact]
  public void ConvertFromProvider_NoParent_ShouldReadAnEmptyList()
  {
    var converter = new GitShaListConverter();

    converter.ConvertFromProvider("[]").ShouldBeAssignableTo<IReadOnlyList<GitSha>>().ShouldBeEmpty();
  }

  // The invariant of GitSha holds on the way back too.
  [Fact]
  public void ConvertFromProvider_NotASha_ShouldThrow()
  {
    var converter = new GitShaListConverter();

    Should.Throw<ArgumentException>(() => converter.ConvertFromProvider("""["not-a-sha"]"""));
  }

  #endregion
}

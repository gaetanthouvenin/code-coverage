// ------------------------------------------------------------------------------------------------
// <copyright file="ConsistencyCheckerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;
using CodeCoverage.Uploader.Git;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Git;

public sealed class ConsistencyCheckerTests
{
  #region Fields

  private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  private const string Detected = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

  private const string Other = "cccccccccccccccccccccccccccccccccccccccc";

  private readonly StringWriter _output = new();

  #endregion

  #region Methods

  [Fact]
  public void Check_PushBuildShaDiffersFromHead_ShouldWarn()
  {
    Check(new DetectedContext { Service = "github-actions", Sha = Detected }, Head, [Other]);

    _output.ToString().ShouldStartWith("warning:");
    _output.ToString().ShouldContain(Detected);
  }

  [Fact]
  public void Check_PullRequestBuildShaDiffersFromHead_ShouldStaySilent()
  {
    Check(new DetectedContext { Service = "github-actions", Sha = Detected, PullRequest = 5 }, Head, [Other]);

    _output.ToString().ShouldBeEmpty();
  }

  [Fact]
  public void Check_MergeHeadOfTheDetectedCommit_ShouldOnlyInform()
  {
    Check(new DetectedContext { Service = "github-actions", Sha = Detected, PullRequest = 5 }, Head, [Other, Detected]);

    _output.ToString().ShouldContain("second parent");
    _output.ToString().ShouldNotContain("warning:");
  }

  [Fact]
  public void Check_MergeHeadOfTheDetectedCommitOnAPush_ShouldOnlyInform()
  {
    Check(new DetectedContext { Service = "github-actions", Sha = Detected }, Head, [Other, Detected]);

    _output.ToString().ShouldNotContain("warning:");
  }

  [Fact]
  public void Check_SameCommit_ShouldStaySilent()
  {
    Check(new DetectedContext { Service = "github-actions", Sha = Head }, Head, []);

    _output.ToString().ShouldBeEmpty();
  }

  [Fact]
  public void Check_NoDetectedCommit_ShouldStaySilent()
  {
    Check(new DetectedContext { Service = "local" }, Head, []);

    _output.ToString().ShouldBeEmpty();
  }

  [Fact]
  public void Check_GitFails_ShouldStaySilent()
  {
    Check(new DetectedContext { Service = "local", Sha = Detected }, null, []);

    _output.ToString().ShouldBeEmpty();
  }

  private void Check(DetectedContext context, string? head, string[] parents)
  {
    ConsistencyChecker.Check(
      context,
      arguments => arguments[1] == "HEAD" ? head : string.Join('\n', parents),
      new ConsoleReporter(_output)
    );
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubPushEventTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

public sealed class GitHubPushEventTests
{
  #region Fields

  private const string Head = "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

  private const string Zero = "0000000000000000000000000000000000000000";

  #endregion

  #region Methods

  [Fact]
  public void Parse_Branch_ShouldReadItsNameAndItsHead()
  {
    var push = Parse($$"""{"ref":"refs/heads/feature/x","before":"{{Zero}}","after":"{{Head}}","created":true,"deleted":false,"forced":false,"commits":[]}""");

    push.Branch.ShouldBe("feature/x");
    push.Head.ShouldNotBeNull().Value.ShouldBe(Head);
    push.Deleted.ShouldBeFalse();
    push.TouchesConfig.ShouldBeFalse();
  }

  [Fact]
  public void Parse_Deleted_ShouldNameTheBranchWithoutAHead()
  {
    var push = Parse($$"""{"ref":"refs/heads/gone","after":"{{Zero}}","deleted":true,"head_commit":null,"commits":[]}""");

    push.Branch.ShouldBe("gone");
    push.Head.ShouldBeNull();
    push.Deleted.ShouldBeTrue();
  }

  [Theory]
  [InlineData("refs/tags/v1.0")]
  [InlineData("refs/heads/")]
  [InlineData("main")]
  public void Parse_NotABranch_ShouldNameNone(string reference)
  {
    var push = Parse($$"""{"ref":"{{reference}}","after":"{{Head}}","deleted":false}""");

    push.Branch.ShouldBeNull();
    push.Head.ShouldBeNull();
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("{}")]
  [InlineData("""{"ref":42}""")]
  [InlineData("""{"ref":"refs/heads/main"}""")]
  [InlineData("""{"ref":"refs/heads/main","after":"not-a-sha"}""")]
  [InlineData("""{"ref":"refs/heads/main","after":"0000000000000000000000000000000000000000"}""")]
  public void Parse_NothingToApply_ShouldNameNoBranch(string json)
  {
    var push = Parse(json);

    push.Branch.ShouldBeNull();
    push.Deleted.ShouldBeFalse();
  }

  [Theory]
  [InlineData("""{"added":["codecov.yml"]}""")]
  [InlineData("""{"modified":[".github/.codecov.yaml"]}""")]
  [InlineData("""{"added":["src/A.cs"],"modified":["dev/codecov.yaml"]}""")]
  public void Parse_CommitTouchingTheConfiguration_ShouldSaySo(string commit)
  {
    Parse(Push("[" + commit + "]", "null")).TouchesConfig.ShouldBeTrue();
    // GitHub cuts the list of commits: the head commit is read as well.
    Parse(Push("[]", commit)).TouchesConfig.ShouldBeTrue();
  }

  [Theory]
  [InlineData("""{"removed":["codecov.yml"]}""")]
  [InlineData("""{"added":["docs/codecov.yml"]}""")]
  [InlineData("""{"modified":["Codecov.yml"]}""")]
  [InlineData("""{"added":"codecov.yml"}""")]
  [InlineData("""{"added":[42]}""")]
  [InlineData("42")]
  public void Parse_CommitNotTouchingTheConfiguration_ShouldSayNo(string commit)
  {
    Parse(Push("[" + commit + "]", commit)).TouchesConfig.ShouldBeFalse();
  }

  // F14. Where the branch was before the push: a branch created by the push was nowhere.
  [Fact]
  public void Parse_ShouldReadTheCommitBeforeThePush()
  {
    const string Previous = "c94a8fe5ccb19ba61c4c0873d391e987982fbbd3";

    Parse($$"""{"ref":"refs/heads/main","before":"{{Previous}}","after":"{{Head}}","commits":[]}""").Before.ShouldNotBeNull().Value.ShouldBe(Previous);
    Parse($$"""{"ref":"refs/heads/main","before":"{{Zero}}","after":"{{Head}}","commits":[]}""").Before.ShouldBeNull();
    Parse($$"""{"ref":"refs/heads/main","after":"{{Head}}","commits":[]}""").Before.ShouldBeNull();
  }

  // F17. A forced push may take the branch back to a commit whose configuration differs, and lists none of the commits
  // it removed: the file is read again.
  [Fact]
  public void Parse_ForcedPush_ShouldSayTheConfigurationMayHaveChanged()
  {
    Parse($$"""{"ref":"refs/heads/main","after":"{{Head}}","forced":true,"commits":[],"head_commit":null}""").TouchesConfig.ShouldBeTrue();
    Parse($$"""{"ref":"refs/heads/main","after":"{{Head}}","forced":false,"commits":[],"head_commit":null}""").TouchesConfig.ShouldBeFalse();
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitHubPushEvent.Parse(null!));
  }

  private static string Push(string commits, string headCommit) =>
    "{\"ref\":\"refs/heads/main\",\"after\":\"" + Head + "\",\"deleted\":false,\"commits\":" + commits + ",\"head_commit\":" + headCommit + "}";

  private static GitHubPushEvent Parse(string json) => GitHubPushEvent.Parse(Encoding.UTF8.GetBytes(json));

  #endregion
}

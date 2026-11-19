// ------------------------------------------------------------------------------------------------
// <copyright file="AzureDevOpsGatewayMapperTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.AzureDevOps;
using CodeCoverage.Infrastructure.AzureDevOps.Models;

namespace CodeCoverage.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsGatewayMapperTests
{
  #region Fields

  private const string Head = "be67f8871a4d2c75f13a51c1d3c30ac0d74d4ef4";

  private const string Base = "f47bbc106853afe3c1b07a81754bce5f4b8dbf62";

  private static readonly Guid RepositoryId = new("1701bc47-1145-4739-a805-64b02e40dbe7");

  private static readonly DateTimeOffset AuthoredAt = new(2026, 11, 15, 17, 6, 33, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void ToForgeRepository_NoDefaultBranch_ShouldUseTheBranchOfAnEmptyRepository()
  {
    var repository = new AzureDevOpsRepository(RepositoryId, "api", new AzureDevOpsProject(Guid.NewGuid(), "Projet A"));

    var mapped = repository.ToForgeRepository("contoso", "org-id");

    mapped.DefaultBranch.ShouldBe(AzureDevOpsGatewayMapper.EmptyRepositoryBranch);
  }

  [Fact]
  public void ToForgeRepository_EmptyDefaultBranch_ShouldUseTheBranchOfAnEmptyRepository()
  {
    var repository = new AzureDevOpsRepository(RepositoryId, "api", new AzureDevOpsProject(Guid.NewGuid(), "Projet A"), string.Empty);

    var mapped = repository.ToForgeRepository("contoso", "org-id");

    mapped.DefaultBranch.ShouldBe(AzureDevOpsGatewayMapper.EmptyRepositoryBranch);
  }

  [Fact]
  public void ToForgeRepository_UnprefixedDefaultBranch_ShouldKeepItAsItIs()
  {
    var repository = new AzureDevOpsRepository(RepositoryId, "api", new AzureDevOpsProject(Guid.NewGuid(), "Projet A"), "develop");

    var mapped = repository.ToForgeRepository("contoso", "org-id");

    mapped.DefaultBranch.ShouldBe("develop");
  }

  [Theory]
  [InlineData("public", false)]
  [InlineData("Public", false)]
  [InlineData("private", true)]
  [InlineData(null, true)]
  public void ToForgeRepository_Visibility_ShouldBePrivateUnlessTheProjectIsPublic(string? visibility, bool isPrivate)
  {
    var repository = new AzureDevOpsRepository(RepositoryId, "api", new AzureDevOpsProject(Guid.NewGuid(), "Projet A", visibility), "refs/heads/main");

    var mapped = repository.ToForgeRepository("contoso", "org-id");

    mapped.IsPrivate.ShouldBe(isPrivate);
    mapped.ExternalId.ShouldBe("1701bc47-1145-4739-a805-64b02e40dbe7");
  }

  [Fact]
  public void ToForgeRepository_NullRepository_ShouldThrowArgumentNull()
  {
    Should.Throw<ArgumentNullException>(() => ((AzureDevOpsRepository)null!).ToForgeRepository("contoso", "org-id"));
  }

  [Fact]
  public void ToForgeRepository_NullOrganizationOrOrganizationId_ShouldThrowArgumentNull()
  {
    var repository = new AzureDevOpsRepository(RepositoryId, "api", new AzureDevOpsProject(Guid.NewGuid(), "Projet A"));

    Should.Throw<ArgumentNullException>(() => repository.ToForgeRepository(null!, "org-id"));
    Should.Throw<ArgumentNullException>(() => repository.ToForgeRepository("contoso", null!));
  }

  [Fact]
  public void ToForgeCommit_NoParentsNorComment_ShouldBeAnEmptyRoot()
  {
    var commit = new AzureDevOpsCommit(Head, Committer: new AzureDevOpsGitUser(Date: AuthoredAt));

    var mapped = commit.ToForgeCommit();

    mapped.ParentShas.ShouldBeEmpty();
    mapped.Message.ShouldBeEmpty();
    mapped.CommittedAt.ShouldBe(AuthoredAt);
  }

  [Fact]
  public void ToForgeCommit_NoCommitterDate_ShouldUseTheAuthorDate()
  {
    var commit = new AzureDevOpsCommit(Head, [Base], "Message", new AzureDevOpsGitUser(Date: AuthoredAt), new AzureDevOpsGitUser("Norman"));

    var mapped = commit.ToForgeCommit();

    mapped.CommittedAt.ShouldBe(AuthoredAt);
    mapped.ParentShas.ShouldBe([GitSha.Parse(Base)]);
  }

  [Fact]
  public void ToForgeCommit_NoCommitter_ShouldUseTheAuthorDate()
  {
    var commit = new AzureDevOpsCommit(Head, Author: new AzureDevOpsGitUser(Date: AuthoredAt));

    commit.ToForgeCommit().CommittedAt.ShouldBe(AuthoredAt);
  }

  [Fact]
  public void ToForgeCommit_NoDate_ShouldThrowJson()
  {
    var commit = new AzureDevOpsCommit(Head, Author: new AzureDevOpsGitUser("Norman"));

    Should.Throw<JsonException>(() => commit.ToForgeCommit());
  }

  [Fact]
  public void ToForgeCommit_NoAuthorNorCommitter_ShouldThrowJson()
  {
    Should.Throw<JsonException>(() => new AzureDevOpsCommit(Head).ToForgeCommit());
  }

  [Fact]
  public void ToForgeCommit_MalformedSha_ShouldThrowJson()
  {
    var commit = new AzureDevOpsCommit("be67f88", Committer: new AzureDevOpsGitUser(Date: AuthoredAt));

    Should.Throw<JsonException>(() => commit.ToForgeCommit());
  }

  [Fact]
  public void ToForgeCommit_Null_ShouldThrowArgumentNull()
  {
    Should.Throw<ArgumentNullException>(() => ((AzureDevOpsCommit)null!).ToForgeCommit());
  }

  [Fact]
  public void ToForgePull_NoCreatorNorMergeCommit_ShouldLeaveThemNull()
  {
    var pull = new AzureDevOpsPullRequest(
      22,
      "active",
      "Title",
      "feature/coverage",
      "refs/heads/main",
      new AzureDevOpsCommitRef(Head),
      new AzureDevOpsCommitRef(Base)
    );

    var mapped = pull.ToForgePull();

    mapped.AuthorLogin.ShouldBeNull();
    mapped.MergeCommitSha.ShouldBeNull();
    mapped.HeadBranch.ShouldBe("feature/coverage");
    mapped.BaseBranch.ShouldBe("main");
  }

  [Fact]
  public void ToForgePull_CreatorWithoutUniqueName_ShouldLeaveTheLoginNull()
  {
    var pull = new AzureDevOpsPullRequest(
      22,
      "active",
      "Title",
      "refs/heads/feature/coverage",
      "refs/heads/main",
      new AzureDevOpsCommitRef(Head),
      new AzureDevOpsCommitRef(Base),
      new AzureDevOpsIdentity(DisplayName: "Norman Paulk")
    );

    pull.ToForgePull().AuthorLogin.ShouldBeNull();
  }

  [Fact]
  public void ToForgePull_Null_ShouldThrowArgumentNull()
  {
    Should.Throw<ArgumentNullException>(() => ((AzureDevOpsPullRequest)null!).ToForgePull());
  }

  [Fact]
  public void ToBranchHead_NullRefsOrBranch_ShouldThrowArgumentNull()
  {
    Should.Throw<ArgumentNullException>(() => ((AzureDevOpsList<AzureDevOpsRef>)null!).ToBranchHead("main"));
    Should.Throw<ArgumentNullException>(() => new AzureDevOpsList<AzureDevOpsRef>(0, []).ToBranchHead(null!));
  }

  [Fact]
  public void ToBranchHead_DifferentCase_ShouldPickTheBranch()
  {
    var refs = new AzureDevOpsList<AzureDevOpsRef>(1, [new AzureDevOpsRef("refs/heads/Main", Base)]);

    refs.ToBranchHead("main").ShouldBe(GitSha.Parse(Base));
  }

  [Fact]
  public void ToBranchHead_MalformedSha_ShouldThrowJson()
  {
    var refs = new AzureDevOpsList<AzureDevOpsRef>(1, [new AzureDevOpsRef("refs/heads/main", "not-a-sha")]);

    Should.Throw<JsonException>(() => refs.ToBranchHead("main"));
  }

  [Fact]
  public void ToTreeSha_ShouldReadTheRootTree()
  {
    new AzureDevOpsCommit(Head, TreeId: Base).ToTreeSha().ShouldBe(GitSha.Parse(Base));
  }

  [Fact]
  public void ToTreeSha_NoTree_ShouldThrowJson()
  {
    Should.Throw<JsonException>(() => new AzureDevOpsCommit(Head).ToTreeSha());
  }

  [Fact]
  public void ToForgeTree_Submodule_ShouldLeaveItOut()
  {
    var tree = new AzureDevOpsTree(
      Base,
      [new AzureDevOpsTreeEntry(Head, "vendor/shared", "commit"), new AzureDevOpsTreeEntry(Base, "src", "tree")]
    );

    var mapped = tree.ToForgeTree(GitSha.Parse(Head));

    mapped.Entries.ShouldHaveSingleItem().ShouldBe(new ForgeTreeEntry("src", GitSha.Parse(Base), true));
    mapped.Truncated.ShouldBeFalse();
  }

  [Fact]
  public void ToForgeTree_MalformedSha_ShouldThrowJson()
  {
    var tree = new AzureDevOpsTree(Base, [new AzureDevOpsTreeEntry("not-a-sha", "README.md", "blob")]);

    Should.Throw<JsonException>(() => tree.ToForgeTree(GitSha.Parse(Head)));
  }

  #endregion
}

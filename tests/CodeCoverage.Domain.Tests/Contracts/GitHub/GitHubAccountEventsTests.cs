// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAccountEventsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

// The bodies of installation_target, organization, member and membership.
public sealed class GitHubAccountEventsTests
{
  #region Methods

  [Fact]
  public void InstallationTarget_ShouldReadTheAccountByItsIdentifier()
  {
    var body = Target("""{"action":"renamed","account":{"id":1001,"login":"New-Octo","type":"Organization"},"changes":{"login":{"from":"octo"}},"target_type":"User"}""");

    body.AccountId.ShouldBe("1001");
    body.Login.ShouldBe("New-Octo");
    // The kind the account itself gives wins.
    body.AccountType.ShouldBe(OrganizationAccountType.Organization);
  }

  [Theory]
  [InlineData("""{"account":{"id":583231,"login":"octocat"},"target_type":"User"}""", OrganizationAccountType.User)]
  [InlineData("""{"account":{"id":583231,"login":"octocat"},"target_type":"Organization"}""", OrganizationAccountType.Organization)]
  [InlineData("""{"account":{"id":583231,"login":"octocat","type":"User"}}""", OrganizationAccountType.User)]
  [InlineData("""{"account":{"id":583231,"login":"octocat"}}""", null)]
  public void InstallationTarget_ShouldReadTheKindOfAccountWhereGitHubGivesIt(string json, OrganizationAccountType? expected)
  {
    Target(json).AccountType.ShouldBe(expected);
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("{}")]
  [InlineData("""{"account":"octo"}""")]
  [InlineData("""{"account":{"id":"1001"}}""")]
  [InlineData("""{"account":{"id":0,"login":7}}""")]
  [InlineData("""{"account":{"id":-4}}""")]
  public void InstallationTarget_MissingOrMalformedParts_ShouldBeLeftOut(string json)
  {
    var body = Target(json);

    body.AccountId.ShouldBeNull();
    body.Login.ShouldBeNull();
  }

  [Theory]
  // organization: member_added, member_removed.
  [InlineData("""{"action":"member_removed","membership":{"role":"member","state":"active","user":{"id":583231,"login":"octocat"}},"organization":{"id":1001}}""", "583231")]
  // member: a collaborator of a repository.
  [InlineData("""{"action":"removed","member":{"id":583231,"login":"octocat"},"repository":{"id":7}}""", "583231")]
  // membership: a member of a team.
  [InlineData("""{"action":"added","scope":"team","member":{"id":583231},"team":{"id":3}}""", "583231")]
  // Both: the member is the user the event is about.
  [InlineData("""{"member":{"id":1},"membership":{"user":{"id":2}}}""", "1")]
  [InlineData("""{"member":{"login":"octocat"},"membership":{"user":{"id":2}}}""", "2")]
  // No user: the rename or the deletion of an organization, an invitation.
  [InlineData("""{"action":"renamed","organization":{"id":1001}}""", null)]
  [InlineData("""{"action":"member_invited","invitation":{"id":9},"user":{"id":583231}}""", null)]
  [InlineData("""{"member":"octocat"}""", null)]
  [InlineData("""{"member":{"id":"583231"}}""", null)]
  [InlineData("""{"membership":{"user":null}}""", null)]
  [InlineData("[]", null)]
  [InlineData("not json", null)]
  public void Member_ShouldReadTheUserTheEventIsAbout(string json, string? expected)
  {
    GitHubMemberEvent.Parse(Encoding.UTF8.GetBytes(json)).UserExternalId.ShouldBe(expected);
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitHubInstallationTargetEvent.Parse(null!));
    Should.Throw<ArgumentNullException>(() => GitHubMemberEvent.Parse(null!));
  }

  [Fact]
  public void RewriteJob_ShouldRunInTheOrganizationOfTheScopeMergedAndWithoutALogin()
  {
    var job = RewriteRepositoryOwnerJobs.Create();

    job.Type.ShouldBe(JobTypes.RewriteRepositoryOwner);
    job.IsInstance.ShouldBeFalse();
    job.OrganizationId.ShouldBeNull();
    // The login is read when the job runs: none is frozen in the payload.
    job.PayloadJson.ShouldBe("{}");
    job.LockKey.ShouldBe("owner-rewrite");
    job.DedupKey.ShouldBe("owner-rewrite");
    job.Coalesce.ShouldBeTrue();
  }

  private static GitHubInstallationTargetEvent Target(string json) => GitHubInstallationTargetEvent.Parse(Encoding.UTF8.GetBytes(json));

  #endregion
}

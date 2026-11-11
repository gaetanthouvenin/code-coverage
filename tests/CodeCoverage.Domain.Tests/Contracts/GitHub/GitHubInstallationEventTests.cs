// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubInstallationEventTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.GitHub;

public sealed class GitHubInstallationEventTests
{
  #region Methods

  [Fact]
  public void Parse_Created_ShouldReadTheInstallationAndItsAccount()
  {
    var body = Parse("""{"action":"created","installation":{"id":42,"account":{"login":"Octo-Org","id":1001,"type":"Organization"},"target_type":"Organization","suspended_at":null}}""");

    var installation = body.Installation.ShouldNotBeNull();
    installation.Id.ShouldBe(42);
    installation.AccountId.ShouldBe("1001");
    installation.AccountLogin.ShouldBe("Octo-Org");
    installation.AccountType.ShouldBe(OrganizationAccountType.Organization);
    installation.Suspended.ShouldBeFalse();
  }

  [Theory]
  [InlineData("""{"login":"someone","id":2002,"type":"User"}""", "", OrganizationAccountType.User)]
  [InlineData("""{"login":"someone","id":2002}""", ""","target_type":"User" """, OrganizationAccountType.User)]
  [InlineData("""{"login":"someone","id":2002}""", "", OrganizationAccountType.Organization)]
  [InlineData("""{"login":"someone","id":2002,"type":"Bot"}""", "", OrganizationAccountType.Organization)]
  public void Parse_ShouldReadTheKindOfAccount(string account, string more, OrganizationAccountType expected)
  {
    var body = Parse("{\"installation\":{\"id\":42,\"account\":" + account + more + "}}");

    body.Installation.ShouldNotBeNull().AccountType.ShouldBe(expected);
  }

  [Fact]
  public void Parse_Suspended_ShouldSaySo()
  {
    var body = Parse("""{"action":"suspend","installation":{"id":42,"account":{"login":"octo","id":1001},"suspended_at":"2026-11-01T00:00:00Z"}}""");

    body.Installation.ShouldNotBeNull().Suspended.ShouldBeTrue();
  }

  [Fact]
  public void Parse_AccountWithoutLogin_ShouldReadAnEmptyOne()
  {
    var body = Parse("""{"installation":{"id":42,"account":{"id":3003,"slug":"an-enterprise"}}}""");

    body.Installation.ShouldNotBeNull().AccountLogin.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("not json")]
  [InlineData("[]")]
  [InlineData("{}")]
  [InlineData("""{"installation":42}""")]
  [InlineData("""{"installation":{"account":{"id":1001,"login":"octo"}}}""")]
  [InlineData("""{"installation":{"id":0,"account":{"id":1001,"login":"octo"}}}""")]
  [InlineData("""{"installation":{"id":42}}""")]
  [InlineData("""{"installation":{"id":42,"account":"octo"}}""")]
  [InlineData("""{"installation":{"id":42,"account":{"login":"octo"}}}""")]
  [InlineData("""{"installation":{"id":42,"account":{"id":"1001","login":"octo"}}}""")]
  public void Parse_MissingOrMalformedPart_ShouldReadNoInstallation(string json)
  {
    Parse(json).Installation.ShouldBeNull();
  }

  [Fact]
  public void Parse_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => GitHubInstallationEvent.Parse(null!));
  }

  [Theory]
  [InlineData("User", OrganizationAccountType.User)]
  [InlineData("user", OrganizationAccountType.User)]
  [InlineData("Organization", OrganizationAccountType.Organization)]
  [InlineData("Enterprise", OrganizationAccountType.Organization)]
  [InlineData(null, OrganizationAccountType.Organization)]
  public void ToAccountType_ShouldTakeAnythingButAUserForAnOrganization(string? type, OrganizationAccountType expected)
  {
    GitHubAccountTypes.ToAccountType(type).ShouldBe(expected);
  }

  private static GitHubInstallationEvent Parse(string json) => GitHubInstallationEvent.Parse(Encoding.UTF8.GetBytes(json));

  #endregion
}

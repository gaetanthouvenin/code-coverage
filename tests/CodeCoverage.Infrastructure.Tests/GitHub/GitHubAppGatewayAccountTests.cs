// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayAccountTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

// M4.02i: GET /users/{login}, which turns a login an administrator types into the identifier of the account.
public sealed class GitHubAppGatewayAccountTests : IDisposable
{
  #region Fields

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task GetAccountByLogin_User_ShouldMapItsIdentifierAndKind()
  {
    Given("/users/octocat", 200, Fixture("user.json"));

    var account = await _github.Gateway.GetAccountByLoginAsync("octocat", Cancellation);

    account.ShouldBe(new GitHubAccountInfo("1", "octocat", OrganizationAccountType.User));
  }

  // GitHub answers /users/{login} for an organization too, with its own spelling of the login.
  [Fact]
  public async Task GetAccountByLogin_Organization_ShouldMapItsKindAndSpelling()
  {
    Given("/users/Octo-Org", 200, """{"login":"octo-org","id":1001,"type":"Organization"}""");

    var account = await _github.Gateway.GetAccountByLoginAsync(" Octo-Org ", Cancellation);

    account.ShouldBe(new GitHubAccountInfo("1001", "octo-org", OrganizationAccountType.Organization));
  }

  [Fact]
  public async Task GetAccountByLogin_UnknownLogin_ShouldBeNull()
  {
    Given("/users/nobody-here", 404, """{"message":"Not Found"}""");

    (await _github.Gateway.GetAccountByLoginAsync("nobody-here", Cancellation)).ShouldBeNull();
  }

  // The account is public: neither the App nor an installation authenticates the reading, so it works before both.
  [Fact]
  public async Task GetAccountByLogin_ShouldSendNoToken()
  {
    Given("/users/octocat", 200, Fixture("user.json"));

    await _github.Gateway.GetAccountByLoginAsync("octocat", Cancellation);

    var request = _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    request.Headers!.ContainsKey("Authorization").ShouldBeFalse();
  }

  [Theory]
  [InlineData(".")]
  [InlineData("..")]
  public async Task GetAccountByLogin_LoginThatIsNotAPathSegment_ShouldBeNullWithoutACall(string login)
  {
    (await _github.Gateway.GetAccountByLoginAsync(login, Cancellation)).ShouldBeNull();

    _github.Server.LogEntries.ShouldBeEmpty();
  }

  [Fact]
  public async Task GetAccountByLogin_UnreadablePayload_ShouldThrowBadGateway()
  {
    Given("/users/octocat", 200, """{"login":"octocat"}""");

    await Should.ThrowAsync<BadGatewayException>(async () => await _github.Gateway.GetAccountByLoginAsync("octocat", Cancellation));
  }

  [Fact]
  public async Task GetAccountByLogin_BlankLogin_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentException>(async () => await _github.Gateway.GetAccountByLoginAsync(" ", Cancellation));
  }

  public void Dispose() => _github.Dispose();

  private static string Fixture(string name) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "github", name));

  private void Given(string path, int status, string body) =>
    _github.Server.Given(Request.Create().WithPath(path).UsingGet()).RespondWith(Response.Create().WithStatusCode(status).WithBody(body));

  #endregion
}

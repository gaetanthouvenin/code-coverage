// ------------------------------------------------------------------------------------------------
// <copyright file="GitHubAppGatewayInstallationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.GitHub;

public sealed class GitHubAppGatewayInstallationTests : IDisposable
{
  #region Fields

  private const long Installation = 42;

  private const string InstallationToken = "ghs_installation";

  private static readonly RSA Key = RSA.Create(2048);

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Ctors

  public GitHubAppGatewayInstallationTests()
  {
    _github.UsePrivateKey(Key);
    _github.Server
           .Given(Request.Create().WithPath($"/app/installations/{Installation}/access_tokens").UsingPost())
           .RespondWith(
             Response.Create()
                     .WithStatusCode(201)
                     .WithBody(
                       "{\"token\":\"" + InstallationToken + "\",\"expires_at\":\"" + DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture) + "\"}"
                     )
           );
  }

  #endregion

  #region Properties

  private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task ListInstallations_ShouldMapAccountAndSuspension()
  {
    GivenPage(
      "/app/installations",
      1,
      """[{"id":42,"account":{"id":1001,"login":"first-org","type":"Organization"},"suspended_at":null},{"id":43,"account":{"id":2002,"login":"someone"},"suspended_at":"2026-11-01T00:00:00Z"}]"""
    );

    var installations = await _github.Gateway.ListInstallationsAsync(Cancellation);

    installations.Count.ShouldBe(2);
    installations[0].Id.ShouldBe(42);
    installations[0].AccountId.ShouldBe("1001");
    installations[0].AccountLogin.ShouldBe("first-org");
    installations[0].Suspended.ShouldBeFalse();
    installations[1].Id.ShouldBe(43);
    installations[1].Suspended.ShouldBeTrue();
  }

  [Fact]
  public async Task ListInstallations_ShouldSignWithTheAppAndNeverAskAnInstallationToken()
  {
    GivenPage("/app/installations", 1, "[]");

    (await _github.Gateway.ListInstallationsAsync(Cancellation)).ShouldBeEmpty();

    var request = _github.Server.LogEntries.ShouldHaveSingleItem().RequestMessage!;
    request.Query!["per_page"].ShouldBe(["100"]);
    var authorization = request.Headers!["Authorization"].ShouldHaveSingleItem();
    authorization.ShouldStartWith("Bearer ");
    GitHubApiHarness.IsSignedBy(authorization["Bearer ".Length..], Key).ShouldBeTrue();
  }

  [Fact]
  public async Task ListInstallations_FullPage_ShouldReadTheNextOne()
  {
    GivenPage("/app/installations", 1, Installations(1, 100));
    GivenPage("/app/installations", 2, Installations(101, 3));

    var installations = await _github.Gateway.ListInstallationsAsync(Cancellation);

    installations.Count.ShouldBe(103);
    installations[^1].Id.ShouldBe(103);
  }

  [Fact]
  public async Task ListInstallations_Refused_ShouldThrowBadGateway()
  {
    _github.Server.Given(Request.Create().WithPath("/app/installations").UsingGet()).RespondWith(Response.Create().WithStatusCode(404));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationsAsync(Cancellation));
  }

  [Fact]
  public async Task ListInstallationRepositories_ShouldMapEveryPageWithTheTokenOfTheInstallation()
  {
    GivenPage("/installation/repositories", 1, Repositories(1, 100, total: 101));
    GivenPage("/installation/repositories", 2, Repositories(101, 1, total: 101));

    var repositories = await _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation);

    repositories.Count.ShouldBe(101);
    repositories[0].ExternalId.ShouldBe("1");
    repositories[0].Owner.ShouldBe("first-org");
    repositories[0].Namespace.ShouldBeEmpty();
    repositories[0].Name.ShouldBe("repo-1");
    repositories[0].DefaultBranch.ShouldBe("main");
    repositories[0].IsPrivate.ShouldBeTrue();
    repositories[^1].ExternalId.ShouldBe("101");

    var listing = _github.Server.LogEntries.Select(static e => e.RequestMessage!).Where(static r => r.Method == "GET").ToList();
    listing.Count.ShouldBe(2);
    listing.ShouldAllBe(static r => r.Headers!["Authorization"].Single() == $"Bearer {InstallationToken}");
  }

  [Fact]
  public async Task ListInstallationRepositories_None_ShouldReturnEmpty()
  {
    GivenPage("/installation/repositories", 1, """{"total_count":0,"repositories":[]}""");

    (await _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation)).ShouldBeEmpty();
  }

  // A listing that failed must never read as "the installation serves nothing": every repository would be deactivated.
  [Theory]
  [InlineData(404)]
  [InlineData(500)]
  public async Task ListInstallationRepositories_Refused_ShouldThrowBadGateway(int status)
  {
    _github.Server.Given(Request.Create().WithPath("/installation/repositories").UsingGet()).RespondWith(Response.Create().WithStatusCode(status));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation));
  }

  [Fact]
  public async Task ListInstallationRepositories_SecondPageRefused_ShouldThrowInsteadOfReturningAPart()
  {
    GivenPage("/installation/repositories", 1, Repositories(1, 100, total: 150));
    _github.Server
           .Given(Request.Create().WithPath("/installation/repositories").WithParam("page", "2").UsingGet())
           .RespondWith(Response.Create().WithStatusCode(404));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation));
  }

  // The pages are numbered: a repository removed between two readings shifts them, and one is never read.
  [Theory]
  [InlineData(101, 150)]
  [InlineData(101, 100)]
  public async Task ListInstallationRepositories_CountDiffersFromTheTotal_ShouldThrowInsteadOfReturningAPart(int secondPageFrom, int total)
  {
    GivenPage("/installation/repositories", 1, Repositories(1, 100, total));
    GivenPage("/installation/repositories", 2, Repositories(secondPageFrom, 1, total));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation));
  }

  [Fact]
  public async Task ListInstallationRepositories_SameRepositoryOnTwoPages_ShouldThrow()
  {
    GivenPage("/installation/repositories", 1, Repositories(1, 100, total: 101));
    GivenPage("/installation/repositories", 2, Repositories(100, 1, total: 101));

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation));
  }

  [Fact]
  public async Task ListInstallationRepositories_ShouldGiveTheAccountOfEachRepository()
  {
    GivenPage("/installation/repositories", 1, Repositories(1, 1, total: 1));

    (await _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation)).ShouldHaveSingleItem().OwnerExternalId.ShouldBe("1001");
  }

  [Fact]
  public async Task ListInstallationRepositories_UnreadableBody_ShouldThrowBadGateway()
  {
    GivenPage("/installation/repositories", 1, """{"total_count":1}""");

    await Should.ThrowAsync<BadGatewayException>(() => _github.Gateway.ListInstallationRepositoriesAsync(Installation, Cancellation));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public async Task ListInstallationRepositories_InstallationNotPositive_ShouldThrow(long installationId)
  {
    await Should.ThrowAsync<ArgumentOutOfRangeException>(() => _github.Gateway.ListInstallationRepositoriesAsync(installationId, Cancellation));
  }

  private static string Installations(int from, int count)
  {
    var items = Enumerable.Range(from, count)
                          .Select(static i => "{\"id\":" + i.ToString(CultureInfo.InvariantCulture) + ",\"account\":{\"id\":" + (1000 + i).ToString(CultureInfo.InvariantCulture) + ",\"login\":\"org-" + i.ToString(CultureInfo.InvariantCulture) + "\"}}");

    return "[" + string.Join(',', items) + "]";
  }

  private static string Repositories(int from, int count, int total)
  {
    var text = new StringBuilder("{\"total_count\":").Append(total.ToString(CultureInfo.InvariantCulture)).Append(",\"repositories\":[");

    for (var i = from; i < from + count; i++)
    {
      var id = i.ToString(CultureInfo.InvariantCulture);

      text.Append(i == from ? string.Empty : ",")
          .Append("{\"id\":").Append(id)
          .Append(",\"name\":\"repo-").Append(id)
          .Append("\",\"default_branch\":\"main\",\"private\":true,\"owner\":{\"login\":\"first-org\",\"id\":1001}}");
    }

    return text.Append("]}").ToString();
  }

  private void GivenPage(string path, int page, string body) =>
    _github.Server
           .Given(Request.Create().WithPath(path).WithParam("page", page.ToString(CultureInfo.InvariantCulture)).UsingGet())
           .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body));

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}
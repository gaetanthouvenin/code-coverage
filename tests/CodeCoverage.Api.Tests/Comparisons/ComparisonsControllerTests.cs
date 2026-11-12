// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonsControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Comparisons;

// The comparison of two commits, through the ApiToken scheme and the gate; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class ComparisonsControllerTests
{
  #region Fields

  private const string Base = "/api/v1/repos/github/octo-org/app";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha HeadSha = GitSha.Parse(new string('a', GitSha.Length));

  private static readonly GitSha BaseSha = GitSha.Parse(new string('b', GitSha.Length));

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  #endregion

  #region Ctors

  public ComparisonsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var head = Commit.Create(5, HeadSha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 9L);
    var comparedTo = Commit.Create(5, BaseSha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(comparedTo, 8L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _commits.FindByShaAsync(5, BaseSha, A<CancellationToken>._)).Returns(comparedTo);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_StoredPair_ShouldReturnTheComparison()
  {
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._))
     .Returns([new CommitComparison(9, null, null, BaseSha, 0, 0, DiffSource.Compare, Result(), Now)]);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/compare?base={BaseSha.Value}&head={HeadSha.Value}", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("pullNumber").ValueKind.ShouldBe(JsonValueKind.Null);
    body.RootElement.GetProperty("isStale").GetBoolean().ShouldBeFalse();
    body.RootElement.GetProperty("bases").GetProperty("isExact").GetBoolean().ShouldBeFalse();
    body.RootElement.GetProperty("diffIncomplete").GetBoolean().ShouldBeFalse();
  }

  [Fact]
  public async Task Get_PairNotStored_ShouldReturn404NotComputed()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/compare?base={BaseSha.Value}&head={HeadSha.Value}", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Comparison.NotComputed");
  }

  // Short SHAs wait for M3.33.
  [Theory]
  [InlineData("?head=aaaaaaa")]
  [InlineData("?base=bbbbbbb&head=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
  [InlineData("?base=cccccccccccccccccccccccccccccccccccccccc&head=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
  public async Task Get_MissingShortOrUnknownSha_ShouldReturn404CommitNotFound(string query)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/compare{query}", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Commit.NotFound");
  }

  private static Comparison Result() =>
    new(new CoverageTotals(1, 1, 0, 0, 0), new CoverageTotals(1, 0, 0, 0, 0), PatchCoverage.Empty, [], [], [], DiffIncomplete: false);

  private static async Task<JsonDocument> JsonAsync(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _commits);
                                  s.AddScoped(_ => _comparisons);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

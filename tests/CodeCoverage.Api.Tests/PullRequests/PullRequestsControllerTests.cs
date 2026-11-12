// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequestsControllerTests.cs" company="Gaëtan Thouvenin">
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
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Diffs;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.PullRequests;

// The pull requests and their comparisons, through the ApiToken scheme and the gate; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class PullRequestsControllerTests
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

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly IComparisonRepository _comparisons = A.Fake<IComparisonRepository>();

  #endregion

  #region Ctors

  public PullRequestsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var head = Commit.Create(5, HeadSha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 9L);
    var pull = PullRequest.Create(5, new ForgePullInfo(12, ForgePullState.Open, "Add a feature", "octocat", HeadSha, "feature", "main", BaseSha), Now);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _commits.FindByShaAsync(5, HeadSha, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _commits.ListByShasAsync(5, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._)).Returns([head]);
    A.CallTo(() => _pullRequests.FindByNumberAsync(5, 12, A<CancellationToken>._)).Returns(pull);
    A.CallTo(() => _pullRequests.ListPageAsync(5, A<PullRequestState?>._, A<int>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily((long _, PullRequestState? _, int page, int size, CancellationToken _) => new PullRequestPage([pull], page, size, 1));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_List_ShouldReturnThePageWithTheComparisonOfEachHead()
  {
    A.CallTo(() => _comparisons.ListByCommitsAsync(A<IReadOnlyCollection<long>>._, A<CancellationToken>._)).Returns([Stored()]);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls?state=all&page=2&pageSize=10", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("page").GetInt32().ShouldBe(2);
    body.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
    var item = body.RootElement.GetProperty("items")[0];
    item.GetProperty("number").GetInt32().ShouldBe(12);
    item.GetProperty("state").GetString().ShouldBe("Open");
    item.GetProperty("author").GetString().ShouldBe("octocat");
    item.GetProperty("headCoverage").GetDecimal().ShouldBe(75m);
    item.GetProperty("patchCoverage").GetDecimal().ShouldBe(50m);
    item.GetProperty("delta").GetDecimal().ShouldBe(25m);
    A.CallTo(() => _pullRequests.ListPageAsync(5, null, 2, 10, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Get_ListWithoutState_ShouldListTheOpenOnes()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    A.CallTo(() => _pullRequests.ListPageAsync(5, PullRequestState.Open, 1, 25, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("state=draft")]
  [InlineData("pageSize=101")]
  [InlineData("page=21474838&pageSize=100")]
  public async Task Get_ListInvalidQuery_ShouldReturn400(string query)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls?{query}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  // The acceptance of M3.10: the comparison says when the diff of the forge was incomplete.
  [Fact]
  public async Task Get_Comparison_ShouldExposeDiffIncompleteAndTheBases()
  {
    A.CallTo(() => _comparisons.FindAsync(9, 12, A<CancellationToken>._)).Returns(Stored());
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls/12", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var root = body.RootElement;
    root.GetProperty("diffIncomplete").GetBoolean().ShouldBeTrue();
    root.GetProperty("isStale").GetBoolean().ShouldBeTrue();
    root.GetProperty("pullNumber").GetInt32().ShouldBe(12);
    root.GetProperty("bases").GetProperty("headSha").GetString().ShouldBe(HeadSha.Value);
    root.GetProperty("bases").GetProperty("comparedToSha").GetString().ShouldBe(BaseSha.Value);
    root.GetProperty("bases").GetProperty("isExact").GetBoolean().ShouldBeTrue();
    root.GetProperty("delta").GetDecimal().ShouldBe(25m);
    var file = root.GetProperty("patch").GetProperty("files")[0];
    file.GetProperty("path").GetString().ShouldBe("src/a.cs");
    file.GetProperty("totals").GetProperty("misses").GetInt32().ShouldBe(1);
    root.GetProperty("diffSource").GetString().ShouldBe("PullFiles");
  }

  [Fact]
  public async Task Get_ComparisonNotComputed_ShouldReturn404WithItsCode()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls/12", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Comparison.NotComputed");
  }

  [Fact]
  public async Task Get_UnknownPullRequest_ShouldReturn404WithItsCode()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls/13", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("PullRequest.NotFound");
  }

  [Fact]
  public async Task Get_UnreadableRepository_ShouldReturn404()
  {
    A.CallTo(() => _access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Repository.NotFound");
  }

  [Fact]
  public async Task Get_WithoutToken_ShouldReturn401()
  {
    using var client = _factory.CreateClient();

    using var response = await client.GetAsync(new Uri($"{Base}/pulls", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  // Head revision 3 against a head commit at 0: the report changed since the comparison.
  private static CommitComparison Stored() =>
    new(
      9,
      12,
      BaseSha,
      BaseSha,
      3,
      2,
      DiffSource.PullFiles,
      new Comparison(
        new CoverageTotals(3, 1, 0, 0, 0),
        new CoverageTotals(1, 1, 0, 0, 0),
        new PatchCoverage(new CoverageTotals(1, 1, 0, 0, 0), [new PatchFileCoverage("src/a.cs", new CoverageTotals(1, 1, 0, 0, 0), false)]),
        [],
        [],
        [],
        DiffIncomplete: true
      ),
      Now
    );

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
                                  s.AddScoped(_ => _pullRequests);
                                  s.AddScoped(_ => _comparisons);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

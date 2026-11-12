// ------------------------------------------------------------------------------------------------
// <copyright file="CommitsControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Commits;

// The read actions of the commits, through the ApiToken scheme and the gate; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class CommitsControllerTests
{
  #region Fields

  private const string Base = "/api/v1/repos/github/octo-org/app";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IPostedStatusRepository _statuses = A.Fake<IPostedStatusRepository>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public CommitsControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    _commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);
    _commit.MarkPending("main", null, Now);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _commits.ListPageAsync(5, A<string?>._, A<int>._, A<int>._, A<CancellationToken>._))
     .ReturnsLazily((long _, string? _, int page, int size, CancellationToken _) => new CommitPage([_commit], page, size, 1));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_List_ShouldReturnThePageEnvelope()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits?branch=main&page=2&pageSize=10", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("page").GetInt32().ShouldBe(2);
    body.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(10);
    body.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(1);
    var item = body.RootElement.GetProperty("items")[0];
    item.GetProperty("sha").GetString().ShouldBe(Sha.Value);
    item.GetProperty("sessions").ValueKind.ShouldBe(JsonValueKind.Null);
    A.CallTo(() => _commits.ListPageAsync(5, "main", 2, 10, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("page=0")]
  [InlineData("pageSize=101")]
  public async Task Get_ListOutOfBounds_ShouldReturn400(string query)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits?{query}", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_Commit_ShouldReturnItsSessionsFlagsAndStatuses()
  {
    A.CallTo(() => _reports.ListSessionsAsync(9, A<CancellationToken>._)).Returns([Session.FromUpload(9, 1, 77, ["unit"], "job", "github-actions", null, default)]);
    A.CallTo(() => _reports.ListFlagTotalsAsync(9, A<CancellationToken>._)).Returns([]);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits/{Sha.Value}", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("state").GetString().ShouldBe("Pending");
    body.RootElement.GetProperty("sessions")[0].GetProperty("flags")[0].GetString().ShouldBe("unit");
    body.RootElement.GetProperty("flags").GetArrayLength().ShouldBe(0);
    body.RootElement.GetProperty("statuses").GetArrayLength().ShouldBe(0);
  }

  [Fact]
  public async Task Get_Files_ShouldReadTheIndex()
  {
    A.CallTo(() => _reports.GetFileIndexAsync(9, A<CancellationToken>._)).Returns([1]);
    A.CallTo(() => _codec.DecodeFileIndex(A<ReadOnlyMemory<byte>>._)).Returns([new FileTotals("src/a.cs", default)]);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits/{Sha.Value}/files", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement[0].GetProperty("path").GetString().ShouldBe("src/a.cs");
  }

  [Fact]
  public async Task Get_FileCoverage_ShouldReturnItsLines()
  {
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns([1]);
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._))
     .Returns(CoverageReport.Create([FileCoverage.Create("src/deep/a.cs", [new LineCoverage(4, 0, 1, 2, false, null)])]));
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits/{Sha.Value}/files/src/deep/a.cs", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var line = body.RootElement.GetProperty("lines")[0];
    line.GetProperty("line").GetInt32().ShouldBe(4);
    line.GetProperty("state").GetString().ShouldBe("Partial");
    line.GetProperty("branches").GetString().ShouldBe("1/2");
  }

  // The acceptance of M3.04, through the API: a repository the token cannot read answers as a missing one.
  [Fact]
  public async Task Get_UnreadableRepository_ShouldReturn404()
  {
    A.CallTo(() => _access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"{Base}/commits/{Sha.Value}", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    body.RootElement.GetProperty("errorCode").GetString().ShouldBe("Repository.NotFound");
  }

  [Theory]
  [InlineData("/commits/not-a-sha")]
  [InlineData("/commits/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
  public async Task Get_UnknownCommit_ShouldReturn404(string path)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Base + path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_WithoutToken_ShouldReturn401()
  {
    using var client = _factory.CreateClient();

    using var response = await client.GetAsync(new Uri($"{Base}/commits", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

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
                                  s.AddScoped(_ => _reports);
                                  s.AddScoped(_ => _codec);
                                  s.AddScoped(_ => _uploads);
                                  s.AddScoped(_ => _statuses);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

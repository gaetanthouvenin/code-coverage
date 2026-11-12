// ------------------------------------------------------------------------------------------------
// <copyright file="HistoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.History;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Repositories;

// GET …/history through the ApiToken scheme and the gate; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class HistoryTests
{
  #region Fields

  private const string Address = "/api/v1/repos/github/octo-org/app/history";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  #endregion

  #region Ctors

  public HistoryTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _reports.ListHistoryAsync(A<long>._, "main", A<IReadOnlyCollection<string>>._, A<DateTimeOffset>._, A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
     .Returns([new HistoryEntry(9, GitSha.Parse(new string('a', GitSha.Length)), "unit", Now.AddDays(-1), null, 10, 8, 2, 0, 80m)]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_RepeatedFlags_ShouldReturnOneSeriesEach()
  {
    using var client = Client();

    using var response = await client.GetAsync(
      new Uri($"{Address}?flag=unit&flag=e2e&from=2026-11-01T00:00:00Z&to=2026-11-12T08:00:00Z&interval=day", UriKind.Relative),
      Ct
    );
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var series = body.RootElement.EnumerateArray().ToList();
    series.Select(static s => s.GetProperty("flag").GetString()).ShouldBe(["unit", "e2e"]);
    var point = series[0].GetProperty("points").EnumerateArray().ShouldHaveSingleItem();
    point.GetProperty("at").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero));
    point.GetProperty("coverage").GetDecimal().ShouldBe(80m);
    series[1].GetProperty("points").GetArrayLength().ShouldBe(0);
  }

  [Fact]
  public async Task Get_WithoutFlag_ShouldReturnTheProjectWithANullFlag()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Address, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.EnumerateArray().ShouldHaveSingleItem().GetProperty("flag").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  [Theory]
  [InlineData("?interval=month")]
  [InlineData("?from=2026-11-12T00:00:00Z&to=2026-11-01T00:00:00Z")]
  public async Task Get_InvalidQuery_ShouldReturn400(string query)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Address + query, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _reports);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

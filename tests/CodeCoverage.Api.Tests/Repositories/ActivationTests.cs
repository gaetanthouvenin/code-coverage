// ------------------------------------------------------------------------------------------------
// <copyright file="ActivationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Repositories;

// Activate and deactivate through the API (M3.07); the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class ActivationTests
{
  #region Fields

  private const string Base = "/api/v1/repos/github/octo-org/app";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly TrackedRepository _repository;

  private readonly TrackedRepository _disabled;

  #endregion

  #region Ctors

  public ActivationTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;
    _repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, 5L);
    _disabled = TrackedRepository.Create(1, "2003", RepositorySlug.Parse("octo-org/old"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_disabled, 6L);
    _disabled.Disable(Now);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _roles.ResolveAsync(ApiTokens.UserId, 3, A<CancellationToken>._)).Returns(OrganizationRole.Participant);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _repositories.SearchActiveAsync(A<string?>._, A<CancellationToken>._)).Returns([_disabled, _repository]);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _access.ListReadableAsync(ApiTokens.UserId, A<CancellationToken>._)).Returns(new HashSet<string>(["2002", "2003"], StringComparer.Ordinal));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [InlineData("activate")]
  [InlineData("deactivate")]
  public async Task Post_NonAdmin_ShouldReturn403(string action)
  {
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/{action}", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Post_Deactivate_AsRepoAdmin_ShouldReturn204AndAudit()
  {
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Admin, A<CancellationToken>._)).Returns(true);
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/deactivate", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    _repository.DisabledAt.ShouldNotBeNull();
    A.CallTo(() => _auditLog.RecordAsync("Repositories.Deactivate", "5", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // Decision 15: a departed row keeps answering 404, the single refusal of ADR-013.
  [Fact]
  public async Task Post_Activate_DepartedRepository_ShouldReturn404()
  {
    _repository.Deactivate(Now);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Admin, A<CancellationToken>._)).Returns(true);
    using var client = Client();

    using var response = await client.PostAsync(new Uri($"{Base}/activate", UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  // Decision 15: a disabled repository stays listed and addressable, with its state and the filter of Codecov API v2.
  [Theory]
  [InlineData("", new[] { "old", "app" })]
  [InlineData("?active=true", new[] { "app" })]
  [InlineData("?active=false", new[] { "old" })]
  public async Task Get_List_ShouldGiveTheStateAndFilterOnIt(string query, string[] expected)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri($"/api/v1/orgs/github/octo-org/repos{query}", UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var items = body.RootElement.EnumerateArray().ToList();
    items.Select(static i => i.GetProperty("name").GetString()).ShouldBe(expected);
    items.ShouldAllBe(i => i.GetProperty("active").GetBoolean() == (i.GetProperty("name").GetString() == "app"));
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

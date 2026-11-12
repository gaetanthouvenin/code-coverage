// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoriesControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Repositories;

// The read actions of the repositories, through the ApiToken scheme and the gate; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class RepositoriesControllerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly IRepositoryFlagRepository _flags = A.Fake<IRepositoryFlagRepository>();

  private readonly TrackedRepository _repository;

  #endregion

  #region Ctors

  public RepositoriesControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;
    _repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, 5L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _roles.ResolveAsync(ApiTokens.UserId, 3, A<CancellationToken>._)).Returns(OrganizationRole.Participant);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _repositories.SearchActiveAsync(A<string?>._, A<CancellationToken>._)).Returns([_repository]);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _access.ListReadableAsync(ApiTokens.UserId, A<CancellationToken>._)).Returns(new HashSet<string>(["2002"], StringComparer.Ordinal));
    A.CallTo(() => _branches.ListAsync(5, A<CancellationToken>._)).Returns([Branch.Create(5, "main", GitSha.Parse(new string('c', GitSha.Length)), Now)]);
    A.CallTo(() => _flags.ListAsync(5, A<CancellationToken>._)).Returns(["unit"]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_List_ShouldReturnTheReadableRepositories()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/api/v1/orgs/github/octo-org/repos?search=AP", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var item = body.RootElement.EnumerateArray().ShouldHaveSingleItem();
    item.GetProperty("forge").GetString().ShouldBe("github");
    item.GetProperty("owner").GetString().ShouldBe("octo-org");
    item.GetProperty("name").GetString().ShouldBe("app");
    A.CallTo(() => _repositories.SearchActiveAsync("AP", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // An organization the user takes no part in is unknown.
  [Fact]
  public async Task Get_ListOfAnOrganizationWithoutRole_ShouldReturn404()
  {
    A.CallTo(() => _roles.ResolveAsync(A<long>._, A<long>._, A<CancellationToken>._)).Returns((OrganizationRole?)null);
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/api/v1/orgs/github/octo-org/repos", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_Repository_ShouldReturnItsBranchesAndFlags()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/api/v1/repos/github/octo-org/app", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("repository").GetProperty("defaultBranch").GetString().ShouldBe("main");
    body.RootElement.GetProperty("branches")[0].GetProperty("name").GetString().ShouldBe("main");
    body.RootElement.GetProperty("flags")[0].GetString().ShouldBe("unit");
    body.RootElement.GetProperty("totals").GetArrayLength().ShouldBe(0);
  }

  [Fact]
  public async Task Get_Branches_ShouldReturnThem()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/api/v1/repos/github/octo-org/app/branches", UriKind.Relative), Ct);
    using var body = await JsonAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement[0].GetProperty("headSha").GetString().ShouldBe(new string('c', GitSha.Length));
  }

  [Theory]
  [InlineData("/api/v1/repos/github/octo-org/app")]
  [InlineData("/api/v1/repos/github/octo-org/app/branches")]
  public async Task Get_UnreadableRepository_ShouldReturn404(string path)
  {
    A.CallTo(() => _access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    using var client = Client();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_WithoutToken_ShouldReturn401()
  {
    using var client = _factory.CreateClient();

    using var response = await client.GetAsync(new Uri("/api/v1/repos/github/octo-org/app", UriKind.Relative), Ct);

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
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _branches);
                                  s.AddScoped(_ => _flags);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

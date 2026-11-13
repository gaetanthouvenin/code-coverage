// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeTokenEndpointsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Repositories;

// The badge token through the API: an administrator of the repository; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class BadgeTokenEndpointsTests
{
  #region Fields

  private const string Address = "/api/v1/repos/github/octo-org/app/badge-token";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  // The gate of a repository action also reads the role in the organization: here, none.
  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Ctors

  public BadgeTokenEndpointsTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _repositories.FindAsync(5, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_NonRepoAdmin_ShouldReturn403()
  {
    using var client = Client();

    using var response = await client.PostAsync(new Uri(Address, UriKind.Relative), null, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Post_RepoAdmin_ShouldReturnTheNewTokenOnce()
  {
    AdministerTheRepository();
    using var client = Client();

    using var response = await client.PostAsync(new Uri(Address, UriKind.Relative), null, Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("value").GetString().ShouldNotBeNullOrEmpty();
  }

  [Fact]
  public async Task Put_Import_ShouldReturn204()
  {
    AdministerTheRepository();
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri(Address, UriKind.Relative), new { value = "AbC-123_xyz" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task Put_InvalidToken_ShouldReturn400()
  {
    AdministerTheRepository();
    using var client = Client();

    using var response = await client.PutAsJsonAsync(new Uri(Address, UriKind.Relative), new { value = "not a token!" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  private void AdministerTheRepository() =>
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Admin, A<CancellationToken>._)).Returns(true);

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

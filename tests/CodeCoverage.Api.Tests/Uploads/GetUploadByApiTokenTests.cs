// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadByApiTokenTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Uploads;

// GET …/uploads/{id} with a token of the API (M3.09): the reader of the repository, through the gate.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class GetUploadByApiTokenTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly Upload _upload = Upload.Receive(9, 1, "key", ["back"], "run", null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now);

  #endregion

  #region Ctors

  public GetUploadByApiTokenTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now));
    A.CallTo(() => _uploads.FindByPublicIdAsync(_upload.PublicId, A<CancellationToken>._)).Returns(_upload);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_WithApiTokenOfAReader_ShouldReturnTheUpload()
  {
    using var client = Client().WithApiToken();

    using var response = await client.GetAsync(new Uri(Path(_upload.PublicId), UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("id").GetGuid().ShouldBe(_upload.PublicId);
    A.CallTo(() => _cache.Find(A<byte[]>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Get_WithApiTokenOfAnUnreadableRepository_ShouldReturn404()
  {
    A.CallTo(() => _access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    using var client = Client().WithApiToken();

    using var response = await client.GetAsync(new Uri(Path(_upload.PublicId), UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  // Decision 11: a revoked token of the API fails closed, never tried as an upload credential.
  [Fact]
  public async Task Get_WithRevokedApiToken_ShouldChallengeWithoutTryingTheUploadCredential()
  {
    using var client = Client().WithApiToken(ApiTokens.RevokedValue);

    using var response = await client.GetAsync(new Uri(Path(_upload.PublicId), UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    response.Headers.WwwAuthenticate.ShouldContain(static h => h.Scheme == "Bearer");
    A.CallTo(() => _cache.Find(A<byte[]>._)).MustNotHaveHappened();
    A.CallTo(() => _uploads.FindByPublicIdAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static string Path(Guid id) => $"{CodeCoverageApi.Prefix}/repos/github/octo-org/app/uploads/{id}";

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _commits);
                                  s.AddScoped(_ => _uploads);
                                  s.AddSingleton(_cache);
                                }))
            .CreateClient();

  #endregion
}

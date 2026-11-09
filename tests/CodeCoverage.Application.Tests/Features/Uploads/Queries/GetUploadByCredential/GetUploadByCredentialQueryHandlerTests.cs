// ------------------------------------------------------------------------------------------------
// <copyright file="GetUploadByCredentialQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Features.Uploads.Queries.GetUploadByCredential;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Uploads.Queries.GetUploadByCredential;

public sealed class GetUploadByCredentialQueryHandlerTests
{
  #region Fields

  private const string Value = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private static readonly Guid UploadId = Guid.NewGuid();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ITokenHasher _hasher = A.Fake<ITokenHasher>();

  private readonly IClock _clock = A.Fake<IClock>();

  private readonly IUploadRepository _uploads = A.Fake<IUploadRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly Upload _upload = Upload.Receive(9, 1, "key", [], null, null, null, null, null, null, null, 3, PrincipalKind.OrganizationToken, Now, UploadId);

  #endregion

  #region Ctors

  public GetUploadByCredentialQueryHandlerTests()
  {
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _hasher.Hash(A<string>._)).Returns(Hash);
    A.CallTo(() => _cache.Find(Hash)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));
    A.CallTo(() => _uploads.FindByPublicIdAsync(UploadId, A<CancellationToken>._)).Returns(_upload);
    Tracked(5);
    CommitOf(5);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_CredentialOfTheRepository_ShouldReturnTheUpload()
  {
    (await Handler().Handle(Query(), Ct)).ShouldBeSameAs(_upload);
  }

  [Fact]
  public async Task Handle_CredentialForOtherRepo_ShouldThrowNotFound()
  {
    A.CallTo(() => _cache.Find(Hash)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Repository, 6, null));

    (await ShouldBeNotFound()).ErrorCode.ShouldBe(UploadErrors.NotFound);
  }

  [Fact]
  public async Task Handle_UploadOfOtherRepository_ShouldThrowNotFound()
  {
    CommitOf(8);

    await ShouldBeNotFound();
  }

  [Fact]
  public async Task Handle_UnknownUpload_ShouldThrowNotFound()
  {
    A.CallTo(() => _uploads.FindByPublicIdAsync(UploadId, A<CancellationToken>._)).Returns((Upload?)null);

    await ShouldBeNotFound();
  }

  [Fact]
  public async Task Handle_InvalidCredential_ShouldStayUnauthorized()
  {
    A.CallTo(() => _cache.Find(Hash)).Returns(null);
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns((UploadToken?)null);

    await Should.ThrowAsync<UnauthorizedException>(async () => await Handler().Handle(Query(), Ct));
  }

  private static GetUploadByCredentialQuery Query() => new(new UploadCredential(UploadCredentialKind.UploadToken, Value), Forge.GitHub, "octo-org", "app", UploadId);

  private async Task<NotFoundException> ShouldBeNotFound() => await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Query(), Ct));

  private GetUploadByCredentialQueryHandler Handler()
  {
    var authenticator = new UploadAuthenticator(_organization, A.Fake<IOrganizationDirectory>(), A.Fake<IOidcTokenValidator>(), _tokens, _cache, _repositories, A.Fake<IForgeGatewayProvider>(), _hasher, _clock);

    return new GetUploadByCredentialQueryHandler(authenticator, _uploads, _commits);
  }

  private void Tracked(long id)
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
  }

  private void CommitOf(long repositoryId) =>
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(Commit.Create(repositoryId, GitSha.Parse(new string('a', GitSha.Length)), Now));

  #endregion
}

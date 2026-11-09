// ------------------------------------------------------------------------------------------------
// <copyright file="UploadAuthenticatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Uploads;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.Identity;
using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Domain.Uploads;

namespace CodeCoverage.Application.Tests.Services;

public sealed class UploadAuthenticatorTests
{
  #region Fields

  private const string OwnerId = "1001";

  private const string RepositoryExternalId = "2002";

  private const string TokenValue = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string ImportedValue = "5b8c4f8a-1111-4222-8333-444455556666";

  private const string JwtValue = "header.payload.signature";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private static readonly RepositorySlug Slug = RepositorySlug.Parse("octo-org/app");

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IOidcTokenValidator _oidc = A.Fake<IOidcTokenValidator>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly IUploadCredentialCache _cache = A.Fake<IUploadCredentialCache>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly ITokenHasher _hasher = A.Fake<ITokenHasher>();

  private readonly IClock _clock = A.Fake<IClock>();

  #endregion

  #region Ctors

  public UploadAuthenticatorTests()
  {
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _clock.UtcNow).Returns(Now);
    A.CallTo(() => _hasher.Hash(A<string>._)).Returns(Hash);
    A.CallTo(() => _gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _cache.Find(A<byte[]>._)).Returns(null);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, OwnerId, "octo-org", OrganizationAccountType.Organization, Now));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Authenticate_ValidOrganizationToken_ShouldPassAndRemember()
  {
    Tracked(5);
    Known(OrganizationToken());

    var authorization = await Authenticate(Token());

    authorization.PrincipalKind.ShouldBe(PrincipalKind.OrganizationToken);
    authorization.RepositoryId.ShouldBe(5);
    A.CallTo(() => _cache.Store(Hash, A<UploadTokenIdentity>.That.Matches(i => i.Scope == UploadTokenScope.Organization))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tokens.RecordUseAsync(A<long>._, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Authenticate_RememberedToken_ShouldNotReadTheDatabase()
  {
    Tracked(5);
    A.CallTo(() => _cache.Find(Hash)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, null));

    await Authenticate(Token());

    A.CallTo(() => _tokens.FindByHashAsync(A<byte[]>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _tokens.RecordUseAsync(A<long>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // The filter of the organization hides the row: the token of another organization is an unknown one.
  [Fact]
  public async Task Authenticate_TokenOfOtherOrganization_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns((UploadToken?)null);

    await ShouldRefuseAsInvalid(Token());
  }

  [Fact]
  public async Task Authenticate_TokenAgainstUnknownOwner_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);

    await ShouldRefuseAsInvalid(Token());

    A.CallTo(() => _tokens.FindByHashAsync(A<byte[]>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Authenticate_RevokedToken_ShouldThrowUnauthorized()
  {
    var token = OrganizationToken();
    token.Revoke(Now);
    Known(token);

    await ShouldRefuseAsInvalid(Token());

    A.CallTo(() => _cache.Store(A<byte[]>._, A<UploadTokenIdentity>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Authenticate_ExpiredToken_ShouldThrowUnauthorized()
  {
    Known(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", Hash, false, "octocat", Now.AddDays(-2), Now.AddDays(-1)));

    await ShouldRefuseAsInvalid(Token());

    // An expired token counts as absent (ARCHITECTURE §5.1, step 4): it is neither used nor remembered.
    A.CallTo(() => _tokens.RecordUseAsync(A<long>._, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _cache.Store(A<byte[]>._, A<UploadTokenIdentity>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Authenticate_RememberedTokenThatExpired_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _cache.Find(Hash)).Returns(new UploadTokenIdentity(3, UploadTokenScope.Organization, null, Now));

    await ShouldRefuseAsInvalid(Token());
  }

  [Fact]
  public async Task Authenticate_TokenOfOtherRepository_ShouldThrowForbidden()
  {
    Tracked(5);
    Known(RepositoryToken(6));

    var exception = await Should.ThrowAsync<ForbiddenException>(() => Authenticate(Token()));

    exception.ErrorCode.ShouldBe(UploadErrors.TokenNotForRepository);
  }

  [Fact]
  public async Task Authenticate_RepositoryTokenOnItsRepository_ShouldPass()
  {
    Tracked(5);
    Known(RepositoryToken(5));

    (await Authenticate(Token())).PrincipalKind.ShouldBe(PrincipalKind.RepositoryToken);
  }

  [Fact]
  public async Task Authenticate_ImportedCodecovToken_ShouldPass()
  {
    Known(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "6666", Hash, true, "octocat", Now, null));

    var authorization = await Authenticate(new UploadCredential(UploadCredentialKind.UploadToken, ImportedValue));

    authorization.PrincipalKind.ShouldBe(PrincipalKind.OrganizationToken);
    A.CallTo(() => _hasher.Hash(ImportedValue)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Authenticate_OidcOnTrackedRepository_ShouldPassWithoutAskingTheForge()
  {
    Tracked(5);
    Proves(RepositoryExternalId, OwnerId);

    var authorization = await Authenticate(Jwt());

    authorization.PrincipalKind.ShouldBe(PrincipalKind.GitHubOidc);
    authorization.TokenId.ShouldBeNull();
    A.CallTo(() => _gateways.GetGateway(A<Forge>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Authenticate_OidcOnUnknownRepository_ShouldCompareWithTheForge()
  {
    Proves(RepositoryExternalId, OwnerId);
    ForgeReports(new ForgeConnectionInfo("77", OwnerId, RepositoryExternalId));

    var authorization = await Authenticate(Jwt());

    authorization.RepositoryId.ShouldBeNull();
    authorization.Connection.ShouldNotBeNull().ConnectionId.ShouldBe("77");
  }

  [Fact]
  public async Task Authenticate_OidcRepositoryIdMismatch_ShouldThrowForbidden()
  {
    Tracked(5);
    Proves("9999", OwnerId);

    var exception = await Should.ThrowAsync<ForbiddenException>(() => Authenticate(Jwt()));

    exception.ErrorCode.ShouldBe(UploadErrors.OidcMismatch);
  }

  [Fact]
  public async Task Authenticate_OidcAccountMismatch_ShouldThrowForbidden()
  {
    Tracked(5);
    Proves(RepositoryExternalId, "8888");

    var exception = await Should.ThrowAsync<ForbiddenException>(() => Authenticate(Jwt()));

    exception.ErrorCode.ShouldBe(UploadErrors.OidcMismatch);
  }

  [Fact]
  public async Task Authenticate_OidcForAnotherSlug_ShouldThrowForbiddenBeforeTheState()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);
    Proves(RepositoryExternalId, OwnerId, "octo-org/other");

    var exception = await Should.ThrowAsync<ForbiddenException>(() => Authenticate(Jwt()));

    exception.ErrorCode.ShouldBe(UploadErrors.OidcMismatch);
  }

  // The token names octo-org/app; octo-org/anything/app is another repository, although its owner and name match.
  [Fact]
  public async Task Authenticate_OidcForASlugWithANamespace_ShouldThrowForbidden()
  {
    Proves(RepositoryExternalId, OwnerId);
    ForgeReports(new ForgeConnectionInfo("77", OwnerId, RepositoryExternalId));

    var exception = await Should.ThrowAsync<ForbiddenException>(
      () => Handler().AuthenticateAsync(Jwt(), Forge.GitHub, RepositorySlug.Parse("octo-org/anything/app"), Ct)
    );

    exception.ErrorCode.ShouldBe(UploadErrors.OidcMismatch);
  }

  [Fact]
  public async Task Authenticate_OidcAgainstUnknownOwner_ShouldThrowNotFound()
  {
    A.CallTo(() => _organization.IsKnown).Returns(false);
    Proves(RepositoryExternalId, OwnerId);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Authenticate(Jwt()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task Authenticate_OidcOnRepositoryNoConnectionServes_ShouldThrowNotFound()
  {
    Proves(RepositoryExternalId, OwnerId);
    ForgeReports(null);

    var exception = await Should.ThrowAsync<NotFoundException>(() => Authenticate(Jwt()));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task Authenticate_InvalidOidcToken_ShouldThrowUnauthorized()
  {
    A.CallTo(() => _oidc.ValidateAsync(JwtValue, A<CancellationToken>._)).Returns((GitHubOidcIdentity?)null);

    await ShouldRefuseAsInvalid(Jwt());
  }

  // Its account identifier could exist on another forge, where it would prove nothing.
  [Fact]
  public async Task Authenticate_OidcOnOtherForge_ShouldThrowUnauthorized()
  {
    Proves(RepositoryExternalId, OwnerId);

    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Handler().AuthenticateAsync(Jwt(), Forge.GitLab, Slug, Ct));

    exception.ErrorCode.ShouldBe(UploadErrors.TokenInvalid);
    A.CallTo(() => _oidc.ValidateAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static UploadCredential Token() => new(UploadCredentialKind.UploadToken, TokenValue);

  private static UploadCredential Jwt() => new(UploadCredentialKind.Jwt, JwtValue);

  private static UploadToken OrganizationToken() => UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", Hash, false, "octocat", Now, null);

  private static UploadToken RepositoryToken(long repositoryId) =>
    UploadToken.Create(UploadTokenScope.Repository, repositoryId, "ci", "hint", Hash, false, "octocat", Now, null);

  private UploadAuthenticator Handler() => new(_organization, _directory, _oidc, _tokens, _cache, _repositories, _gateways, _hasher, _clock);

  private Task<UploadAuthorization> Authenticate(UploadCredential credential) => Handler().AuthenticateAsync(credential, Forge.GitHub, Slug, Ct);

  private async Task ShouldRefuseAsInvalid(UploadCredential credential)
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(() => Authenticate(credential));

    exception.ErrorCode.ShouldBe(UploadErrors.TokenInvalid);
  }

  private void Known(UploadToken token) => A.CallTo(() => _tokens.FindByHashAsync(Hash, A<CancellationToken>._)).Returns(token);

  private void Proves(string repositoryId, string ownerId, string repository = "octo-org/app") =>
    A.CallTo(() => _oidc.ValidateAsync(JwtValue, A<CancellationToken>._))
     .Returns(new GitHubOidcIdentity(repository, repositoryId, "octo-org", ownerId, null, null));

  private void ForgeReports(ForgeConnectionInfo? connection) =>
    A.CallTo(() => _gateway.GetRepositoryConnectionAsync("octo-org", "app", A<CancellationToken>._)).Returns(connection);

  private void Tracked(long id)
  {
    var repository = TrackedRepository.Create(1, RepositoryExternalId, Slug, "main", true, "protected", Now);

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, id);
    A.CallTo(() => _repositories.FindBySlugAsync(Slug, A<CancellationToken>._)).Returns(repository);
  }

  #endregion
}

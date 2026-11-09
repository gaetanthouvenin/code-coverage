// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeRepositoryUploadTokenCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.UploadTokens;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Tokens;

using static CodeCoverage.Application.Tests.Features.UploadTokens.UploadTokenFixture;

namespace CodeCoverage.Application.Tests.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;

// A token is only revoked through the owner it belongs to: another owner gets the answer of an unknown token.
public sealed class RevokeRepositoryUploadTokenCommandHandlerTests
{
  #region Fields

  private readonly UploadTokenFixture _fixture = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_UnknownRepository_ShouldThrowNotFound()
  {
    A.CallTo(() => _fixture.Repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task Handle_MalformedOwner_ShouldThrowNotFound()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(
      async () => await Handler().Handle(new RevokeRepositoryUploadTokenCommand(Forge.GitHub, "/", "repo", 1), Ct)
    );

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task Handle_TokenOfTheOrganization_ShouldThrowNotFound()
  {
    SetRepository();
    var token = UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", Hash, false, "octocat", Now, null);
    A.CallTo(() => _fixture.Tokens.FindAsync(1, A<CancellationToken>._)).Returns(token);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(UploadTokenErrors.NotFound);
    token.RevokedAt.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_UnknownToken_ShouldThrowNotFound()
  {
    SetRepository();
    A.CallTo(() => _fixture.Tokens.FindAsync(1, A<CancellationToken>._)).Returns((UploadToken?)null);

    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));
  }

  private static RevokeRepositoryUploadTokenCommand Command() => new(Forge.GitHub, "acme", "repo", 1);

  private void SetRepository()
  {
    var repository = TrackedRepository.Create(101, "1", RepositorySlug.Parse("acme/repo"), "main", false, "protected-token", Now);

    A.CallTo(() => _fixture.Repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
  }

  private RevokeRepositoryUploadTokenCommandHandler Handler() => new(_fixture.Service());

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="RevokeOrganizationUploadTokenCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

using static CodeCoverage.Application.Tests.Features.UploadTokens.UploadTokenFixture;

namespace CodeCoverage.Application.Tests.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;

public sealed class RevokeOrganizationUploadTokenCommandHandlerTests
{
  #region Fields

  private readonly UploadTokenFixture _fixture = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldRevokeSaveAndAudit()
  {
    var token = Returning(UploadTokenScope.Organization, null);

    await Handler().Handle(Command(), Ct);

    token.RevokedAt.ShouldBe(Now);
    A.CallTo(() => _fixture.Tokens.UpdateAsync(token, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _fixture.Audit.RecordAsync(UploadTokenService.RevokeAuditAction, A<string>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_AlreadyRevoked_ShouldChangeNothing()
  {
    var token = Returning(UploadTokenScope.Organization, null);
    token.Revoke(Now.AddDays(-1));

    await Handler().Handle(Command(), Ct);

    token.RevokedAt.ShouldBe(Now.AddDays(-1));
    A.CallTo(() => _fixture.Tokens.UpdateAsync(A<UploadToken>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_fixture.Audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_TokenOfARepository_ShouldThrowNotFound()
  {
    var token = Returning(UploadTokenScope.Repository, 5);

    await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Command(), Ct));

    token.RevokedAt.ShouldBeNull();
  }

  private static RevokeOrganizationUploadTokenCommand Command() => new(Forge.GitHub, "acme", 1);

  private UploadToken Returning(UploadTokenScope scope, long? repositoryId)
  {
    var token = UploadToken.Create(scope, repositoryId, "ci", "hint", Hash, false, "octocat", Now, null);

    A.CallTo(() => _fixture.Tokens.FindAsync(1, A<CancellationToken>._)).Returns(token);

    return token;
  }

  private RevokeOrganizationUploadTokenCommandHandler Handler() => new(_fixture.Service());

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="CreateOrganizationUploadTokenCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.UploadTokens;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;

using static CodeCoverage.Application.Tests.Features.UploadTokens.UploadTokenFixture;

namespace CodeCoverage.Application.Tests.Features.UploadTokens.Commands.CreateOrganizationUploadToken;

public sealed class CreateOrganizationUploadTokenCommandHandlerTests
{
  #region Fields

  private readonly UploadTokenFixture _fixture = new();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Import_ShouldStoreHashOnly()
  {
    UploadToken? saved = null;
    A.CallTo(() => _fixture.Tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._))
     .Invokes((UploadToken token, CancellationToken _) => saved = token)
     .Returns(true);

    await Handler().Handle(Command(Imported), Ct);

    A.CallTo(() => _fixture.Hasher.Hash(Imported)).MustHaveHappenedOnceExactly();
    saved.ShouldNotBeNull();
    saved.Hash.ShouldBe(Hash);
    saved.Imported.ShouldBeTrue();
    saved.Hint.ShouldBe("6666");
    saved.Scope.ToString().ShouldBe(nameof(UploadTokenScope.Organization));
    saved.GetType().GetProperties().Where(static p => p.PropertyType == typeof(string)).Select(p => p.GetValue(saved)).ShouldNotContain(Imported);
  }

  [Fact]
  public async Task Handle_ShouldReturnPlainValueOnce()
  {
    var issued = await Handler().Handle(Command(null), Ct);

    issued.Value.ShouldBe(Generated);
    issued.Token.Hint.ShouldBe("cvc_ab12");
    issued.Token.Imported.ShouldBeFalse();
    issued.ToString().ShouldNotContain(Generated);
    A.CallTo(() => _fixture.Generator.NewUploadToken()).MustHaveHappenedOnceExactly();
  }

  // The rows of two organizations are told apart by the index: the same answer from the repository is the only thing to see here.
  [Fact]
  public async Task Handle_SameImportedValueInOtherOrganization_ShouldAccept()
  {
    var issued = await Handler().Handle(Command(Imported), Ct);

    issued.Value.ShouldBe(Imported);
  }

  [Fact]
  public async Task Handle_SameImportedValueTwiceInOrganization_ShouldThrowConflict()
  {
    A.CallTo(() => _fixture.Tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._)).Returns(false);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(Imported), Ct));

    exception.ErrorCode.ShouldBe(UploadTokenErrors.AlreadyImported);
    A.CallTo(_fixture.Audit).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ShouldAuditWithoutTheValue()
  {
    await Handler().Handle(Command(Imported), Ct);

    A.CallTo(() => _fixture.Audit.RecordAsync(
                     UploadTokenService.CreateAuditAction,
                     A<string>._,
                     A<IReadOnlyDictionary<string, string>>.That.Matches(static d => !d.Values.Contains(Imported)),
                     A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void ToString_ShouldNotShowTheImportedValue()
  {
    Command(Imported).ToString().ShouldNotContain(Imported);
  }

  private static CreateOrganizationUploadTokenCommand Command(string? importedValue) => new(Forge.GitHub, "acme", "ci", importedValue, null);

  private CreateOrganizationUploadTokenCommandHandler Handler() => new(_fixture.Service());

  #endregion
}

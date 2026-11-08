// ------------------------------------------------------------------------------------------------
// <copyright file="ReplaceSecretCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

using static CodeCoverage.Application.Tests.Features.Settings.Support.SettingsStoreFakes;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.ReplaceSecret;

public sealed class ReplaceSecretCommandHandlerTests
{
  #region Fields

  private const string PlainSecret = "s3cr3t-plain";

  private const string ProtectedSecret = "CfDJ8-protected";

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  #endregion

  #region Ctors

  public ReplaceSecretCommandHandlerTests()
  {
    A.CallTo(() => _protector.Protect(PlainSecret)).Returns(ProtectedSecret);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldProtectBeforeSaving()
  {
    var definition = Definition(SettingDefinitions.GitHubWebhookSecret);
    var store = Returning(definition, null, SettingSource.Default);
    var handler = new ReplaceSecretCommandHandler(store, _protector, _auditLog);

    await handler.Handle(new ReplaceSecretCommand(definition.Key, PlainSecret), Ct);

    A.CallTo(() => _protector.Protect(PlainSecret)).MustHaveHappenedOnceExactly()
     .Then(A.CallTo(() => store.SaveAsync(definition, ProtectedSecret, Ct)).MustHaveHappenedOnceExactly());
    A.CallTo(() => store.SaveAsync(A<SettingDefinition>._, PlainSecret, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ShouldAuditWithoutAnyValue()
  {
    var definition = Definition(SettingDefinitions.GitHubClientSecret);
    var store = Returning(definition, "old-protected", SettingSource.Store);
    var handler = new ReplaceSecretCommandHandler(store, _protector, _auditLog);

    await handler.Handle(new ReplaceSecretCommand(definition.Key, PlainSecret), Ct);

    A.CallTo(() => _auditLog.RecordAsync("Settings.ReplaceSecret", definition.Key, null, Ct))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_WithSettingThatIsNotSecret_ShouldThrowUnprocessableEntity()
  {
    var store = A.Fake<ISettingsStore>();
    var handler = new ReplaceSecretCommandHandler(store, _protector, _auditLog);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await handler.Handle(
                                    new ReplaceSecretCommand(SettingDefinitions.GitHubAppId, PlainSecret),
                                    Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.NotSecret);
    A.CallTo(store).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WhenProvidedByEnvironment_ShouldThrowUnprocessableEntity()
  {
    var definition = Definition(SettingDefinitions.GitHubPrivateKeyPem);
    var store = Returning(definition, "env-protected", SettingSource.Environment);
    var handler = new ReplaceSecretCommandHandler(store, _protector, _auditLog);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await handler.Handle(new ReplaceSecretCommand(definition.Key, PlainSecret), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.ProvidedByEnvironment);
    A.CallTo(_protector).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WithUnknownKey_ShouldThrowNotFound()
  {
    var handler = new ReplaceSecretCommandHandler(A.Fake<ISettingsStore>(), _protector, _auditLog);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await handler.Handle(new ReplaceSecretCommand("Nope:Secret", PlainSecret), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
  }

  [Fact]
  public void ToString_ShouldNeverContainTheSecret()
  {
    var command = new ReplaceSecretCommand(SettingDefinitions.GitHubWebhookSecret, PlainSecret);

    command.ToString().ShouldNotContain(PlainSecret);
    command.ToString().ShouldContain(SettingDefinitions.GitHubWebhookSecret);
    command.ShouldBeAssignableTo<IAdminOnly>();
  }

  [Fact]
  public void Validator_WithEmptySecret_ShouldFail()
  {
    var result = new ReplaceSecretCommandValidator().Validate(
      new ReplaceSecretCommand(SettingDefinitions.GitHubWebhookSecret, string.Empty));

    result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(ReplaceSecretCommand.Value));
  }

  #endregion
}

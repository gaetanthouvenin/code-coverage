// ------------------------------------------------------------------------------------------------
// <copyright file="ResetSettingCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.ResetSetting;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

using static CodeCoverage.Application.Tests.Features.Settings.Support.SettingsStoreFakes;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.ResetSetting;

public sealed class ResetSettingCommandHandlerTests
{
  #region Fields

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldResetThenAudit()
  {
    var definition = Definition(SettingDefinitions.BadgesLabel);
    var store = Returning(definition, "cov", SettingSource.Store);
    var handler = new ResetSettingCommandHandler(store, _auditLog);
    IReadOnlyDictionary<string, string>? details = null;
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, Ct))
     .Invokes((string _, string _, IReadOnlyDictionary<string, string>? d, CancellationToken _) => details = d);

    await handler.Handle(new ResetSettingCommand(definition.Key), Ct);

    A.CallTo(() => store.ResetAsync(definition, Ct)).MustHaveHappenedOnceExactly()
     .Then(A.CallTo(() => _auditLog.RecordAsync("Settings.Reset", definition.Key, A<IReadOnlyDictionary<string, string>?>._, Ct))
            .MustHaveHappenedOnceExactly());
    details.ShouldNotBeNull();
    details["PreviousValue"].ShouldBe("cov");
    details["PreviousSource"].ShouldBe(nameof(SettingSource.Store));
  }

  [Fact]
  public async Task Handle_WithSecret_ShouldNotAuditThePreviousValue()
  {
    var definition = Definition(SettingDefinitions.GitHubWebhookSecretPrevious);
    var store = Returning(definition, "CfDJ8-protected", SettingSource.Store);
    var handler = new ResetSettingCommandHandler(store, _auditLog);
    IReadOnlyDictionary<string, string>? details = null;
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, Ct))
     .Invokes((string _, string _, IReadOnlyDictionary<string, string>? d, CancellationToken _) => details = d);

    await handler.Handle(new ResetSettingCommand(definition.Key), Ct);

    A.CallTo(() => store.ResetAsync(definition, Ct)).MustHaveHappenedOnceExactly();
    details.ShouldNotBeNull();
    details.ContainsKey("PreviousValue").ShouldBeFalse();
    details.Values.ShouldNotContain("CfDJ8-protected");
  }

  [Fact]
  public async Task Handle_WhenProvidedByEnvironment_ShouldThrowUnprocessableEntity()
  {
    var definition = Definition(SettingDefinitions.GitHubAppSlug);
    var store = Returning(definition, "other-app", SettingSource.Environment);
    var handler = new ResetSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await handler.Handle(new ResetSettingCommand(definition.Key), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.ProvidedByEnvironment);
    A.CallTo(() => store.ResetAsync(A<SettingDefinition>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WithUnknownKey_ShouldThrowNotFound()
  {
    var handler = new ResetSettingCommandHandler(A.Fake<ISettingsStore>(), _auditLog);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await handler.Handle(new ResetSettingCommand("Nope:Missing"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
  }

  [Theory]
  [InlineData(SettingDefinitions.CoverageOrganizationYaml)]
  [InlineData(SettingDefinitions.NotificationsStatusContextPrefix)]
  [InlineData(SettingDefinitions.NotificationsStatusesEnabled)]
  [InlineData(SettingDefinitions.NotificationsCommentsEnabled)]
  public async Task Handle_OrganizationKey_ShouldThrow(string key)
  {
    var store = A.Fake<ISettingsStore>();
    var handler = new ResetSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await handler.Handle(new ResetSettingCommand(key), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
    A.CallTo(store).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public void Command_ShouldBeAdminOnly()
  {
    new ResetSettingCommand("Badges:Label").ShouldBeAssignableTo<IAdminOnly>();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

using static CodeCoverage.Application.Tests.Features.Settings.Support.SettingsStoreFakes;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.UpdateSetting;

public sealed class UpdateSettingCommandHandlerTests
{
  #region Fields

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_WhenProvidedByEnvironment_ShouldThrowUnprocessableEntity()
  {
    var definition = Definition(SettingDefinitions.GitHubAppId);
    var store = Returning(definition, "12345", SettingSource.Environment);
    var handler = new UpdateSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await handler.Handle(new UpdateSettingCommand(definition.Key, "999"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.ProvidedByEnvironment);
    A.CallTo(() => store.SaveAsync(A<SettingDefinition>._, A<string>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_ShouldAudit()
  {
    var definition = Definition(SettingDefinitions.UploadsMaxFilesPerUpload);
    var store = Returning(definition, "50", SettingSource.Default);
    var handler = new UpdateSettingCommandHandler(store, _auditLog);
    IReadOnlyDictionary<string, string>? details = null;
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, Ct))
     .Invokes((string _, string _, IReadOnlyDictionary<string, string>? d, CancellationToken _) => details = d);

    await handler.Handle(new UpdateSettingCommand("uploads:maxfilesperupload", "75"), Ct);

    A.CallTo(() => store.SaveAsync(definition, "75", Ct)).MustHaveHappenedOnceExactly()
     .Then(A.CallTo(() => _auditLog.RecordAsync("Settings.Update", definition.Key, A<IReadOnlyDictionary<string, string>?>._, Ct))
            .MustHaveHappenedOnceExactly());
    details.ShouldNotBeNull();
    details["Value"].ShouldBe("75");
    details["PreviousValue"].ShouldBe("50");
    details["PreviousSource"].ShouldBe(nameof(SettingSource.Default));
  }

  [Theory]
  [InlineData(SettingDefinitions.UploadsMaxFilesPerUpload, " 0042 ", "42")]
  [InlineData(SettingDefinitions.CoverageAllowFilenameOnlyMatch, "True", "true")]
  [InlineData(SettingDefinitions.UrlsApiPublicUrl, " https://cov.example.com ", "https://cov.example.com")]
  [InlineData(SettingDefinitions.NotificationsCommentTitle, " Coverage ", " Coverage ")]
  public async Task Handle_ShouldSaveTheCanonicalValue(string key, string value, string expected)
  {
    var definition = Definition(key);
    var store = Returning(definition, null, SettingSource.Default);
    var handler = new UpdateSettingCommandHandler(store, _auditLog);

    await handler.Handle(new UpdateSettingCommand(key, value), Ct);

    A.CallTo(() => store.SaveAsync(definition, expected, Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_WithUnknownKey_ShouldThrowNotFound()
  {
    var store = A.Fake<ISettingsStore>();
    var handler = new UpdateSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await handler.Handle(new UpdateSettingCommand("Nope:Missing", "1"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
    A.CallTo(store).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(SettingDefinitions.CoverageOrganizationYaml, "ignore: []")]
  [InlineData(SettingDefinitions.NotificationsStatusContextPrefix, "codecov")]
  [InlineData(SettingDefinitions.NotificationsStatusesEnabled, "false")]
  [InlineData(SettingDefinitions.NotificationsCommentsEnabled, "false")]
  public async Task Handle_OrganizationKey_ShouldThrow(string key, string value)
  {
    var store = A.Fake<ISettingsStore>();
    var handler = new UpdateSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await handler.Handle(new UpdateSettingCommand(key, value), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
    A.CallTo(store).MustNotHaveHappened();
    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WithSecret_ShouldThrowUnprocessableEntity()
  {
    var store = A.Fake<ISettingsStore>();
    var handler = new UpdateSettingCommandHandler(store, _auditLog);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await handler.Handle(
                                    new UpdateSettingCommand(SettingDefinitions.GitHubWebhookSecret, "clear"),
                                    Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.IsSecret);
    A.CallTo(store).MustNotHaveHappened();
  }

  [Fact]
  public void Command_ShouldBeAdminOnly()
  {
    new UpdateSettingCommand("GitHub:AppId", "1").ShouldBeAssignableTo<IAdminOnly>();
  }

  #endregion
}

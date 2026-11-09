// ------------------------------------------------------------------------------------------------
// <copyright file="GetSetupStateQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Queries.GetSetupState;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Queries.GetSetupState;

public sealed class GetSetupStateQueryHandlerTests
{
  #region Fields

  private const string Token = "setup-token";

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  #endregion

  #region Ctors

  public GetSetupStateQueryHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid(Token)).Returns(true);
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, null, SettingSource.Default, null, null));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_NothingDone_ShouldReportNothing()
  {
    (await SendAsync(Token)).ShouldBe(new SetupState(false, false, false));
  }

  // GitHub__* in the environment makes the wizard optional: the source of the identifier does not matter.
  [Fact]
  public async Task Handle_AppFromTheEnvironment_ShouldCountAsCreated()
  {
    Returning(SettingDefinitions.GitHubAppId, "1", SettingSource.Environment);

    (await SendAsync(Token)).GitHubAppCreated.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_OnlyOneAddress_ShouldNotCountAsConfigured()
  {
    Returning(SettingDefinitions.UrlsWebPublicUrl, "https://coverage.example.com", SettingSource.Store);

    (await SendAsync(Token)).PublicUrlsConfigured.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_EverythingDone_ShouldBeComplete()
  {
    Returning(SettingDefinitions.GitHubAppId, "1", SettingSource.Store);
    Returning(SettingDefinitions.UrlsWebPublicUrl, "https://coverage.example.com", SettingSource.Store);
    Returning(SettingDefinitions.UrlsApiPublicUrl, "https://api.example.com", SettingSource.Store);
    Returning(SettingDefinitions.AccessAdminUserIds, "583231", SettingSource.Store);

    (await SendAsync(Token)).IsComplete.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_InvalidSetupToken_ShouldThrowUnauthorized()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync("wrong"));

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    A.CallTo(_store).MustNotHaveHappened();
  }

  private void Returning(string key, string value, SettingSource source) =>
    A.CallTo(() => _store.GetAsync(SettingDefinitions.Find(key)!, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, value, source, null, null));

  private ValueTask<SetupState> SendAsync(string? token) =>
    new GetSetupStateQueryHandler(_store, _issuer).Handle(new GetSetupStateQuery(token), Ct);

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteGitHubAppManifestCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.CompleteGitHubAppManifest;

public sealed class CompleteGitHubAppManifestCommandHandlerTests
{
  #region Fields

  private const string Token = "setup-token";

  private const string Code = "manifest-code";

  private static readonly GitHubAppCredentials App = new("12345", "code-coverage", "Iv1.abc", "client-secret", "webhook-secret", "pem-key");

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private readonly IGitHubAppGateway _gateway = A.Fake<IGitHubAppGateway>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<(string Key, string Value)> _saved = [];

  #endregion

  #region Ctors

  public CompleteGitHubAppManifestCommandHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid(Token)).Returns(true);
    A.CallTo(() => _gateway.ConvertManifestAsync(Code, A<CancellationToken>._)).Returns(App);
    A.CallTo(() => _protector.Protect(A<string>._)).ReturnsLazily((string value) => $"sealed:{value.Length}");
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, null, SettingSource.Default, null, null));
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>._, A<string>._, A<CancellationToken>._))
     .Invokes((SettingDefinition definition, string value, CancellationToken _) => _saved.Add((definition.Key, value)));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldProtectSecrets()
  {
    await SendAsync();

    _saved.ShouldContain((SettingDefinitions.GitHubClientSecret, $"sealed:{App.ClientSecret.Length}"));
    _saved.ShouldContain((SettingDefinitions.GitHubWebhookSecret, $"sealed:{App.WebhookSecret.Length}"));
    _saved.ShouldContain((SettingDefinitions.GitHubPrivateKeyPem, $"sealed:{App.PrivateKeyPem.Length}"));
    _saved.Select(static s => s.Value).ShouldNotContain(App.ClientSecret);
    _saved.Select(static s => s.Value).ShouldNotContain(App.WebhookSecret);
    _saved.Select(static s => s.Value).ShouldNotContain(App.PrivateKeyPem);
  }

  [Fact]
  public async Task Handle_ShouldStoreTheIdentifiersAndTheAppIdLast()
  {
    await SendAsync();

    _saved.ShouldContain((SettingDefinitions.GitHubClientId, App.ClientId));
    _saved.ShouldContain((SettingDefinitions.GitHubAppSlug, App.Slug));
    _saved[^1].ShouldBe((SettingDefinitions.GitHubAppId, App.AppId));
    A.CallTo(() => _auditLog.RecordAsync(CompleteGitHubAppManifestCommandHandler.AuditAction, App.AppId, null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // The wizard still has its last step: the token stays valid until CompleteSetup.
  [Fact]
  public async Task Handle_ShouldKeepTheToken()
  {
    await SendAsync();

    A.CallTo(() => _issuer.Revoke()).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("wrong")]
  public async Task Handle_InvalidSetupToken_ShouldThrowUnauthorized(string? token)
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(token));

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    A.CallTo(_gateway).MustNotHaveHappened();
    _saved.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_AppAlreadyCreated_ShouldThrowConflictWithoutUsingTheCode()
  {
    Returning(SettingDefinitions.GitHubAppId, "1", SettingSource.Store);

    var exception = await Should.ThrowAsync<ConflictException>(async () => await SendAsync());

    exception.ErrorCode.ShouldBe(SettingErrors.GitHubAppAlreadyCreated);
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  // The code can be used once: nothing calls GitHub while a setting could still refuse the result.
  [Fact]
  public async Task Handle_SettingProvidedByTheEnvironment_ShouldRefuseBeforeUsingTheCode()
  {
    Returning(SettingDefinitions.GitHubPrivateKeyPem, "from-env", SettingSource.Environment);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await SendAsync());

    exception.ErrorCode.ShouldBe(SettingErrors.ProvidedByEnvironment);
    A.CallTo(_gateway).MustNotHaveHappened();
    _saved.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_CodeUnknownToGitHub_ShouldThrowBadRequest()
  {
    A.CallTo(() => _gateway.ConvertManifestAsync(Code, A<CancellationToken>._)).Returns((GitHubAppCredentials?)null);

    var exception = await Should.ThrowAsync<BadRequestException>(async () => await SendAsync());

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidManifestCode);
    _saved.ShouldBeEmpty();
  }

  [Fact]
  public void ToString_ShouldHoldNeitherTheTokenNorTheCode()
  {
    var command = new CompleteGitHubAppManifestCommand(Token, Code);

    command.ToString().ShouldNotContain(Token);
    command.ToString().ShouldNotContain(Code);
    command.ShouldBeAssignableTo<IAnonymousEntry>();
    App.ToString().ShouldNotContain(App.ClientSecret);
    App.ToString().ShouldNotContain(App.PrivateKeyPem);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Validator_BlankCode_ShouldFail(string code)
  {
    new CompleteGitHubAppManifestCommandValidator().Validate(new CompleteGitHubAppManifestCommand(Token, code))
                                                   .Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.InvalidManifestCode);
  }

  private void Returning(string key, string value, SettingSource source) =>
    A.CallTo(() => _store.GetAsync(SettingDefinitions.Find(key)!, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, value, source, null, null));

  private ValueTask<Mediator.Unit> SendAsync(string? token = Token) =>
    new CompleteGitHubAppManifestCommandHandler(_store, _issuer, _gateway, _protector, _auditLog)
      .Handle(new CompleteGitHubAppManifestCommand(token, Code), Ct);

  #endregion
}

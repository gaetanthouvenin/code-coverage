// ------------------------------------------------------------------------------------------------
// <copyright file="SaveGitHubAppCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.SaveGitHubApp;

public sealed class SaveGitHubAppCommandHandlerTests
{
  #region Fields

  private const string Pem = "-----BEGIN RSA PRIVATE KEY-----\nMIIE\n-----END RSA PRIVATE KEY-----";

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly Dictionary<string, string> _saved = new(StringComparer.Ordinal);

  #endregion

  #region Ctors

  public SaveGitHubAppCommandHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid("secret")).Returns(true);
    A.CallTo(() => _protector.Protect(A<string>._)).ReturnsLazily(call => "protected:" + call.GetArgument<string>(0));
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        var definition = call.GetArgument<SettingDefinition>(0)!;

        return Task.FromResult(_saved.TryGetValue(definition.Key, out var stored)
                                 ? SettingValue.Create(definition, stored, SettingSource.Store, null, null)
                                 : SettingValue.Create(definition, null, SettingSource.Default, null, null));
      });
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>._, A<string>._, A<CancellationToken>._))
     .ReturnsLazily(call =>
      {
        _saved[call.GetArgument<SettingDefinition>(0)!.Key] = call.GetArgument<string>(1)!;

        return Task.CompletedTask;
      });
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldProtectSecretsAndSaveTheIdentifierLast()
  {
    await Handler().Handle(Command(), Ct);

    _saved[SettingDefinitions.GitHubClientId].ShouldBe("Iv1.client");
    _saved[SettingDefinitions.GitHubClientSecret].ShouldBe("protected:client-secret");
    _saved[SettingDefinitions.GitHubWebhookSecret].ShouldBe("protected:webhook-secret");
    _saved[SettingDefinitions.GitHubPrivateKeyPem].ShouldBe("protected:" + Pem);
    _saved[SettingDefinitions.GitHubAppId].ShouldBe("12345");
    _saved.Keys.Last().ShouldBe(SettingDefinitions.GitHubAppId);
    A.CallTo(() => _auditLog.RecordAsync(SaveGitHubAppCommandHandler.AuditAction, "12345", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A threat case of the wizard: an anonymous caller without the token writes nothing.
  [Fact]
  public async Task Handle_WrongToken_ShouldRefuseAndSaveNothing()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await Handler().Handle(Command("wrong"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    _saved.ShouldBeEmpty();
  }

  // A threat case of the wizard: the App of an instance already set up is never replaced.
  [Fact]
  public async Task Handle_AppAlreadyCreated_ShouldRefuse()
  {
    _saved[SettingDefinitions.GitHubAppId] = "999";

    var exception = await Should.ThrowAsync<ConflictException>(async () => await Handler().Handle(Command(), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.GitHubAppAlreadyCreated);
    _saved[SettingDefinitions.GitHubAppId].ShouldBe("999");
  }

  // The command never writes a secret down, not even in a log.
  [Fact]
  public void ToString_ShouldCarryNoSecret()
  {
    var text = Command().ToString();

    text.ShouldNotContain("client-secret");
    text.ShouldNotContain("webhook-secret");
    text.ShouldNotContain("secret");
    text.ShouldNotContain(Pem);
  }

  private static SaveGitHubAppCommand Command(string? token = "secret") =>
    new(token, "12345", "Iv1.client", "client-secret", "webhook-secret", Pem, "code-coverage");

  private SaveGitHubAppCommandHandler Handler() => new(_store, _issuer, _protector, _auditLog);

  #endregion
}

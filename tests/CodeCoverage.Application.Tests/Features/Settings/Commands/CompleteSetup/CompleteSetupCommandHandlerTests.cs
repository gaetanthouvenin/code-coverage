// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteSetupCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.CompleteSetup;

public sealed class CompleteSetupCommandHandlerTests
{
  #region Fields

  private const string Token = "setup-token";

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<(string Key, string Value)> _saved = [];

  #endregion

  #region Ctors

  public CompleteSetupCommandHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid(Token)).Returns(true);
    A.CallTo(() => _store.GetAsync(A<SettingDefinition>._, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, null, SettingSource.Default, null, null));
    A.CallTo(() => _store.SaveAsync(A<SettingDefinition>._, A<string>._, A<CancellationToken>._))
     .Invokes((SettingDefinition definition, string value, CancellationToken _) => _saved.Add((definition.Key, value)));
    Returning(SettingDefinitions.GitHubAppId, "1", SettingSource.Store);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldSaveTheValuesAndForgetTheToken()
  {
    await SendAsync(new CompleteSetupCommand(Token, " https://coverage.example.com ", "https://api.example.com", " 583231,1234 "));

    _saved.ShouldBe(
      [
        (SettingDefinitions.UrlsWebPublicUrl, "https://coverage.example.com"),
        (SettingDefinitions.UrlsApiPublicUrl, "https://api.example.com"),
        (SettingDefinitions.AccessAdminUserIds, "583231,1234"),
      ]
    );
    A.CallTo(() => _issuer.Revoke()).MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(CompleteSetupCommandHandler.AuditAction, A<string>._, null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ValuesNotGiven_ShouldKeepThemAndStillForgetTheToken()
  {
    await SendAsync(new CompleteSetupCommand(Token, null, null, null));

    _saved.ShouldBeEmpty();
    A.CallTo(() => _issuer.Revoke()).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_InvalidSetupToken_ShouldThrowUnauthorized()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(async () => await SendAsync(new CompleteSetupCommand("wrong", null, null, "1")));

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    _saved.ShouldBeEmpty();
    A.CallTo(() => _issuer.Revoke()).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_AppNotCreated_ShouldRefuseAndKeepTheToken()
  {
    A.CallTo(() => _store.GetAsync(SettingDefinitions.Find(SettingDefinitions.GitHubAppId)!, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, null, SettingSource.Default, null, null));

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await SendAsync(new CompleteSetupCommand(Token, null, null, "1")));

    exception.ErrorCode.ShouldBe(SettingErrors.GitHubAppNotCreated);
    A.CallTo(() => _issuer.Revoke()).MustNotHaveHappened();
  }

  // Nothing is half saved: every value is checked before the first write, and the token survives a refusal.
  [Fact]
  public async Task Handle_ValueProvidedByTheEnvironment_ShouldSaveNothingAndKeepTheToken()
  {
    Returning(SettingDefinitions.AccessAdminUserIds, "9", SettingSource.Environment);

    await Should.ThrowAsync<UnprocessableEntityException>(async () => await SendAsync(new CompleteSetupCommand(Token, "https://coverage.example.com", null, "1")));

    _saved.ShouldBeEmpty();
    A.CallTo(() => _issuer.Revoke()).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("octocat")]
  [InlineData("1,octocat")]
  [InlineData("1,,2")]
  [InlineData("")]
  public void Validator_AdministratorNotNumeric_ShouldFail(string list)
  {
    new CompleteSetupCommandValidator().Validate(new CompleteSetupCommand(Token, null, null, list))
                                       .Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.InvalidAdministrators);
  }

  [Fact]
  public void Validator_NotAnAddress_ShouldFail()
  {
    new CompleteSetupCommandValidator().Validate(new CompleteSetupCommand(Token, "not-a-url", null, null))
                                       .Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.InvalidValue);
  }

  [Fact]
  public void Validator_NothingGiven_ShouldPass()
  {
    new CompleteSetupCommandValidator().Validate(new CompleteSetupCommand(Token, null, null, null)).IsValid.ShouldBeTrue();
  }

  private void Returning(string key, string value, SettingSource source) =>
    A.CallTo(() => _store.GetAsync(SettingDefinitions.Find(key)!, A<CancellationToken>._))
     .ReturnsLazily((SettingDefinition definition, CancellationToken _) => SettingValue.Create(definition, value, source, null, null));

  private ValueTask<Mediator.Unit> SendAsync(CompleteSetupCommand command) =>
    new CompleteSetupCommandHandler(_store, _issuer, _auditLog).Handle(command, Ct);

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="SavePublicUrlsCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Commands.SavePublicUrls;

public sealed class SavePublicUrlsCommandHandlerTests
{
  #region Fields

  private readonly ISettingsStore _store = A.Fake<ISettingsStore>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly Dictionary<string, string> _saved = new(StringComparer.Ordinal);

  #endregion

  #region Ctors

  public SavePublicUrlsCommandHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid("secret")).Returns(true);
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

  // Decision 4: the addresses are saved before the App exists, since its manifest carries them.
  [Fact]
  public async Task Handle_WithoutGitHubApp_ShouldSaveBothAddresses()
  {
    await new SavePublicUrlsCommandHandler(_store, _issuer, _auditLog).Handle(Command(), Ct);

    _saved[SettingDefinitions.UrlsWebPublicUrl].ShouldBe("https://coverage.example.com");
    _saved[SettingDefinitions.UrlsApiPublicUrl].ShouldBe("https://coverage-api.example.com");
    A.CallTo(() => _auditLog.RecordAsync(SavePublicUrlsCommandHandler.AuditAction, "setup", null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A threat case of the wizard: an anonymous caller without the token writes nothing.
  [Fact]
  public async Task Handle_WrongToken_ShouldRefuseAndSaveNothing()
  {
    var exception = await Should.ThrowAsync<UnauthorizedException>(
                      async () => await new SavePublicUrlsCommandHandler(_store, _issuer, _auditLog).Handle(Command("wrong"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    _saved.ShouldBeEmpty();
  }

  // The token never reaches a log through the command.
  [Fact]
  public void ToString_ShouldCarryNoToken() => Command().ToString().ShouldNotContain("secret");

  private static SavePublicUrlsCommand Command(string? token = "secret") =>
    new(token, "https://coverage.example.com", "https://coverage-api.example.com");

  #endregion
}

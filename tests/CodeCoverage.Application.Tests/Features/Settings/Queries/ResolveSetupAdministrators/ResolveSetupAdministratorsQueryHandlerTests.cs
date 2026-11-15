// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveSetupAdministratorsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Application.Features.Settings.Queries.ResolveSetupAdministrators;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Queries.ResolveSetupAdministrators;

// M4.02f: the administrators of the wizard are typed by login or by identifier; only identifiers leave the handler.
public sealed class ResolveSetupAdministratorsQueryHandlerTests
{
  #region Fields

  private const string Token = "setup-token";

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private readonly IGitHubAppGateway _gateway = A.Fake<IGitHubAppGateway>();

  #endregion

  #region Ctors

  public ResolveSetupAdministratorsQueryHandlerTests()
  {
    A.CallTo(() => _issuer.IsValid(Token)).Returns(true);
    A.CallTo(() => _gateway.GetAccountByLoginAsync(A<string>._, A<CancellationToken>._)).Returns((GitHubAccountInfo?)null);
    Known("Octocat", "583231", "octocat");
    Known("hubot", "1234", "hubot");
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_Login_ShouldGiveItsIdentifierAndTheLoginOfGitHub()
  {
    var resolved = await HandleAsync("Octocat");

    resolved.ShouldBe([new SetupAdministrator("583231", "octocat")]);
  }

  [Fact]
  public async Task Handle_Identifiers_ShouldKeepThemWithoutCallingGitHub()
  {
    var resolved = await HandleAsync("583231, 1234");

    resolved.ShouldBe([new SetupAdministrator("583231", null), new SetupAdministrator("1234", null)]);
    A.CallTo(() => _gateway.GetAccountByLoginAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_MixedValues_ShouldKeepTheOrderTyped()
  {
    var resolved = await HandleAsync("42 , hubot,Octocat");

    resolved.ShouldBe([new SetupAdministrator("42", null), new SetupAdministrator("1234", "hubot"), new SetupAdministrator("583231", "octocat")]);
  }

  // The same account typed by its login and by its identifier is one administrator, where it first appears.
  [Fact]
  public async Task Handle_Duplicates_ShouldKeepEachAdministratorOnce()
  {
    var resolved = await HandleAsync("583231, Octocat, hubot, 1234, 583231");

    resolved.ShouldBe([new SetupAdministrator("583231", null), new SetupAdministrator("1234", "hubot")]);
  }

  [Fact]
  public async Task Handle_UnknownLogin_ShouldThrowUnprocessableEntity()
  {
    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await HandleAsync("Octocat, nobody-here"));

    exception.ErrorCode.ShouldBe(SettingErrors.UnknownGitHubLogin);
    exception.Message.ShouldContain("nobody-here");
  }

  // A threat case: GitHub is never asked anything for a caller without the setup token.
  [Fact]
  public async Task Handle_InvalidToken_ShouldThrowUnauthorizedBeforeCallingGitHub()
  {
    var handler = new ResolveSetupAdministratorsQueryHandler(_issuer, _gateway);

    var exception = await Should.ThrowAsync<UnauthorizedException>(
                      async () => await handler.Handle(new ResolveSetupAdministratorsQuery("wrong", "Octocat"), Ct)
                    );

    exception.ErrorCode.ShouldBe(SettingErrors.InvalidSetupToken);
    A.CallTo(() => _gateway.GetAccountByLoginAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("583231")]
  [InlineData("octocat")]
  [InlineData("octocat, 583231, northwind-dev")]
  public void Validator_AcceptedList_ShouldPass(string list)
  {
    new ResolveSetupAdministratorsQueryValidator().Validate(new ResolveSetupAdministratorsQuery(Token, list)).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("octocat,")]
  [InlineData("not a login")]
  [InlineData("-octocat")]
  public void Validator_RefusedList_ShouldFail(string? list)
  {
    var result = new ResolveSetupAdministratorsQueryValidator().Validate(new ResolveSetupAdministratorsQuery(Token, list));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(SettingErrors.InvalidAdministrators);
  }

  [Fact]
  public void Query_ShouldBeAuthenticatedByTheSetupTokenAndHideIt()
  {
    typeof(IAnonymousEntry).IsAssignableFrom(typeof(ResolveSetupAdministratorsQuery)).ShouldBeTrue();
    new ResolveSetupAdministratorsQuery(Token, "octocat").ToString().ShouldNotContain(Token);
  }

  private void Known(string typed, string id, string login) =>
    A.CallTo(() => _gateway.GetAccountByLoginAsync(typed, A<CancellationToken>._))
     .Returns(new GitHubAccountInfo(id, login, OrganizationAccountType.User));

  private async Task<IReadOnlyList<SetupAdministrator>> HandleAsync(string list) =>
    await new ResolveSetupAdministratorsQueryHandler(_issuer, _gateway).Handle(new ResolveSetupAdministratorsQuery(Token, list), Ct);

  #endregion
}

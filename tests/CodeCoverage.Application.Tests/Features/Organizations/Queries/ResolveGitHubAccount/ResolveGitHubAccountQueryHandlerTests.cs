// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveGitHubAccountQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Queries.ResolveGitHubAccount;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Queries.ResolveGitHubAccount;

public sealed class ResolveGitHubAccountQueryHandlerTests
{
  #region Fields

  private readonly IGitHubAppGateway _gateway = A.Fake<IGitHubAppGateway>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_KnownLogin_ShouldGiveTheAccountAndItsIdentifier()
  {
    var account = new GitHubAccountInfo("88120441", "northwind-dev", OrganizationAccountType.Organization);
    A.CallTo(() => _gateway.GetAccountByLoginAsync("Northwind-Dev", A<CancellationToken>._)).Returns(account);

    var resolved = await new ResolveGitHubAccountQueryHandler(_gateway).Handle(new ResolveGitHubAccountQuery("Northwind-Dev"), Ct);

    resolved.ShouldBe(account);
  }

  [Fact]
  public async Task Handle_UnknownLogin_ShouldThrowUnprocessableEntity()
  {
    A.CallTo(() => _gateway.GetAccountByLoginAsync(A<string>._, A<CancellationToken>._)).Returns((GitHubAccountInfo?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(
                      async () => await new ResolveGitHubAccountQueryHandler(_gateway).Handle(new ResolveGitHubAccountQuery("nobody-here"), Ct)
                    );

    exception.ErrorCode.ShouldBe(OrganizationErrors.UnknownGitHubLogin);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not a login")]
  public void Validator_InvalidLogin_ShouldFail(string login)
  {
    var result = new ResolveGitHubAccountQueryValidator().Validate(new ResolveGitHubAccountQuery(login));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(OrganizationErrors.InvalidLogin);
  }

  [Fact]
  public void Query_ShouldBeReservedToTheAdministratorsOfTheInstance()
  {
    typeof(IAdminOnly).IsAssignableFrom(typeof(ResolveGitHubAccountQuery)).ShouldBeTrue();
  }

  #endregion
}

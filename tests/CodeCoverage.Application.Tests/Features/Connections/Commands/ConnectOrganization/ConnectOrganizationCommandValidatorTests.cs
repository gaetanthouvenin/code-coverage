// ------------------------------------------------------------------------------------------------
// <copyright file="ConnectOrganizationCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Connections;
using CodeCoverage.Application.Features.Connections.Commands.ConnectOrganization;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.ConnectOrganization;

public sealed class ConnectOrganizationCommandValidatorTests
{
  #region Fields

  private readonly IForgeAvailability _availability = A.Fake<IForgeAvailability>();

  #endregion

  #region Ctors

  public ConnectOrganizationCommandValidatorTests()
  {
    A.CallTo(() => _availability.AdmissionForges).Returns([Forge.GitHub, Forge.AzureDevOps]);
  }

  #endregion

  #region Methods

  [Fact]
  public void Validate_AvailableForge_ShouldAccept()
  {
    Validate(Command(Forge.AzureDevOps, "fabrikam")).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_GitHub_ShouldRefuse()
  {
    var result = Validate(Command(Forge.GitHub, "octo-org"));

    // The installation is the only way to admit a GitHub account: its availability is not even asked.
    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(ConnectionErrors.UseInstallation);
  }

  [Fact]
  public void Validate_ForgeNotAvailable_ShouldRefuse()
  {
    var result = Validate(Command(Forge.Bitbucket, "fabrikam"));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(ConnectionErrors.ForgeNotAvailable);
  }

  [Theory]
  [InlineData("")]
  [InlineData("not a login")]
  [InlineData("team/sub")]
  [InlineData("~stale-1")]
  public void Validate_LoginOutOfShape_ShouldRefuse(string login)
  {
    var result = Validate(Command(Forge.AzureDevOps, login));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(OrganizationErrors.InvalidLogin);
  }

  private static ConnectOrganizationCommand Command(Forge forge, string login) =>
    new(forge, login, ForgeConnectionKind.AzureDevOpsEntraApplication, null, null);

  private FluentValidation.Results.ValidationResult Validate(ConnectOrganizationCommand command) =>
    new ConnectOrganizationCommandValidator(_availability).Validate(command);

  #endregion
}

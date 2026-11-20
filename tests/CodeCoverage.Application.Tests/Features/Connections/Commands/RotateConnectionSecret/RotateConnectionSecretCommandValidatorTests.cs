// ------------------------------------------------------------------------------------------------
// <copyright file="RotateConnectionSecretCommandValidatorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Connections;
using CodeCoverage.Application.Features.Connections.Commands.RotateConnectionSecret;
using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Tests.Features.Connections.Commands.RotateConnectionSecret;

public sealed class RotateConnectionSecretCommandValidatorTests
{
  #region Methods

  [Fact]
  public void Validate_Credential_ShouldAccept()
  {
    Validate(Command(ConnectionSecretKind.Credential, "new-credential")).IsValid.ShouldBeTrue();
  }

  [Fact]
  public void Validate_WebhookSecret_ShouldAccept()
  {
    Validate(Command(ConnectionSecretKind.WebhookSecret, null)).IsValid.ShouldBeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData("Not-Lower")]
  [InlineData("-starts-with-a-hyphen")]
  public void Validate_ConnectionIdOutOfShape_ShouldRefuse(string connectionId)
  {
    var result = Validate(Command(ConnectionSecretKind.WebhookSecret, null) with { ConnectionId = connectionId });

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(ConnectionErrors.NotFound);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  public void Validate_CredentialMissing_ShouldRefuse(string? credential)
  {
    var result = Validate(Command(ConnectionSecretKind.Credential, credential));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(ConnectionErrors.CredentialRefused);
  }

  [Fact]
  public void Validate_WebhookSecretGiven_ShouldRefuse()
  {
    var result = Validate(Command(ConnectionSecretKind.WebhookSecret, "chosen-secret"));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(GeneralErrors.ValidationFailed);
  }

  [Fact]
  public void Validate_UnknownKind_ShouldRefuse()
  {
    var result = Validate(Command((ConnectionSecretKind)42, null));

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(GeneralErrors.ValidationFailed);
  }

  private static RotateConnectionSecretCommand Command(ConnectionSecretKind kind, string? credential) =>
    new(Forge.AzureDevOps, "fabrikam", "azure-devops-1", kind, credential);

  private static FluentValidation.Results.ValidationResult Validate(RotateConnectionSecretCommand command) =>
    new RotateConnectionSecretCommandValidator().Validate(command);

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateOrganizationSettingCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;

/// <summary>Validator for <see cref="UpdateOrganizationSettingCommand" />.</summary>
/// <remarks>
///   The value is checked against the type and the bounds of its setting (<see cref="SettingRules.Check" />). A key that
///   is not a setting of an organization is left to the handler, which refuses it as unknown.
/// </remarks>
internal sealed class UpdateOrganizationSettingCommandValidator : AbstractValidator<UpdateOrganizationSettingCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UpdateOrganizationSettingCommandValidator" /> class.</summary>
  public UpdateOrganizationSettingCommandValidator()
  {
    RuleFor(static c => c.Key).NotEmpty().WithMessage("The key of the setting is required.");
    RuleFor(static c => c.Value).NotNull().WithMessage("The value is required.");
    RuleFor(static c => c.Value).Custom(CheckValue);
  }

  #endregion

  #region Methods

  /// <summary>Checks the value against the type and the bounds of its setting.</summary>
  /// <param name="value">The value.</param>
  /// <param name="context">The validation context.</param>
  private static void CheckValue(string? value, ValidationContext<UpdateOrganizationSettingCommand> context)
  {
    var key = context.InstanceToValidate.Key;

    if (value is null
        || string.IsNullOrWhiteSpace(key)
        || SettingDefinitions.Find(key) is not { Scope: SettingScope.Organization } definition
        || SettingRules.Check(definition, value) is not { } errorCode)
    {
      return;
    }

    context.AddFailure(
      new FluentValidation.Results.ValidationFailure(nameof(UpdateOrganizationSettingCommand.Value), $"The value of '{definition.Key}' is not valid.", value)
      {
        ErrorCode = errorCode,
      }
    );
  }

  #endregion
}

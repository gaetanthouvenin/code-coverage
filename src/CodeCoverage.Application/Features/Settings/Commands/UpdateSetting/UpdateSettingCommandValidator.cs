// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateSettingCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using FluentValidation;
using FluentValidation.Results;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;

/// <summary>Validator for <see cref="UpdateSettingCommand" />.</summary>
/// <remarks>
///   The value is checked against the type and the bounds of its setting. An unknown key, a key set
///   per organization or a secret is left to the handler, which refuses it with its own error code.
/// </remarks>
internal sealed class UpdateSettingCommandValidator : AbstractValidator<UpdateSettingCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UpdateSettingCommandValidator" /> class.</summary>
  public UpdateSettingCommandValidator()
  {
    RuleFor(c => c.Key).NotEmpty().WithMessage("The key of the setting is required.");

    RuleFor(c => c.Value).NotNull().WithMessage("The value is required.");

    RuleFor(c => c.Value).Custom(CheckValue);
  }

  #endregion

  #region Methods

  /// <summary>Checks the value against the type and the bounds of its setting.</summary>
  /// <param name="value">The value.</param>
  /// <param name="context">The validation context.</param>
  private static void CheckValue(string? value, ValidationContext<UpdateSettingCommand> context)
  {
    var key = context.InstanceToValidate.Key;

    if (value is null || string.IsNullOrWhiteSpace(key))
    {
      return;
    }

    var definition = SettingDefinitions.Find(key);

    if (definition is null || definition.Scope != SettingScope.Instance || definition.IsSecret)
    {
      return;
    }

    var errorCode = SettingRules.Check(definition, value);

    if (errorCode is null)
    {
      return;
    }

    var message = errorCode == SettingErrors.OutOfRange
                    ? $"The value of '{definition.Key}' must be between {Bound(definition.Min, "-inf")} and {Bound(definition.Max, "+inf")}."
                    : $"The value of '{definition.Key}' is not a valid {definition.Type}.";

    context.AddFailure(
      new ValidationFailure(nameof(UpdateSettingCommand.Value), message, value) { ErrorCode = errorCode }
    );
  }

  /// <summary>Writes a bound of a number with the invariant culture.</summary>
  /// <param name="bound">The bound, or <see langword="null" /> when there is none.</param>
  /// <param name="unbounded">The text written when there is no bound.</param>
  /// <returns>The bound as text.</returns>
  private static string Bound(int? bound, string unbounded)
  {
    return bound?.ToString(CultureInfo.InvariantCulture) ?? unbounded;
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenRules.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Features.UploadTokens;

/// <summary>The rules shared by the validators of the upload tokens.</summary>
internal static class UploadTokenRules
{
  #region Methods

  /// <summary>Requires a label that fits its column.</summary>
  /// <typeparam name="T">The validated message.</typeparam>
  /// <param name="rule">The rule of the label.</param>
  /// <returns>The same rule, for chaining.</returns>
  public static IRuleBuilderOptions<T, string> MustBeALabel<T>(this IRuleBuilder<T, string> rule) =>
    rule.Must(static label => !string.IsNullOrWhiteSpace(label) && label.Length <= UploadToken.LabelMaxLength)
        .WithErrorCode(UploadTokenErrors.InvalidLabel)
        .WithMessage($"The label is required and holds at most {UploadToken.LabelMaxLength} characters.");

  /// <summary>Requires an imported value in the form of a Codecov token: a UUID.</summary>
  /// <typeparam name="T">The validated message.</typeparam>
  /// <param name="rule">The rule of the imported value.</param>
  /// <returns>The same rule, for chaining.</returns>
  public static IRuleBuilderOptions<T, string?> MustBeACodecovToken<T>(this IRuleBuilder<T, string?> rule) =>
    rule.Must(static value => value is null || Guid.TryParseExact(value, "D", out _))
        .WithErrorCode(UploadTokenErrors.InvalidImportedValue)
        .WithMessage("An imported token is a Codecov token, a UUID.");

  /// <summary>Requires an expiry in the future, when there is one.</summary>
  /// <typeparam name="T">The validated message.</typeparam>
  /// <param name="rule">The rule of the expiry.</param>
  /// <param name="clock">The clock.</param>
  /// <returns>The same rule, for chaining.</returns>
  public static IRuleBuilderOptions<T, DateTimeOffset?> MustBeInTheFuture<T>(this IRuleBuilder<T, DateTimeOffset?> rule, IClock clock) =>
    rule.Must(expiresAt => expiresAt is null || expiresAt > clock.UtcNow)
        .WithErrorCode(UploadTokenErrors.InvalidExpiry)
        .WithMessage("The expiry must be in the future.");

  #endregion
}

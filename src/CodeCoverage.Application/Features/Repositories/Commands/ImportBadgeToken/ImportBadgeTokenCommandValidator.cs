// ------------------------------------------------------------------------------------------------
// <copyright file="ImportBadgeTokenCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Repositories.Commands.ImportBadgeToken;

/// <summary>Validator for <see cref="ImportBadgeTokenCommand" />.</summary>
/// <remarks>Lenient for parity: the graphing tokens of Codecov are about ten alphanumeric characters.</remarks>
internal sealed class ImportBadgeTokenCommandValidator : AbstractValidator<ImportBadgeTokenCommand>
{
  #region Fields

  /// <summary>The longest badge token that can be imported.</summary>
  internal const int MaxLength = 64;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ImportBadgeTokenCommandValidator" /> class.</summary>
  public ImportBadgeTokenCommandValidator()
  {
    RuleFor(static c => c.Value)
      .Must(static value => value is { Length: > 0 and <= MaxLength } && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
      .WithErrorCode(RepositoryErrors.InvalidBadgeToken)
      .WithMessage($"Give 1 to {MaxLength} characters among letters, digits, '_' and '-'.");
  }

  #endregion
}

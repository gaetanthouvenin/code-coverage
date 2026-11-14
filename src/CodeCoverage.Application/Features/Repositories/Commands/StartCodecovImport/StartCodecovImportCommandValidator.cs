// ------------------------------------------------------------------------------------------------
// <copyright file="StartCodecovImportCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Repositories.Commands.StartCodecovImport;

/// <summary>Validator for <see cref="StartCodecovImportCommand" />.</summary>
/// <remarks>Lenient: Codecov alone knows whether the token is valid, and the job tells.</remarks>
internal sealed class StartCodecovImportCommandValidator : AbstractValidator<StartCodecovImportCommand>
{
  #region Fields

  /// <summary>The longest API token of Codecov that is accepted.</summary>
  internal const int MaxLength = 256;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="StartCodecovImportCommandValidator" /> class.</summary>
  public StartCodecovImportCommandValidator()
  {
    RuleFor(static c => c.Token)
      .Must(static value => value is { Length: > 0 and <= MaxLength } && value.All(static c => c is > ' ' and <= '~'))
      .WithErrorCode(RepositoryErrors.InvalidCodecovToken)
      .WithMessage($"Give the API token of Codecov: 1 to {MaxLength} printable characters, without spaces.");
  }

  #endregion
}

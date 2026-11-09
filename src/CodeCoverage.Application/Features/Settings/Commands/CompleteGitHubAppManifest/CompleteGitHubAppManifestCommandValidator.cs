// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteGitHubAppManifestCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;

/// <summary>Validator for <see cref="CompleteGitHubAppManifestCommand" />.</summary>
internal sealed class CompleteGitHubAppManifestCommandValidator : AbstractValidator<CompleteGitHubAppManifestCommand>
{
  #region Fields

  private const int CodeMaxLength = 256;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CompleteGitHubAppManifestCommandValidator" /> class.</summary>
  public CompleteGitHubAppManifestCommandValidator()
  {
    RuleFor(static c => c.Code)
      .Must(static code => !string.IsNullOrWhiteSpace(code) && code.Length <= CodeMaxLength)
      .WithErrorCode(SettingErrors.InvalidManifestCode)
      .WithMessage($"The code is required and holds at most {CodeMaxLength} characters.");
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="SaveGitHubAppCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

namespace CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;

/// <summary>Validator of the <see cref="SaveGitHubAppCommand" /> command.</summary>
internal sealed class SaveGitHubAppCommandValidator : AbstractValidator<SaveGitHubAppCommand>
{
  #region Fields

  /// <summary>The longest value accepted for the fields of the App, the key aside.</summary>
  private const int FieldMaxLength = 256;

  /// <summary>The longest private key accepted, which holds a PEM of 4096 bits with room to spare.</summary>
  private const int KeyMaxLength = 16384;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SaveGitHubAppCommandValidator" /> class.</summary>
  public SaveGitHubAppCommandValidator()
  {
    RuleFor(static c => c.AppId)
      .Must(static id => !string.IsNullOrWhiteSpace(id) && id.Length <= FieldMaxLength && id.All(char.IsAsciiDigit))
      .WithErrorCode(SettingErrors.InvalidValue)
      .WithMessage("The identifier of the App is a number.");

    RuleFor(static c => c.ClientId).Must(IsFilled).WithErrorCode(SettingErrors.InvalidValue).WithMessage("The client identifier is required.");
    RuleFor(static c => c.ClientSecret).Must(IsFilled).WithErrorCode(SettingErrors.InvalidValue).WithMessage("The client secret is required.");
    RuleFor(static c => c.WebhookSecret).Must(IsFilled).WithErrorCode(SettingErrors.InvalidValue).WithMessage("The webhook secret is required.");

    RuleFor(static c => c.PrivateKeyPem)
      .Must(static key => !string.IsNullOrWhiteSpace(key) && key.Length <= KeyMaxLength && key.Contains("-----BEGIN", StringComparison.Ordinal))
      .WithErrorCode(SettingErrors.InvalidValue)
      .WithMessage("The private key is a PEM block.");

    RuleFor(static c => c.Slug)
      .Must(IsAcceptedSlug)
      .WithErrorCode(SettingErrors.InvalidValue)
      .WithMessage("The slug holds letters, digits, hyphens and underscores.");
  }

  #endregion

  #region Methods

  private static bool IsFilled(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= FieldMaxLength;

  private static bool IsAcceptedSlug(string? slug) =>
    slug is null || (slug.Length <= FieldMaxLength && slug.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'));

  #endregion
}

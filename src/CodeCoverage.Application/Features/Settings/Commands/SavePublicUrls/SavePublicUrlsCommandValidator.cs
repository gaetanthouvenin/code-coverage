// ------------------------------------------------------------------------------------------------
// <copyright file="SavePublicUrlsCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;

/// <summary>Validator of the <see cref="SavePublicUrlsCommand" /> command.</summary>
internal sealed class SavePublicUrlsCommandValidator : AbstractValidator<SavePublicUrlsCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="SavePublicUrlsCommandValidator" /> class.</summary>
  public SavePublicUrlsCommandValidator()
  {
    RuleFor(static c => c.WebPublicUrl)
      .Must(static url => IsAcceptedUrl(SettingDefinitions.UrlsWebPublicUrl, url))
      .WithErrorCode(SettingErrors.InvalidValue)
      .WithMessage("The address must be an absolute HTTP or HTTPS URL.");

    RuleFor(static c => c.ApiPublicUrl)
      .Must(static url => IsAcceptedUrl(SettingDefinitions.UrlsApiPublicUrl, url))
      .WithErrorCode(SettingErrors.InvalidValue)
      .WithMessage("The address must be an absolute HTTP or HTTPS URL.");
  }

  #endregion

  #region Methods

  private static bool IsAcceptedUrl(string key, string? url) =>
    !string.IsNullOrWhiteSpace(url) && SettingRules.Check(SettingDefinitions.Find(key)!, url) is null;

  #endregion
}

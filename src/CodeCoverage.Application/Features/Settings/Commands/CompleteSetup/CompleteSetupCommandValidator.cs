// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteSetupCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;

/// <summary>Validator for <see cref="CompleteSetupCommand" />.</summary>
/// <remarks>An administrator is named by a number, which a renamed account or a login taken by another cannot move.</remarks>
internal sealed class CompleteSetupCommandValidator : AbstractValidator<CompleteSetupCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CompleteSetupCommandValidator" /> class.</summary>
  public CompleteSetupCommandValidator()
  {
    RuleFor(static c => c.WebPublicUrl).Must(static url => IsAcceptedUrl(SettingDefinitions.UrlsWebPublicUrl, url)).WithErrorCode(SettingErrors.InvalidValue).WithMessage("The address must be an absolute HTTP or HTTPS URL.");
    RuleFor(static c => c.ApiPublicUrl).Must(static url => IsAcceptedUrl(SettingDefinitions.UrlsApiPublicUrl, url)).WithErrorCode(SettingErrors.InvalidValue).WithMessage("The address must be an absolute HTTP or HTTPS URL.");
    RuleFor(static c => c.AdminUserIds).Must(IsAcceptedList).WithErrorCode(SettingErrors.InvalidAdministrators).WithMessage("Give at least one numeric GitHub identifier, separated by commas.");
  }

  #endregion

  #region Methods

  /// <summary>Checks an address against its setting.</summary>
  /// <param name="key">The key of the setting.</param>
  /// <param name="url">The address, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when the address is absent or accepted.</returns>
  private static bool IsAcceptedUrl(string key, string? url) =>
    url is null || SettingRules.Check(SettingDefinitions.Find(key)!, url) is null;

  /// <summary>Checks a list of administrators.</summary>
  /// <param name="list">The list, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when the list is absent, or holds only numeric identifiers.</returns>
  private static bool IsAcceptedList(string? list) =>
    list is null
    || (list.Split(',', StringSplitOptions.TrimEntries) is { Length: > 0 } ids
        && ids.All(static id => id.Length > 0 && id.All(char.IsAsciiDigit)));

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveSetupAdministratorsQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Settings.Queries.ResolveSetupAdministrators;

/// <summary>Validator for <see cref="ResolveSetupAdministratorsQuery" />.</summary>
internal sealed class ResolveSetupAdministratorsQueryValidator : AbstractValidator<ResolveSetupAdministratorsQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ResolveSetupAdministratorsQueryValidator" /> class.</summary>
  public ResolveSetupAdministratorsQueryValidator()
  {
    RuleFor(static q => q.AdminUserIds)
      .Must(IsAcceptedList)
      .WithErrorCode(SettingErrors.InvalidAdministrators)
      .WithMessage("Give at least one GitHub login or numeric identifier, separated by commas.");
  }

  #endregion

  #region Methods

  /// <summary>Checks a list of administrators.</summary>
  /// <param name="list">The list, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when the list is not empty and every entry is a numeric identifier or a well-formed login.</returns>
  private static bool IsAcceptedList(string? list) =>
    ResolveSetupAdministratorsQueryHandler.EntriesOf(list) is { Length: > 0 } entries
    && entries.All(static e => ResolveSetupAdministratorsQueryHandler.IsIdentifier(e) || Organization.IsWellFormedLogin(e));

  #endregion
}

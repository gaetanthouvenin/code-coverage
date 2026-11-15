// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveGitHubAccountQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Application.Features.Organizations.Queries.ResolveGitHubAccount;

/// <summary>Validator for <see cref="ResolveGitHubAccountQuery" />.</summary>
internal sealed class ResolveGitHubAccountQueryValidator : AbstractValidator<ResolveGitHubAccountQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ResolveGitHubAccountQueryValidator" /> class.</summary>
  public ResolveGitHubAccountQueryValidator()
  {
    RuleFor(static q => q.Login)
      .Must(Organization.IsWellFormedLogin)
      .WithErrorCode(OrganizationErrors.InvalidLogin)
      .WithMessage("The login is not one a forge can give.");
  }

  #endregion
}

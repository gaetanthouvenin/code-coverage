// ------------------------------------------------------------------------------------------------
// <copyright file="AuthorizeUploadQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;

using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Features.Uploads.Queries.AuthorizeUpload;

/// <summary>Validator for <see cref="AuthorizeUploadQuery" />.</summary>
internal sealed class AuthorizeUploadQueryValidator : AbstractValidator<AuthorizeUploadQuery>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="AuthorizeUploadQueryValidator" /> class.</summary>
  public AuthorizeUploadQueryValidator()
  {
    RuleFor(static q => q.Slug)
      .Must(static slug => RepositorySlug.TryParse(slug, out _))
      .WithErrorCode(UploadErrors.InvalidSlug)
      .WithMessage("The repository is written owner/name.");
  }

  #endregion
}

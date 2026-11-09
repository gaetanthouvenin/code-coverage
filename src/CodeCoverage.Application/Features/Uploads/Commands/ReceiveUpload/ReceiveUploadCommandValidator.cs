// ------------------------------------------------------------------------------------------------
// <copyright file="ReceiveUploadCommandValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using FluentValidation;

using CodeCoverage.Domain.Commits;

namespace CodeCoverage.Application.Features.Uploads.Commands.ReceiveUpload;

/// <summary>Validator for <see cref="ReceiveUploadCommand" />.</summary>
internal sealed partial class ReceiveUploadCommandValidator : AbstractValidator<ReceiveUploadCommand>
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ReceiveUploadCommandValidator" /> class.</summary>
  public ReceiveUploadCommandValidator()
  {
    RuleForEach(static c => c.Metadata.Flags)
      .Must(static flag => FlagPattern().IsMatch(flag))
      .WithErrorCode(UploadErrors.InvalidMetadata)
      .WithMessage("A flag holds 1 to 200 characters, without quotes.");

    Limit(static c => c.Metadata.Branch, Commit.BranchMaxLength);
    Limit(static c => c.Metadata.Name, Upload.NameMaxLength);
    Limit(static c => c.Metadata.Root, Upload.RepoRootMaxLength);
    Limit(static c => c.Metadata.UploaderVersion, Upload.UploaderVersionMaxLength);
    Limit(static c => c.Metadata.Ci.Service, Upload.CiServiceMaxLength);
    Limit(static c => c.Metadata.Ci.BuildUrl, Upload.BuildUrlMaxLength);
    Limit(static c => c.Metadata.Ci.BuildId, Upload.BuildIdMaxLength);
    Limit(static c => c.Metadata.Ci.JobId, Upload.BuildIdMaxLength);
    Limit(static c => c.IdempotencyKey, Upload.IdempotencyKeyMaxLength);

    RuleFor(static c => c.Metadata.PullNumber)
      .GreaterThan(0)
      .WithErrorCode(UploadErrors.InvalidMetadata)
      .WithMessage("The number of a pull request is positive.");
  }

  #endregion

  #region Methods

  [GeneratedRegex("""^[^'"]{1,200}$""", RegexOptions.CultureInvariant)]
  private static partial Regex FlagPattern();

  /// <summary>Limits the length of an optional text.</summary>
  /// <param name="selector">The text.</param>
  /// <param name="maxLength">Its maximum length.</param>
  private void Limit(System.Linq.Expressions.Expression<Func<ReceiveUploadCommand, string?>> selector, int maxLength)
  {
    RuleFor(selector)
      .Must(value => value is null || (value.Length > 0 && value.Length <= maxLength))
      .WithErrorCode(UploadErrors.InvalidMetadata)
      .WithMessage($"The value is not empty and holds at most {maxLength} characters.");
  }

  #endregion
}

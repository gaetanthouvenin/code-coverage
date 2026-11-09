// ------------------------------------------------------------------------------------------------
// <copyright file="UploadPart.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>A part of an upload, read as a stream.</summary>
/// <param name="Kind">What the part holds.</param>
/// <param name="FileName">The name the part was sent under.</param>
/// <param name="Content">Its content.</param>
public sealed record UploadPart(UploadPartKind Kind, string FileName, Stream Content)
{
  #region Fields

  /// <summary>The maximum length of <see cref="FileName" />.</summary>
  public const int FileNameMaxLength = 1024;

  #endregion
}

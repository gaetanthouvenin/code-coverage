// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTooLargeException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Contracts.Uploads;

/// <summary>Exception thrown when the decompressed content of an upload exceeds a limit.</summary>
public sealed class UploadTooLargeException : Exception
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UploadTooLargeException" /> class.</summary>
  /// <param name="message">The limit that was exceeded.</param>
  public UploadTooLargeException(string message)
    : base(message)
  {
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UploadReceipt.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Http;

/// <summary>What the server answered to an upload it accepted.</summary>
/// <param name="StatusUrl">
///   The address the server gave to follow the processing, or <see langword="null" /> when it gave none
///   that can be asked safely.
/// </param>
/// <param name="Credential">The credential the upload was sent with, which reads its state as well.</param>
internal sealed record UploadReceipt(Uri? StatusUrl, string Credential)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>The credential is never printed.</remarks>
  public override string ToString() => $"{nameof(UploadReceipt)} {{ StatusUrl = {StatusUrl} }}";

  #endregion
}

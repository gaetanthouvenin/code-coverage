// ------------------------------------------------------------------------------------------------
// <copyright file="GzipFileContent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.IO.Compression;
using System.Net;

namespace CodeCoverage.Uploader.Http;

/// <summary>The content of a report, compressed as it is sent.</summary>
/// <param name="path">The path of the report.</param>
internal sealed class GzipFileContent(string path) : HttpContent
{
  #region Methods

  /// <inheritdoc />
  protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
  {
    await using var file = File.OpenRead(path);
    await using var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);

    await file.CopyToAsync(gzip).ConfigureAwait(false);
  }

  /// <inheritdoc />
  protected override bool TryComputeLength(out long length)
  {
    length = 0;

    return false;
  }

  #endregion
}

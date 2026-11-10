// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net.Mime;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeCoverage.Api.Configuration.Uploader;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploader;

namespace CodeCoverage.Api.Controllers;

/// <summary><c>/api/v1/uploader</c>: the standalone binaries of <c>coverage-upload</c>, for the agents without .NET.</summary>
/// <param name="options">Where the image keeps the binaries.</param>
[ApiController]
[Route(CodeCoverageApi.Route + "/uploader")]
[Tags(CodeCoverageApi.Tag)]
public sealed class UploaderController(IOptions<UploaderFilesOptions> options) : ControllerBase
{
  #region Methods

  /// <summary>Download the uploader</summary>
  /// <remarks>
  ///   <para>
  ///     <c>GET /api/v1/uploader/{rid}</c> answers the self-contained, single-file binary of the uploader
  ///     for a runtime: <c>linux-x64</c>, <c>linux-musl-x64</c>, <c>win-x64</c> or <c>osx-arm64</c>.
  ///     <c>GET /api/v1/uploader/{rid}.sha256</c> answers its SHA-256, in lower-case hexadecimal.
  ///   </para>
  ///   <para>
  ///     No credential is needed. The sum served here only guards against a corrupted download: a
  ///     pipeline pins the sum of the version it trusts and compares the binary with it before running it.
  ///   </para>
  /// </remarks>
  /// <param name="name">The runtime identifier, followed by <c>.sha256</c> for the sum.</param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>The binary, or its sum as plain text.</returns>
  /// <exception cref="NotFoundException">The runtime is not one of the list, or its binary is not shipped with this server.</exception>
  [HttpGet("{name}", Name = "DownloadUploader")]
  [AllowAnonymous]
  [Produces(MediaTypeNames.Application.Octet, MediaTypeNames.Text.Plain)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> DownloadAsync(string name, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(name);

    // One action for both addresses: « linux-x64.sha256 » would match a « {rid} » route as well.
    var isChecksum = name.EndsWith(UploaderFileCatalog.ChecksumSuffix, StringComparison.Ordinal);
    var runtimeIdentifier = isChecksum ? name[..^UploaderFileCatalog.ChecksumSuffix.Length] : name;

    // The path is built from the constant of the list, never from what was sent.
    if (!UploaderFileCatalog.TryGetFileName(runtimeIdentifier, out var fileName))
    {
      throw new NotFoundException(UploaderErrors.UnknownRuntime, "The uploader is not published for this runtime.");
    }

    var path = Path.Combine(options.Value.Directory, isChecksum ? fileName + UploaderFileCatalog.ChecksumSuffix : fileName);

    if (!System.IO.File.Exists(path))
    {
      throw new NotFoundException(UploaderErrors.NotAvailable, "The uploader of this runtime is not shipped with this server.");
    }

    if (!isChecksum)
    {
      return PhysicalFile(Path.GetFullPath(path), MediaTypeNames.Application.Octet, fileName, enableRangeProcessing: true);
    }

    var checksum = await System.IO.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

    return Content(checksum.Trim() + "\n", MediaTypeNames.Text.Plain);
  }

  #endregion
}

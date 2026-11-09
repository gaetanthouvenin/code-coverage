// ------------------------------------------------------------------------------------------------
// <copyright file="UploadMultipartReader.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Api.Uploads;

/// <summary>Reads an upload request as it arrives: its text fields first, then its files, without model binding.</summary>
internal sealed class UploadMultipartReader
{
  #region Fields

  private const string ReportField = "file";

  private const string NetworkField = "network";

  private const string DefaultReportName = "report";

  private const string DefaultNetworkName = "network.txt";

  private const int MaxFieldBytes = 65_536;

  private const int MaxFields = 32;

  private readonly MultipartReader _reader;

  private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);

  private bool _fileSeen;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="UploadMultipartReader" /> class.</summary>
  /// <param name="request">The request.</param>
  /// <exception cref="BadRequestException">The request is not <c>multipart/form-data</c>.</exception>
  public UploadMultipartReader(HttpRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
        || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
        || HeaderUtilities.RemoveQuotes(contentType.Boundary).Length == 0)
    {
      throw new BadRequestException(UploadErrors.InvalidMetadata, "An upload is a multipart/form-data request.");
    }

    _reader = new MultipartReader(HeaderUtilities.RemoveQuotes(contentType.Boundary).ToString(), request.Body);
  }

  #endregion

  #region Properties

  /// <summary>Gets the text fields read so far.</summary>
  public IReadOnlyDictionary<string, string> Fields => _fields;

  #endregion

  #region Methods

  /// <summary>Reads the text fields up to the next file.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The file, or <see langword="null" /> at the end of the request.</returns>
  /// <exception cref="BadRequestException">
  ///   A text field follows a file, a field cannot be read, or the body is not well-formed multipart.
  /// </exception>
  public async Task<MultipartFilePart?> ReadNextFileAsync(CancellationToken cancellationToken)
  {
    while (await ReadNextSectionAsync(cancellationToken).ConfigureAwait(false) is { } section)
    {
      var disposition = section.GetContentDispositionHeader();
      var name = HeaderUtilities.RemoveQuotes(disposition?.Name ?? default).ToString();

      if (name is ReportField or NetworkField)
      {
        _fileSeen = true;

        var fileName = disposition!.FileNameStar.HasValue ? disposition.FileNameStar.ToString() : HeaderUtilities.RemoveQuotes(disposition.FileName).ToString();
        var isNetwork = name is NetworkField;

        return new MultipartFilePart(
          isNetwork,
          fileName.Length > 0 ? fileName : isNetwork ? DefaultNetworkName : DefaultReportName,
          new MultipartSectionStream(section.Body)
        );
      }

      if (_fileSeen)
      {
        throw new BadRequestException(UploadErrors.MetadataMustPrecedeFiles, "The text fields must come before the files.");
      }

      await ReadFieldAsync(name, new MultipartSectionStream(section.Body), cancellationToken).ConfigureAwait(false);
    }

    return null;
  }

  /// <summary>Reads the files of the request, after the first one that was already read.</summary>
  /// <param name="first">The first file.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The files, as they arrive.</returns>
  public async IAsyncEnumerable<MultipartFilePart> ReadFilesAsync(MultipartFilePart first, [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var file = first;

    while (file is not null)
    {
      yield return file;

      file = await ReadNextFileAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Reads the next section, refusing a body that is not well-formed multipart.</summary>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The section, or <see langword="null" /> at the end of the request.</returns>
  private async Task<MultipartSection?> ReadNextSectionAsync(CancellationToken cancellationToken)
  {
    try
    {
      return await _reader.ReadNextSectionAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (MultipartSectionStream.IsMalformed(exception))
    {
      throw MultipartSectionStream.Malformed(exception);
    }
  }

  /// <summary>Accumulates a text field.</summary>
  /// <param name="name">The name of the field.</param>
  /// <param name="body">Its content.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>A task that completes when the field is read.</returns>
  private async Task ReadFieldAsync(string name, Stream body, CancellationToken cancellationToken)
  {
    if (_fields.Count >= MaxFields)
    {
      throw new BadRequestException(UploadErrors.InvalidMetadata, "The request holds too many text fields.");
    }

    using var content = new MemoryStream();
    var buffer = new byte[4096];
    int read;

    while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
    {
      if (content.Length + read > MaxFieldBytes)
      {
        throw new BadRequestException(UploadErrors.InvalidMetadata, $"The field '{name}' is too long.");
      }

      content.Write(buffer, 0, read);
    }

    _fields[name] = Encoding.UTF8.GetString(content.GetBuffer(), 0, (int)content.Length);
  }

  #endregion
}

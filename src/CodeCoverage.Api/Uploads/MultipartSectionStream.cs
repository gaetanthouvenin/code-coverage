// ------------------------------------------------------------------------------------------------
// <copyright file="MultipartSectionStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Uploads;

namespace CodeCoverage.Api.Uploads;

/// <summary>The content of a file of an upload request, which reports a body that is not well-formed multipart as a refusal.</summary>
/// <param name="inner">The body of the section.</param>
/// <remarks>
///   <c>MultipartReader</c> raises an <see cref="IOException" /> when the body ends before the closing boundary and an
///   <see cref="InvalidDataException" /> when its headers are too long: a mistake of the client, answered with
///   400 <c>Upload.InvalidPart</c> and not 500. A limit of Kestrel (<see cref="BadHttpRequestException" />, a body too
///   large) keeps its own answer.
/// </remarks>
internal sealed class MultipartSectionStream(Stream inner) : Stream
{
  #region Properties

  /// <inheritdoc />
  public override bool CanRead => true;

  /// <inheritdoc />
  public override bool CanSeek => false;

  /// <inheritdoc />
  public override bool CanWrite => false;

  /// <inheritdoc />
  public override long Length => throw new NotSupportedException();

  /// <inheritdoc />
  public override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  #endregion

  #region Methods

  /// <summary>Tells whether an exception raised while the body is read means that it is not well-formed multipart.</summary>
  /// <param name="exception">The exception.</param>
  /// <returns><see langword="true" /> for a framing error of the client.</returns>
  public static bool IsMalformed(Exception exception) =>
    exception is (IOException or InvalidDataException) and not BadHttpRequestException;

  /// <summary>Builds the refusal of a body that is not well-formed multipart.</summary>
  /// <param name="exception">What the reader raised.</param>
  /// <returns>The exception.</returns>
  public static BadRequestException Malformed(Exception exception) =>
    new(UploadErrors.InvalidPart, "The body of the request is not well-formed multipart/form-data.", exception);

  /// <inheritdoc />
  public override void Flush()
  {
  }

  /// <inheritdoc />
  public override int Read(byte[] buffer, int offset, int count)
  {
    try
    {
      return inner.Read(buffer, offset, count);
    }
    catch (Exception exception) when (IsMalformed(exception))
    {
      throw Malformed(exception);
    }
  }

  /// <inheritdoc />
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    try
    {
      return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (IsMalformed(exception))
    {
      throw Malformed(exception);
    }
  }

  /// <inheritdoc />
  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  /// <inheritdoc />
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  /// <inheritdoc />
  public override void SetLength(long value) => throw new NotSupportedException();

  /// <inheritdoc />
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  #endregion
}

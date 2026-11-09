// ------------------------------------------------------------------------------------------------
// <copyright file="HeadReplayStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Services;

/// <summary>Reads again the start of a stream that cannot seek, then the rest of it.</summary>
/// <param name="head">The bytes already read.</param>
/// <param name="length">How many of them count.</param>
/// <param name="rest">The stream they were read from.</param>
internal sealed class HeadReplayStream(byte[] head, int length, Stream rest) : Stream
{
  #region Fields

  private int _position;

  #endregion

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

  /// <inheritdoc />
  public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

  /// <inheritdoc />
  public override int Read(Span<byte> buffer)
  {
    if (_position >= length)
    {
      return rest.Read(buffer);
    }

    var count = Math.Min(buffer.Length, length - _position);

    head.AsSpan(_position, count).CopyTo(buffer);
    _position += count;

    return count;
  }

  /// <inheritdoc />
  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  /// <inheritdoc />
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    if (_position >= length)
    {
      return await rest.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    var count = Math.Min(buffer.Length, length - _position);

    head.AsMemory(_position, count).CopyTo(buffer);
    _position += count;

    return count;
  }

  /// <inheritdoc />
  public override void Flush()
  {
  }

  /// <inheritdoc />
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  /// <inheritdoc />
  public override void SetLength(long value) => throw new NotSupportedException();

  /// <inheritdoc />
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  #endregion
}

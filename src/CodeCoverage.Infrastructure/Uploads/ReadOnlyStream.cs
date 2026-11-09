// ------------------------------------------------------------------------------------------------
// <copyright file="ReadOnlyStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>The base of the forward-only streams that are only read.</summary>
internal abstract class ReadOnlyStream : Stream
{
  #region Properties

  /// <inheritdoc />
  public sealed override bool CanRead => true;

  /// <inheritdoc />
  public sealed override bool CanSeek => false;

  /// <inheritdoc />
  public sealed override bool CanWrite => false;

  /// <inheritdoc />
  public sealed override long Length => throw new NotSupportedException();

  /// <inheritdoc />
  public sealed override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  #endregion

  #region Methods

  /// <inheritdoc />
  public sealed override void Flush()
  {
  }

  /// <inheritdoc />
  public abstract override int Read(byte[] buffer, int offset, int count);

  /// <inheritdoc />
  public abstract override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

  /// <inheritdoc />
  public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  /// <inheritdoc />
  public sealed override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  /// <inheritdoc />
  public sealed override void SetLength(long value) => throw new NotSupportedException();

  /// <inheritdoc />
  public sealed override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  #endregion
}

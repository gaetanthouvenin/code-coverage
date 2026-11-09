// ------------------------------------------------------------------------------------------------
// <copyright file="ChunkWriteStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Cuts what it receives into chunks of a fixed size and saves each one as soon as it is full.</summary>
/// <param name="saveChunk">Saves a chunk by its index; the bytes are only valid during the call.</param>
/// <param name="chunkSize">The size of a chunk.</param>
internal sealed class ChunkWriteStream(
  Func<int, ReadOnlyMemory<byte>, CancellationToken, Task> saveChunk,
  int chunkSize) : Stream
{
  #region Fields

  /// <summary>The chunk being filled.</summary>
  private readonly byte[] _buffer = new byte[chunkSize];

  /// <summary>How many bytes of the chunk are filled.</summary>
  private int _filled;

  /// <summary>The index of the next chunk.</summary>
  private int _nextIndex;

  /// <summary>Whether what is written is dropped.</summary>
  private bool _discarded;

  #endregion

  #region Properties

  /// <inheritdoc />
  public override bool CanRead => false;

  /// <inheritdoc />
  public override bool CanSeek => false;

  /// <inheritdoc />
  public override bool CanWrite => true;

  /// <inheritdoc />
  public override long Length => throw new NotSupportedException();

  /// <inheritdoc />
  public override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  /// <summary>Gets the number of bytes received.</summary>
  public long TotalBytes { get; private set; }

  #endregion

  #region Methods

  /// <inheritdoc />
  public override void Flush()
  {
  }

  /// <inheritdoc />
  public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  /// <inheritdoc />
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  /// <inheritdoc />
  public override void SetLength(long value) => throw new NotSupportedException();

  /// <inheritdoc />
  /// <exception cref="NotSupportedException">The stream was not discarded: saving a chunk is asynchronous.</exception>
  public override void Write(byte[] buffer, int offset, int count)
  {
    if (!_discarded)
    {
      throw new NotSupportedException("The chunks are saved asynchronously.");
    }
  }

  /// <inheritdoc />
  public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  /// <inheritdoc />
  public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
  {
    if (_discarded)
    {
      return;
    }

    TotalBytes += buffer.Length;

    while (!buffer.IsEmpty)
    {
      var count = Math.Min(chunkSize - _filled, buffer.Length);

      buffer[..count].CopyTo(_buffer.AsMemory(_filled));
      _filled += count;
      buffer = buffer[count..];

      if (_filled == chunkSize)
      {
        await SaveCurrentAsync(cancellationToken).ConfigureAwait(false);
      }
    }
  }

  /// <summary>Saves the last chunk, if it holds anything.</summary>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  public async Task CompleteAsync(CancellationToken cancellationToken)
  {
    if (_filled > 0)
    {
      await SaveCurrentAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  /// <summary>Drops everything written from now on, so that a compressor can be disposed after a failure.</summary>
  public void Discard() => _discarded = true;

  /// <summary>Saves the chunk being filled.</summary>
  /// <param name="cancellationToken">A token to cancel the saving.</param>
  /// <returns>A task that completes when it is saved.</returns>
  private async Task SaveCurrentAsync(CancellationToken cancellationToken)
  {
    await saveChunk(_nextIndex, _buffer.AsMemory(0, _filled), cancellationToken).ConfigureAwait(false);

    _nextIndex++;
    _filled = 0;
  }

  #endregion
}

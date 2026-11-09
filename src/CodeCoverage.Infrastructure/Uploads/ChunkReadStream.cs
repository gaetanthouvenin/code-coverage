// ------------------------------------------------------------------------------------------------
// <copyright file="ChunkReadStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Uploads;

/// <summary>Reads a sequence of stored chunks as one stream, holding one chunk at a time.</summary>
/// <param name="loadAsync">Loads a chunk by its index, or returns <see langword="null" /> after the last one.</param>
/// <param name="load">The same, for a synchronous reader.</param>
internal sealed class ChunkReadStream(
  Func<int, CancellationToken, Task<byte[]?>> loadAsync,
  Func<int, byte[]?> load) : ReadOnlyStream
{
  #region Fields

  /// <summary>The chunk being read, or <see langword="null" /> after the last one.</summary>
  private byte[]? _chunk = [];

  /// <summary>How many bytes of the chunk were read.</summary>
  private int _offset;

  /// <summary>The index of the next chunk.</summary>
  private int _nextIndex;

  #endregion

  #region Methods

  /// <inheritdoc />
  public override int Read(byte[] buffer, int offset, int count)
  {
    while (_chunk is not null && _offset == _chunk.Length)
    {
      Accept(load(_nextIndex));
    }

    return Copy(buffer.AsMemory(offset, count));
  }

  /// <inheritdoc />
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    while (_chunk is not null && _offset == _chunk.Length)
    {
      Accept(await loadAsync(_nextIndex, cancellationToken).ConfigureAwait(false));
    }

    return Copy(buffer);
  }

  /// <summary>Makes a loaded chunk the current one.</summary>
  /// <param name="chunk">The chunk, or <see langword="null" /> after the last one.</param>
  private void Accept(byte[]? chunk)
  {
    _chunk = chunk;
    _offset = 0;
    _nextIndex++;
  }

  /// <summary>Copies the bytes of the current chunk that fit.</summary>
  /// <param name="buffer">The destination.</param>
  /// <returns>The number of bytes copied, zero after the last chunk.</returns>
  private int Copy(Memory<byte> buffer)
  {
    if (_chunk is null)
    {
      return 0;
    }

    var count = Math.Min(_chunk.Length - _offset, buffer.Length);

    _chunk.AsMemory(_offset, count).CopyTo(buffer);
    _offset += count;

    return count;
  }

  #endregion
}

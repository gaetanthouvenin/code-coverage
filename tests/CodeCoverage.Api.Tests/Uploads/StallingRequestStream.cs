// ------------------------------------------------------------------------------------------------
// <copyright file="StallingRequestStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Tests.Uploads;

// The start of a request body, then a transfer that never ends: nothing can answer by reading past the start.
internal sealed class StallingRequestStream(byte[] start) : Stream
{
  #region Fields

  private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

  private int _position;

  #endregion

  #region Properties

  public override bool CanRead => true;

  public override bool CanSeek => false;

  public override bool CanWrite => false;

  public override long Length => throw new NotSupportedException();

  public override long Position
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  #endregion

  #region Methods

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    if (_position < start.Length)
    {
      var count = Math.Min(buffer.Length, start.Length - _position);

      start.AsMemory(_position, count).CopyTo(buffer);
      _position += count;

      return count;
    }

    await _released.Task.WaitAsync(cancellationToken);

    return 0;
  }

  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
    ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  public override void Flush()
  {
  }

  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  public override void SetLength(long value) => throw new NotSupportedException();

  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  protected override void Dispose(bool disposing)
  {
    _released.TrySetResult();

    base.Dispose(disposing);
  }

  #endregion
}

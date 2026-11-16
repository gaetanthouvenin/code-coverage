// ------------------------------------------------------------------------------------------------
// <copyright file="BrokenStream.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader.Tests.Http;

// A body whose connection breaks while it is read.
internal sealed class BrokenStream : Stream
{
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

  public override void Flush()
  {
  }

  public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The connection was reset.");

  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  public override void SetLength(long value) => throw new NotSupportedException();

  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  #endregion
}

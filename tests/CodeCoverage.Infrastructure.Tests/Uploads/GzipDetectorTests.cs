// ------------------------------------------------------------------------------------------------
// <copyright file="GzipDetectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

public sealed class GzipDetectorTests
{
  #region Methods

  [Theory]
  [InlineData(new byte[] { 0x1F, 0x8B }, true)]
  [InlineData(new byte[] { 0x1F, 0x8B, 0x08 }, true)]
  [InlineData(new byte[] { 0x1F }, false)]
  [InlineData(new byte[] { 0x1F, 0x00 }, false)]
  [InlineData(new byte[] { 0x3C, 0x3F }, false)]
  [InlineData(new byte[0], false)]
  public void IsGzip_ShouldReadTheSignature(byte[] head, bool expected)
  {
    GzipDetector.IsGzip(head).ShouldBe(expected);
  }

  #endregion
}

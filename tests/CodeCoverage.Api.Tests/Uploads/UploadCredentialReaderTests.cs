// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCredentialReaderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;

using CodeCoverage.Api.Uploads;

namespace CodeCoverage.Api.Tests.Uploads;

public sealed class UploadCredentialReaderTests
{
  #region Methods

  [Theory]
  [InlineData("Authorization", "Bearer abc")]
  [InlineData("Authorization", "bearer abc")]
  [InlineData("Authorization", "token abc")]
  [InlineData("X-Upload-Token", "abc")]
  public void Read_SupportedHeader_ShouldGiveTheValue(string header, string value)
  {
    var headers = new HeaderDictionary { [header] = value };

    UploadCredentialReader.Read(headers).ShouldBe("abc");
  }

  [Theory]
  [InlineData("Authorization", "Basic abc")]
  [InlineData("Authorization", "Bearer  ")]
  [InlineData("X-Upload-Token", "  ")]
  public void Read_NoUsableValue_ShouldGiveNull(string header, string value)
  {
    var headers = new HeaderDictionary { [header] = value };

    UploadCredentialReader.Read(headers).ShouldBeNull();
  }

  [Fact]
  public void Read_NoHeader_ShouldGiveNull()
  {
    UploadCredentialReader.Read(new HeaderDictionary()).ShouldBeNull();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCredentialTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Domain.Contracts.Uploads;

namespace CodeCoverage.Domain.Tests.Contracts.Uploads;

public sealed class UploadCredentialTests
{
  #region Fields

  private const string Generated = "cvc_ab12cd34ef56gh78ij90kl12mn34op56qr78st90uvw";

  #endregion

  #region Methods

  [Fact]
  public void ToString_ShouldRedactValue()
  {
    var text = new UploadCredential(UploadCredentialKind.UploadToken, Generated).ToString();

    text.ShouldBe("cvc_ab12…(redacted)");
    text.ShouldNotContain("cd34");
  }

  [Theory]
  [InlineData(UploadCredentialKind.UploadToken, "5b8c4f8a-1111-4222-8333-444455556666")]
  [InlineData(UploadCredentialKind.Jwt, "eyJhbGciOiJSUzI1NiJ9.payload.signature")]
  public void ToString_ValueNotGenerated_ShouldShowNothing(UploadCredentialKind kind, string value)
  {
    new UploadCredential(kind, value).ToString().ShouldBe("(redacted)");
  }

  [Fact]
  public void Serialize_ShouldLeaveOutTheValue()
  {
    JsonSerializer.Serialize(new UploadCredential(UploadCredentialKind.UploadToken, Generated)).ShouldNotContain(Generated);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Ctor_EmptyValue_ShouldThrow(string value)
  {
    Should.Throw<ArgumentException>(() => new UploadCredential(UploadCredentialKind.Jwt, value));
  }

  #endregion
}

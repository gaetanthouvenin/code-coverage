// ------------------------------------------------------------------------------------------------
// <copyright file="MultipartBuilderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Http;

namespace CodeCoverage.Uploader.Tests.Http;

public sealed class MultipartBuilderTests
{
  #region Fields

  private const string Boundary = "test-boundary";

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Build_Forge_ShouldSendTheField()
  {
    var parts = await PartsOf(Request() with { Forge = "azure-devops" });

    MultipartBody.Text(parts.Single(static part => NameOf(part.Headers) == "forge").Content).ShouldBe("azure-devops");
  }

  // An upload without the field is one of a GitHub repository for the server: nothing is sent.
  [Fact]
  public async Task Build_NoForge_ShouldLeaveTheFieldOut()
  {
    var parts = await PartsOf(Request());

    parts.ShouldNotContain(static part => NameOf(part.Headers) == "forge");
  }

  private static UploadRequest Request() => new() { Slug = "octo-org/app", Sha = new string('a', 40), Reports = [] };

  private static async Task<IReadOnlyList<(string Headers, byte[] Content)>> PartsOf(UploadRequest request)
  {
    using var content = MultipartBuilder.Build(request, Boundary);

    return MultipartBody.Parse(await content.ReadAsByteArrayAsync(Ct), Boundary);
  }

  private static string NameOf(string headers)
  {
    var start = headers.IndexOf("name=", StringComparison.Ordinal) + "name=".Length;
    var end = headers.IndexOfAny([';', '\r', '\n'], start);

    return (end < 0 ? headers[start..] : headers[start..end]).Trim().Trim('"');
  }

  #endregion
}

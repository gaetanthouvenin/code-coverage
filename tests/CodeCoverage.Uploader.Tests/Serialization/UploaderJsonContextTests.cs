// ------------------------------------------------------------------------------------------------
// <copyright file="UploaderJsonContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using CodeCoverage.Uploader.Serialization;

namespace CodeCoverage.Uploader.Tests.Serialization;

public sealed class UploaderJsonContextTests
{
  #region Methods

  [Fact]
  public void Deserialize_JsonElement_ShouldUseTheGeneratedMetadata()
  {
    var element = JsonSerializer.Deserialize("""{"number":12}""", UploaderJsonContext.Default.JsonElement);

    element.GetProperty("number").GetInt32().ShouldBe(12);
  }

  #endregion
}

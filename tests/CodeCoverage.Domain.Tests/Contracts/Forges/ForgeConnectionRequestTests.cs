// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeConnectionRequestTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeConnectionRequestTests
{
  #region Methods

  [Fact]
  public void ToString_ShouldNameTheAccountWithoutTheCredential()
  {
    var request = new ForgeConnectionRequest(Forge.AzureDevOps, ForgeConnectionKind.AzureDevOpsEntraApplication, "fabrikam", Guid.Empty, "s3cr3t-value");

    var text = request.ToString();

    text.ShouldContain("fabrikam");
    text.ShouldContain(nameof(Forge.AzureDevOps));
    text.ShouldNotContain("s3cr3t-value");
  }

  #endregion
}

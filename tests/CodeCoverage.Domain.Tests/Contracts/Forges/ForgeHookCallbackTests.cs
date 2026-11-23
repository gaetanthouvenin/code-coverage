// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookCallbackTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeHookCallbackTests
{
  #region Fields

  private static readonly Uri Api = new("https://coverage-api.example.com/");

  #endregion

  #region Methods

  [Fact]
  public void For_ShouldTargetTheRouteOfTheConnection()
  {
    var callback = ForgeHookCallback.For(Api, Forge.AzureDevOps, "contoso-devops", "s3cret");

    callback.Address.ShouldBe(new Uri("https://coverage-api.example.com/api/v1/azure-devops/webhooks/contoso-devops"));
    callback.Secret.ShouldBe("s3cret");
  }

  [Fact]
  public void For_ApiUnderAPath_ShouldKeepThePath()
  {
    ForgeHookCallback.For(new Uri("https://example.com/coverage"), Forge.Bitbucket, "acme", "s3cret")
                     .Address.ShouldBe(new Uri("https://example.com/coverage/api/v1/bitbucket/webhooks/acme"));
  }

  [Fact]
  public void ToString_ShouldNeverWriteTheSecret()
  {
    var text = ForgeHookCallback.For(Api, Forge.GitLab, "acme", "s3cret").ToString();

    text.ShouldContain("/api/v1/gitlab/webhooks/acme");
    text.ShouldNotContain("s3cret");
  }

  [Fact]
  public void For_NullArgument_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => ForgeHookCallback.For(null!, Forge.GitLab, "acme", "s3cret"));
    Should.Throw<ArgumentNullException>(() => ForgeHookCallback.For(Api, Forge.GitLab, null!, "s3cret"));
    Should.Throw<ArgumentNullException>(() => ForgeHookCallback.For(Api, Forge.GitLab, "acme", null!));
  }

  [Fact]
  public void For_InvalidArgument_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => ForgeHookCallback.For(new Uri("/relative", UriKind.Relative), Forge.GitLab, "acme", "s3cret"))
          .ParamName.ShouldBe("apiPublicUrl");
    Should.Throw<ArgumentException>(() => ForgeHookCallback.For(Api, Forge.GitLab, "Not-Well-Formed", "s3cret")).ParamName.ShouldBe("connectionId");
    Should.Throw<ArgumentException>(() => ForgeHookCallback.For(Api, Forge.GitLab, "acme", " ")).ParamName.ShouldBe("secret");
  }

  [Fact]
  public void For_UnknownForge_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => ForgeHookCallback.For(Api, (Forge)99, "acme", "s3cret"));
  }

  #endregion
}

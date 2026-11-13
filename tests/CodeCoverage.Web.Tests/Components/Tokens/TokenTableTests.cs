// ------------------------------------------------------------------------------------------------
// <copyright file="TokenTableTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Tokens;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Tokens;

namespace CodeCoverage.Web.Tests.Components.Tokens;

// The shared table of tokens (M3.35), reused by the repository, the organization and the account.
public sealed class TokenTableTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly UploadTokenViewModel Live = new(1, "azure-pipelines", "cv_12ab", false, Now.AddDays(-6), Now.AddHours(-2), null);

  private static readonly UploadTokenViewModel Imported = new(2, "codecov-import", "…a71c", true, Now.AddDays(-11), null, null);

  private static readonly UploadTokenViewModel Revoked = new(3, "local-dev", "cv_99zz", false, Now.AddDays(-30), null, Now.AddDays(-1));

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowEachTokenWithItsHintAndOrigin()
  {
    var cut = Render<TokenTable>(Tokens(Live, Imported));

    var rows = cut.FindAll("tbody tr");
    rows.Count.ShouldBe(2);
    rows[0].TextContent.ShouldContain("azure-pipelines");
    rows[0].QuerySelector("code")!.TextContent.ShouldBe("cv_12ab");
    rows[1].TextContent.ShouldContain(L("Token_Imported"));
    rows[1].TextContent.ShouldContain(L("Token_NeverUsed"));
  }

  // A revoked token can no longer be used: it is hidden, and no revoke button is offered for it.
  [Fact]
  public void Render_RevokedToken_ShouldHideItBehindTheToggle()
  {
    var cut = Render<TokenTable>(Tokens(Live, Revoked));

    cut.FindAll("tbody tr").Count.ShouldBe(1);

    cut.Find("input[data-toggle=revoked]").Change(true);

    var rows = cut.FindAll("tbody tr");
    rows.Count.ShouldBe(2);
    rows[1].TextContent.ShouldContain(L("Token_Revoked"));
    rows[1].QuerySelectorAll("button").ShouldBeEmpty();
  }

  // Without a revoked token, the toggle has nothing to offer.
  [Fact]
  public void Render_WithoutRevokedToken_ShouldNotOfferTheToggle()
  {
    var cut = Render<TokenTable>(Tokens(Live));

    cut.FindAll("input[data-toggle=revoked]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_WithoutToken_ShouldShowTheEmptyState()
  {
    var cut = Render<TokenTable>(Tokens());

    cut.Find("[data-state=empty]").TextContent.ShouldContain(L("Token_EmptyTitle"));
  }

  // Only revoked tokens: the list is empty until they are shown.
  [Fact]
  public void Render_OnlyRevokedTokens_ShouldShowTheEmptyStateWithTheToggle()
  {
    var cut = Render<TokenTable>(Tokens(Revoked));

    cut.Find("[data-state=empty]").ShouldNotBeNull();
    cut.Find("input[data-toggle=revoked]").Change(true);
    cut.FindAll("tbody tr").ShouldHaveSingleItem();
  }

  [Fact]
  public void ClickRevoke_ShouldAskThePageForThatToken()
  {
    UploadTokenViewModel? asked = null;
    var cut = Render<TokenTable>(p => Fill(p, [Live]).Add(static c => c.OnRevokeAsked, EventCallback.Factory.Create<UploadTokenViewModel>(this, t => asked = t)));

    cut.Find("tr[data-token='1'] button").Click();

    asked.ShouldBe(Live);
  }

  private static Action<ComponentParameterCollectionBuilder<TokenTable>> Tokens(params UploadTokenViewModel[] tokens) =>
    p => Fill(p, tokens);

  private static ComponentParameterCollectionBuilder<TokenTable> Fill(ComponentParameterCollectionBuilder<TokenTable> builder, IReadOnlyList<UploadTokenViewModel> tokens) =>
    builder.Add(static c => c.Title, "Tokens").Add(static c => c.EmptyDescription, "Create one.").Add(static c => c.Tokens, tokens);

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}

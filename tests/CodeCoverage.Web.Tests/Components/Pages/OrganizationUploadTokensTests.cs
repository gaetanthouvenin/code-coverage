// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationUploadTokensTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Queries.ListOrganizationUploadTokens;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /orgs/{forge}/{org}/upload-tokens (M3.38) against a fake sender.
public sealed class OrganizationUploadTokensTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly List<UploadToken> _tokens = [];

  #endregion

  #region Ctors

  public OrganizationUploadTokensTests()
  {
    Services.AddFakeSenderRunner(_sender);

    _tokens.Add(Token("codecov-org", "cv_c04d", imported: true));
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<UploadToken>>(_tokens));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheTokensOfThatOrganization()
  {
    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Find("[data-panel=tokens] tbody tr").TextContent.ShouldContain("codecov-org");
    cut.Find("[role=tablist], nav").TextContent.ShouldContain(L("Tab_UploadTokens"));
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>.That.Matches(q => q.Login == "acme-labs"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // A repository left the organization while a token was live: it stays valid in its CI secrets until it is revoked.
  [Fact]
  public void Render_RotationAdvised_ShouldShowBanner()
  {
    AdviseRotation(_tokens[0]);

    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Find("[data-state=rotation]").TextContent.ShouldContain(L("OrganizationTokens_RotationTitle"));
  }

  // A revoked token can no longer be used anywhere: it no longer calls for a rotation.
  [Fact]
  public void Render_RotationAdvisedOnARevokedToken_ShouldNotShowBanner()
  {
    AdviseRotation(_tokens[0]);
    _tokens[0].Revoke(Now);

    var cut = Render<OrganizationUploadTokens>(Address());

    cut.FindAll("[data-state=rotation]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_Participant_ShouldBeDenied()
  {
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("OrganizationTokens_Denied"));
  }

  [Fact]
  public void Render_UnknownOrganization_ShouldShowTheInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new NotFoundException(OrganizationErrors.NotFound, "Missing."));

    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  [Fact]
  public void Create_ShouldShowTheValueOnceAndReadTheListAgain()
  {
    A.CallTo(() => _sender.Send(A<CreateOrganizationUploadTokenCommand>._, A<CancellationToken>._))
     .Returns(new ValueTask<IssuedToken>(new IssuedToken(Token("azure-shared", "cv_77ef", imported: false), "d3f1a9e2")));
    var cut = Render<OrganizationUploadTokens>(Address());

    cut.FindAll("[data-panel=tokens] .cc-panel__tools button").Last().Click();
    cut.Find(".cc-dialog input").Input("azure-shared");
    cut.FindAll(".cc-dialog__actions button").Last().Click();

    cut.WaitForAssertion(() => cut.Find(".cc-dialog pre.cc-code").TextContent.ShouldBe("d3f1a9e2"));
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Revoke_ShouldRevokeThatTokenOfThatOrganization()
  {
    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Find("[data-panel=tokens] tbody tr button").Click();
    cut.FindAll(".cc-dialog__actions button").Last().Click();

    A.CallTo(() => _sender.Send(A<RevokeOrganizationUploadTokenCommand>.That.Matches(c => c.Login == "acme-labs"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // A late answer for the first organization never shows its tokens on the second.
  [Fact]
  public void Navigate_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<IReadOnlyList<UploadToken>>();
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>.That.Matches(q => q.Login == "acme-labs"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<IReadOnlyList<UploadToken>>(first.Task));
    A.CallTo(() => _sender.Send(A<ListOrganizationUploadTokensQuery>.That.Matches(q => q.Login == "blue-harbor"), A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<UploadToken>>([Token("blue-token", "cv_0001", imported: false)]));
    var cut = Render<OrganizationUploadTokens>(Address());

    cut.Render(static p => p.Add(static c => c.Login, "blue-harbor"));
    cut.WaitForAssertion(() => cut.Find("[data-panel=tokens] tbody tr").TextContent.ShouldContain("blue-token"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult([Token("codecov-org", "cv_c04d", imported: true)]));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find("[data-panel=tokens] tbody tr").TextContent.ShouldContain("blue-token");
  }

  // The rotation is advised by a bulk update of the repository, never by the entity itself.
  private static void AdviseRotation(UploadToken token) =>
    typeof(UploadToken).GetProperty(nameof(UploadToken.RotationAdvisedAt))!.SetValue(token, Now);

  private static UploadToken Token(string label, string hint, bool imported) =>
    UploadToken.Create(UploadTokenScope.Organization, null, label, hint, new byte[UploadToken.HashLength], imported, "octocat", Now, null);

  private static Action<ComponentParameterCollectionBuilder<OrganizationUploadTokens>> Address(string forge = "github") =>
    p => p.Add(static c => c.Forge, forge).Add(static c => c.Login, "acme-labs");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}

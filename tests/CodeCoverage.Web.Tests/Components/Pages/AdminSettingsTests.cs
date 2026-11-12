// ------------------------------------------------------------------------------------------------
// <copyright file="AdminSettingsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Settings.Commands.ReplaceSecret;
using CodeCoverage.Application.Features.Settings.Commands.ResetSetting;
using CodeCoverage.Application.Features.Settings.Commands.UpdateSetting;
using CodeCoverage.Application.Features.Settings.Queries.GetSettings;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /admin/settings (M3.37) against a fake sender: what the page shows of each setting and what it sends.
public sealed class AdminSettingsTests : UiTestContext
{
  #region Fields

  private const string StoredSecret = "CfDJ8-protected-form-of-the-secret";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Ctors

  public AdminSettingsTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetSettingsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<SettingValue>>(
     [
       Value(SettingDefinitions.GitHubWebhookSecret, StoredSecret, SettingSource.Store).Redact(),
       Value(SettingDefinitions.GitHubWebhookSecretPrevious, null, SettingSource.Default),
       Value(SettingDefinitions.UploadsMaxFilesPerUpload, "60", SettingSource.Store),
       Value(SettingDefinitions.UploadsMaxRequestMegabytes, "300", SettingSource.Environment),
     ]));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldGroupTheSettingsWithTheirSource()
  {
    var cut = Render<AdminSettings>();

    cut.FindAll(".cc-panel__title").Select(static t => t.TextContent).ShouldBe([L("SettingGroup_GitHub"), L("SettingGroup_Uploads")]);
    Row(cut, SettingDefinitions.UploadsMaxFilesPerUpload).TextContent.ShouldContain(L("Setting_SourceDatabase"));
    Row(cut, SettingDefinitions.UploadsMaxRequestMegabytes).TextContent.ShouldContain(L("Setting_SourceEnvironment"));
    cut.FindAll(".cc-pill-tabs a").Count.ShouldBe(4);
  }

  [Fact]
  public void Render_Secret_ShouldNeverShowValue()
  {
    var cut = Render<AdminSettings>();

    cut.Markup.ShouldNotContain(StoredSecret);
    var row = Row(cut, SettingDefinitions.GitHubWebhookSecret);
    row.TextContent.ShouldContain(L("Setting_Defined"));
    row.QuerySelectorAll("input, textarea").ShouldBeEmpty();
    Row(cut, SettingDefinitions.GitHubWebhookSecretPrevious).TextContent.ShouldContain(L("Setting_NotDefined"));
  }

  [Fact]
  public void Render_EnvironmentKey_ShouldBeReadOnly()
  {
    var cut = Render<AdminSettings>();

    var row = Row(cut, SettingDefinitions.UploadsMaxRequestMegabytes);
    row.QuerySelector("input")!.HasAttribute("readonly").ShouldBeTrue();
    row.QuerySelector(".cc-setting__btn--lock").ShouldNotBeNull();
    row.QuerySelectorAll("button").ShouldBeEmpty();
  }

  [Fact]
  public void Save_ShouldSendTheValueAndReadTheSettingsAgain()
  {
    var cut = Render<AdminSettings>();
    var row = Row(cut, SettingDefinitions.UploadsMaxFilesPerUpload);

    row.QuerySelector("input")!.Input("70");
    cut.Find($"[data-key='{SettingDefinitions.UploadsMaxFilesPerUpload.ToLowerInvariant()}'] .cc-setting__btn--save").Click();

    A.CallTo(() => _sender.Send(A<UpdateSettingCommand>.That.Matches(c => c.Key == SettingDefinitions.UploadsMaxFilesPerUpload && c.Value == "70"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<GetSettingsQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Save_RefusedValue_ShouldShowTheErrorUnderTheField()
  {
    A.CallTo(() => _sender.Send(A<UpdateSettingCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new ValidationException([new FluentValidation.Results.ValidationFailure("Value", "Too large.") { ErrorCode = "Setting.OutOfRange" }]));
    var cut = Render<AdminSettings>();

    cut.Find($"[data-key='{SettingDefinitions.UploadsMaxFilesPerUpload.ToLowerInvariant()}'] .cc-setting__btn--save").Click();

    cut.Find($"[data-key='{SettingDefinitions.UploadsMaxFilesPerUpload.ToLowerInvariant()}'] .cc-field__error").ShouldNotBeNull();
  }

  // Decision 19: a stored secret can be cleared, e.g. the previous webhook secret after a rotation.
  [Fact]
  public void Clear_StoredSecret_ShouldResetIt()
  {
    var cut = Render<AdminSettings>();

    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Clear")).Click();

    A.CallTo(() => _sender.Send(A<ResetSettingCommand>.That.Matches(c => c.Key == SettingDefinitions.GitHubWebhookSecret), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public void Replace_ShouldSendTheSecretThenForgetIt()
  {
    var cut = Render<AdminSettings>();

    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Replace")).Click();
    cut.Find("[role=dialog] input[type=password]").Input("new-secret");
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    A.CallTo(() => _sender.Send(A<ReplaceSecretCommand>.That.Matches(c => c.Key == SettingDefinitions.GitHubWebhookSecret && c.Value == "new-secret"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    cut.FindAll("[role=dialog]").ShouldBeEmpty();
    cut.Markup.ShouldNotContain("new-secret");

    // Review R9: opened again, the dialog has forgotten the value.
    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Replace")).Click();
    (cut.Find("[role=dialog] input[type=password]").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
  }

  // Second review S2: refused, the focused Replace button is disabled; the focus goes back into the dialog, to its field.
  [Fact]
  public void Replace_Refused_ShouldPutTheFocusBackInTheDialog()
  {
    A.CallTo(() => _sender.Send(A<ReplaceSecretCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new ValidationException([new FluentValidation.Results.ValidationFailure("Value", "Too long.") { ErrorCode = "Setting.OutOfRange" }]));
    var cut = Render<AdminSettings>();

    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Replace")).Click();
    cut.Find("[role=dialog] input[type=password]").Input("refused-secret");
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    JSInterop.VerifyInvoke("ccUi.focusDialog");
  }

  // Review R9: a refused value is not kept for another try either.
  [Fact]
  public void Replace_Refused_ShouldForgetTheValueAndKeepTheDialogOpen()
  {
    A.CallTo(() => _sender.Send(A<ReplaceSecretCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new ValidationException([new FluentValidation.Results.ValidationFailure("Value", "Too long.") { ErrorCode = "Setting.OutOfRange" }]));
    var cut = Render<AdminSettings>();

    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Replace")).Click();
    cut.Find("[role=dialog] input[type=password]").Input("refused-secret");
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    cut.Find("[role=dialog]").ShouldNotBeNull();
    (cut.Find("[role=dialog] input[type=password]").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
    cut.Markup.ShouldNotContain("refused-secret");
  }

  [Fact]
  public void Search_ShouldKeepTheMatchingKeys()
  {
    var cut = Render<AdminSettings>();

    cut.Find(".cc-search input").Input("maxfiles");

    cut.FindAll(".cc-setting").Select(static r => r.GetAttribute("data-key")).ShouldBe([SettingDefinitions.UploadsMaxFilesPerUpload.ToLowerInvariant()]);
  }

  // The server stays the authority: refused, the page says so instead of a toast.
  [Fact]
  public void Render_NotAnAdministrator_ShouldShowTheDeniedState()
  {
    A.CallTo(() => _sender.Send(A<GetSettingsQuery>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<AdminSettings>();

    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminSettings_Denied"));
    cut.FindAll(".cc-setting").ShouldBeEmpty();
  }

  // Review R8: a write the server refuses with 403 switches the page to its denied state, never silently.
  [Fact]
  public void Save_Forbidden_ShouldShowTheDeniedState()
  {
    A.CallTo(() => _sender.Send(A<UpdateSettingCommand>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));
    var cut = Render<AdminSettings>();

    cut.Find($"[data-key='{SettingDefinitions.UploadsMaxFilesPerUpload.ToLowerInvariant()}'] .cc-setting__btn--save").Click();

    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminSettings_Denied"));
  }

  [Fact]
  public void Toggle_Forbidden_ShouldShowTheDeniedState()
  {
    A.CallTo(() => _sender.Send(A<GetSettingsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<IReadOnlyList<SettingValue>>([Value(SettingDefinitions.CoverageAllowFilenameOnlyMatch, "false", SettingSource.Default)]));
    A.CallTo(() => _sender.Send(A<UpdateSettingCommand>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));
    var cut = Render<AdminSettings>();

    cut.Find("[role=switch]").Click();

    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminSettings_Denied"));
  }

  [Fact]
  public void Replace_Forbidden_ShouldCloseTheDialogAndShowTheDeniedState()
  {
    A.CallTo(() => _sender.Send(A<ReplaceSecretCommand>._, A<CancellationToken>._)).ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));
    var cut = Render<AdminSettings>();

    Row(cut, SettingDefinitions.GitHubWebhookSecret).QuerySelectorAll("button").Single(b => b.TextContent == L("Action_Replace")).Click();
    cut.Find("[role=dialog] input[type=password]").Input("new-secret");
    cut.FindAll("[role=dialog] .cc-dialog__actions button").Last().Click();

    cut.FindAll("[role=dialog]").ShouldBeEmpty();
    cut.Find(".cc-status-page").TextContent.ShouldContain(L("AdminSettings_Denied"));
  }

  private static SettingValue Value(string key, string? value, SettingSource source) =>
    SettingValue.Create(SettingDefinitions.Find(key)!, value, source, source is SettingSource.Store ? Now : null, null);

  private static AngleSharp.Dom.IElement Row(IRenderedComponent<AdminSettings> cut, string key) => cut.Find($"[data-key='{key.ToLowerInvariant()}']");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}

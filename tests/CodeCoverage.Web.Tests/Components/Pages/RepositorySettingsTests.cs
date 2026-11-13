// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySettingsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.ActivateRepository;
using CodeCoverage.Application.Features.Repositories.Commands.DeactivateRepository;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositoryPermission;
using CodeCoverage.Application.Features.Repositories.Queries.GetRepositorySettings;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Repositories;

using DomainRepositorySettings = CodeCoverage.Domain.Repositories.RepositorySettings;
using CodeCoverage.Web.Components.Pages;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Pages;

// /{forge}/{owner}/{repo}/settings (M3.34) against a fake sender.
public sealed class RepositorySettingsTests : UiTestContext
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("e4f5a6b" + new string('1', GitSha.Length - 7));

  private const string Yaml = "coverage:\n  precision: 1\n";

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly TrackedRepository _repository = TrackedRepository.Create(5, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);

  #endregion

  #region Ctors

  public RepositorySettingsTests()
  {
    Services.AddFakeSenderRunner(_sender);

    A.CallTo(() => _sender.Send(A<GetRepositoryPermissionQuery>._, A<CancellationToken>._)).Returns(new ValueTask<bool>(true));
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<DomainRepositorySettings>(new DomainRepositorySettings(_repository, Config())));
  }

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldShowTheStateAndTheYamlOfTheRepository()
  {
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=activation] [role=switch]").GetAttribute("aria-checked").ShouldBe("true");
    cut.Find("[data-panel=activation]").TextContent.ShouldContain(L("Repository_Active"));
    cut.Find("[data-panel=config] pre.cc-code").TextContent.ShouldBe(Yaml);
    cut.Find("[data-panel=config]").TextContent.ShouldContain(L("ConfigSource_Commit"));
    cut.Find("[data-panel=config]").TextContent.ShouldContain(L("RepositorySettings_ConfigValid"));
    cut.Find("[data-panel=config] code").TextContent.ShouldBe(".codecov.yml");
  }

  // A repository an administrator disabled stays reachable, and its switch shows the state of the server.
  [Fact]
  public void Render_DisabledRepository_ShouldShowItInactive()
  {
    _repository.Disable(Now);

    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=activation] [role=switch]").GetAttribute("aria-checked").ShouldBe("false");
    cut.Find("[data-panel=activation]").TextContent.ShouldContain(L("Repository_Inactive"));
  }

  [Fact]
  public void Render_Reader_ShouldShowAccessDenied()
  {
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));

    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-state=denied]").TextContent.ShouldContain(L("RepositorySettings_Denied"));
    cut.FindAll("[data-panel=activation]").ShouldBeEmpty();
  }

  [Fact]
  public void Render_UnknownRepository_ShouldShowTheInaccessibleState()
  {
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._))
     .ThrowsAsync(new NotFoundException(RepositoryErrors.NotFound, "Missing."));

    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-state=missing]").TextContent.ShouldContain(L("Inaccessible_Title"));
  }

  // A forge segment the console does not serve is an unknown address, never a repository of GitHub.
  [Fact]
  public void Render_UnknownForge_ShouldShowTheInaccessibleStateWithoutSending()
  {
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address("gitlob"));

    cut.Find("[data-state=missing]").ShouldNotBeNull();
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public void Render_InvalidConfiguration_ShouldShowItsErrors()
  {
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<DomainRepositorySettings>(new DomainRepositorySettings(
       _repository,
       new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Commit, Sha, ".codecov.yml", Yaml, [new ConfigWarning(ConfigWarning.InvalidValue, "coverage.round", "nearest-ish")], ["Line 12: unknown value."]))));

    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    var panel = cut.Find("[data-panel=config]");
    panel.TextContent.ShouldContain(L("RepositorySettings_ConfigInvalid"));
    panel.TextContent.ShouldContain("Line 12: unknown value.");
    panel.TextContent.ShouldContain("coverage.round");
    cut.FindAll("[data-panel=config] .cc-banner [role=alert], [data-panel=config] [role=alert]").ShouldNotBeEmpty();
  }

  // Without any configuration file, the built-in defaults apply and no code block is shown.
  [Fact]
  public void Render_NoConfigurationFile_ShouldSayTheDefaultsApply()
  {
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._))
     .Returns(new ValueTask<DomainRepositorySettings>(new DomainRepositorySettings(
       _repository,
       new EffectiveConfig(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Builtin, null, null, null, [], []))));

    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=config] [data-state=noyaml]").TextContent.ShouldContain(L("RepositorySettings_ConfigNone"));
    cut.FindAll("[data-panel=config] pre.cc-code").ShouldBeEmpty();
  }

  [Fact]
  public void Toggle_ShouldDeactivateAndReadTheStateAgain()
  {
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=activation] [role=switch]").Click();

    A.CallTo(() => _sender.Send(A<DeactivateRepositoryCommand>.That.Matches(c => c.Owner == "octo-org" && c.Name == "app"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Fact]
  public void Toggle_Disabled_ShouldActivateAgain()
  {
    _repository.Disable(Now);
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=activation] [role=switch]").Click();

    A.CallTo(() => _sender.Send(A<ActivateRepositoryCommand>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A refused change leaves the switch where the server says it is.
  [Fact]
  public void Toggle_Refused_ShouldKeepTheStateOfTheServer()
  {
    A.CallTo(() => _sender.Send(A<DeactivateRepositoryCommand>._, A<CancellationToken>._))
     .ThrowsAsync(new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."));
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Find("[data-panel=activation] [role=switch]").Click();

    cut.Find("[data-panel=activation] [role=switch]").GetAttribute("aria-checked").ShouldBe("true");
  }

  // A navigation to another repository while the first load is pending: the late answer of the first is dropped.
  [Fact]
  public void Navigate_FirstLoadAnsweringLast_ShouldShowTheSecond()
  {
    var first = new TaskCompletionSource<DomainRepositorySettings>();
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>.That.Matches(q => q.Name == "app"), A<CancellationToken>._))
     .ReturnsLazily(() => new ValueTask<DomainRepositorySettings>(first.Task));
    var other = TrackedRepository.Create(5, "2003", RepositorySlug.Parse("octo-org/other"), "main", true, "protected", Now);
    other.Disable(Now);
    A.CallTo(() => _sender.Send(A<GetRepositorySettingsQuery>.That.Matches(q => q.Name == "other"), A<CancellationToken>._))
     .Returns(new ValueTask<DomainRepositorySettings>(new DomainRepositorySettings(other, Config())));
    var cut = Render<Web.Components.Pages.RepositorySettings>(Address());

    cut.Render(static p => p.Add(static c => c.Name, "other"));
    cut.WaitForAssertion(() => cut.Find("[data-panel=activation] [role=switch]").GetAttribute("aria-checked").ShouldBe("false"));
    var renders = cut.RenderCount;
    cut.InvokeAsync(() => first.SetResult(new DomainRepositorySettings(_repository, Config())));

    cut.WaitForState(() => cut.RenderCount > renders);
    cut.Find("[data-panel=activation] [role=switch]").GetAttribute("aria-checked").ShouldBe("false");
  }

  private static EffectiveConfig Config() =>
    new(CoverageConfig.Create(ConfigNode.FromMap()), ConfigSource.Commit, Sha, ".codecov.yml", Yaml, [], []);

  private static Action<ComponentParameterCollectionBuilder<Web.Components.Pages.RepositorySettings>> Address(string forge = "github") =>
    p => p.Add(static c => c.Forge, forge).Add(static c => c.Owner, "octo-org").Add(static c => c.Name, "app");

  private string L(string key) => Services.GetRequiredService<IStringLocalizer<Common>>()[key];

  #endregion
}

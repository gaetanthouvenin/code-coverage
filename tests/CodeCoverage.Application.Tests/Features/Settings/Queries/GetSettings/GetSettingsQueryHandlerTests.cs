// ------------------------------------------------------------------------------------------------
// <copyright file="GetSettingsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Settings.Queries.GetSettings;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

using static CodeCoverage.Application.Tests.Features.Settings.Support.SettingsStoreFakes;

namespace CodeCoverage.Application.Tests.Features.Settings.Queries.GetSettings;

public sealed class GetSettingsQueryHandlerTests
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldNeverReturnSecretValue()
  {
    var webhookSecret = Definition(SettingDefinitions.GitHubWebhookSecret);
    var clientSecret = Definition(SettingDefinitions.GitHubClientSecret);
    var appId = Definition(SettingDefinitions.GitHubAppId);
    var store = A.Fake<ISettingsStore>();
    A.CallTo(() => store.ListAsync(Ct))
     .Returns(
       new[]
       {
         SettingValue.Create(appId, "12345", SettingSource.Store, DateTimeOffset.UtcNow, "octocat"),
         SettingValue.Create(webhookSecret, "CfDJ8-protected", SettingSource.Store, DateTimeOffset.UtcNow, "octocat"),
         SettingValue.Create(clientSecret, null, SettingSource.Default, null, null),
       });

    var values = await new GetSettingsQueryHandler(store).Handle(new GetSettingsQuery(), Ct);

    values.Count.ShouldBe(3);
    values.Where(v => v.Definition.IsSecret).ShouldAllBe(v => v.Value == null);
    values.ShouldNotContain(v => v.Value == "CfDJ8-protected");
    values.Single(v => v.Key == webhookSecret.Key).IsDefined.ShouldBeTrue();
    values.Single(v => v.Key == clientSecret.Key).IsDefined.ShouldBeFalse();
    values.Single(v => v.Key == appId.Key).Value.ShouldBe("12345");
  }

  [Fact]
  public void Query_ShouldBeAdminOnly()
  {
    new GetSettingsQuery().ShouldBeAssignableTo<IAdminOnly>();
  }

  #endregion
}

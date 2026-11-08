// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsStoreFakes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Settings.Support;

internal static class SettingsStoreFakes
{
  #region Methods

  public static ISettingsStore Returning(SettingDefinition definition, string? value, SettingSource source)
  {
    var store = A.Fake<ISettingsStore>();
    A.CallTo(() => store.GetAsync(definition, A<CancellationToken>._))
     .Returns(SettingValue.Create(definition, value, source, null, null));

    return store;
  }

  public static SettingDefinition Definition(string key)
  {
    return SettingDefinitions.Find(key)!;
  }

  #endregion
}

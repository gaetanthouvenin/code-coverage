// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigNodes.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Tests.Support;

// Short forms to write a configuration in a test.
internal static class ConfigNodes
{
  #region Methods

  internal static ConfigNode Map(params (string Key, ConfigNode Value)[] entries) => ConfigNode.FromMap(entries);

  internal static ConfigNode List(params ConfigNode[] items) => ConfigNode.FromList(items);

  internal static ConfigNode Texts(params string[] items) => ConfigNode.FromList(items.Select(ConfigNode.FromText));

  internal static ConfigNode Text(string value) => ConfigNode.FromText(value);

  internal static ConfigNode Number(decimal value) => ConfigNode.FromNumber(value);

  internal static ConfigNode Bool(bool value) => ConfigNode.FromBoolean(value);

  #endregion
}

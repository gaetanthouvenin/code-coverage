// ------------------------------------------------------------------------------------------------
// <copyright file="VerifyConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace CodeCoverage.Api.Tests.Support;

// The snapshots are compared byte for byte and kept LF, without a byte order mark (see .gitattributes).
internal static class VerifyConfiguration
{
  #region Methods

  [ModuleInitializer]
  public static void Initialize()
  {
    VerifierSettings.UseUtf8NoBom();
  }

  #endregion
}

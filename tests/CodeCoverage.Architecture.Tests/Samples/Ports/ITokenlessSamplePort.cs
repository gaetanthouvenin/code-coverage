// ------------------------------------------------------------------------------------------------
// <copyright file="ITokenlessSamplePort.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Ports;

// Breaks the port rule on cancellation: no token, then a token with a default value.
internal interface ITokenlessSamplePort
{
  #region Methods

  Task SaveAsync(string name);

  ValueTask<int> CountAsync(CancellationToken cancellationToken = default);

  #endregion
}

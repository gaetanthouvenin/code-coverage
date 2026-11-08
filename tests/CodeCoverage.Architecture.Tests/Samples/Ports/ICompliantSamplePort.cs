// ------------------------------------------------------------------------------------------------
// <copyright file="ICompliantSamplePort.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Ports;

// Follows every port rule: Domain results, mandatory token, saved at once.
internal interface ICompliantSamplePort
{
  #region Properties

  DateTimeOffset UtcNow { get; }

  #endregion

  #region Methods

  Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

  Task AddAsync(string name, CancellationToken cancellationToken);

  IAsyncEnumerable<string> StreamAsync(CancellationToken cancellationToken);

  string Hash(string value);

  #endregion
}

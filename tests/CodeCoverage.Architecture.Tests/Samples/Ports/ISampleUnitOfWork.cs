// ------------------------------------------------------------------------------------------------
// <copyright file="ISampleUnitOfWork.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Ports;

// Breaks the port rule on persistence: a unit of work the handlers would have to save.
internal interface ISampleUnitOfWork
{
  #region Methods

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);

  #endregion
}

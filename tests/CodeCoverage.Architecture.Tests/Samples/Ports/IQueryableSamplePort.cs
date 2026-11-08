// ------------------------------------------------------------------------------------------------
// <copyright file="IQueryableSamplePort.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Ports;

// Breaks the port rule on IQueryable, as a result and as a property. Never implemented.
internal interface IQueryableSamplePort
{
  #region Properties

  IQueryable<string> Names { get; }

  #endregion

  #region Methods

  Task<IOrderedQueryable<string>> SearchAsync(string text, CancellationToken cancellationToken);

  #endregion
}

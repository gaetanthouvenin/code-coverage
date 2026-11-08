// ------------------------------------------------------------------------------------------------
// <copyright file="ConstructedSenderComponent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Components;

// Breaks the sender rule: it receives a mediator through its constructor.
internal sealed class ConstructedSenderComponent(IMediator mediator)
{
  #region Methods

  public override string ToString() => mediator.GetType().Name;

  #endregion
}

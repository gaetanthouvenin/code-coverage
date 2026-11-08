// ------------------------------------------------------------------------------------------------
// <copyright file="InjectedSenderComponent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Components;

// Breaks the sender rule: like an [Inject] ISender, it holds the sender in a property.
internal sealed class InjectedSenderComponent
{
  #region Properties

  public ISender? Sender { get; set; }

  #endregion
}

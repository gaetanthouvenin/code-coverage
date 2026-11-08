// ------------------------------------------------------------------------------------------------
// <copyright file="RunnerComponent.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Components;

// Follows the sender rule: it only writes the request it hands to the runner, which supplies the
// sender (the lambda names ISender, the component holds none).
internal sealed class RunnerComponent
{
  #region Methods

  public static Func<ISender, CancellationToken, Task<string>> Request() =>
    (sender, _) => Task.FromResult(sender.GetType().Name);

  #endregion
}

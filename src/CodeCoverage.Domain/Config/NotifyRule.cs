// ------------------------------------------------------------------------------------------------
// <copyright file="NotifyRule.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>When the notifications of a commit are sent (<c>codecov.notify</c>).</summary>
/// <param name="AfterNBuilds">The number of uploads the notifications wait for.</param>
/// <param name="WaitForCi">A value indicating whether they wait for the CI (read, without effect).</param>
/// <param name="ManualTrigger">A value indicating whether they wait for an explicit trigger.</param>
/// <param name="NotifyError">A value indicating whether a processing error replaces the comment.</param>
public sealed record NotifyRule(int AfterNBuilds, bool WaitForCi, bool ManualTrigger, bool NotifyError);

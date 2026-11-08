// ------------------------------------------------------------------------------------------------
// <copyright file="TracingPingQueryValidator.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;
using FluentValidation.Results;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Application.Tests.Behaviors.Support;

// A validator of the ping query that records its run in the trace and fails when asked to.
internal sealed class TracingPingQueryValidator : AbstractValidator<PingQuery>
{
  #region Ctors

  public TracingPingQueryValidator(PipelineTrace trace, bool fail)
  {
    RuleFor(q => q).Custom((_, context) =>
    {
      trace.Steps.Add("Validation");

      if (fail)
      {
        context.AddFailure(new ValidationFailure("Probe", "The probe refuses the query."));
      }
    });
  }

  #endregion
}

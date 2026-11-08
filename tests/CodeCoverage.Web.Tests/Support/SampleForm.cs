// ------------------------------------------------------------------------------------------------
// <copyright file="SampleForm.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Tests.Support;

// The model of a sample form, whose fields receive the messages of a validation failure.
public sealed class SampleForm
{
  #region Properties

  public string? Name { get; set; }

  #endregion
}

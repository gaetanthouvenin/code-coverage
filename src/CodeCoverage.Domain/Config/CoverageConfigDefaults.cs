// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageConfigDefaults.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>The built-in configuration, which is the lowest layer of the merge.</summary>
public static class CoverageConfigDefaults
{
  #region Properties

  /// <summary>Gets the built-in configuration.</summary>
  /// <remarks>
  ///   <c>project</c> and <c>patch</c> stay <see langword="true" /> rather than a map: a map would be
  ///   merged with the entries of the repository and keep a <c>default</c> status it did not ask for.
  /// </remarks>
  public static ConfigNode Root { get; } = Build();

  #endregion

  #region Methods

  private static ConfigNode Build()
  {
    var no = ConfigNode.FromBoolean(false);
    var yes = ConfigNode.FromBoolean(true);

    return ConfigNode.FromMap(
      ("codecov", ConfigNode.FromMap(
        ("require_ci_to_pass", no),
        ("notify", ConfigNode.FromMap(
          ("wait_for_ci", no),
          ("after_n_builds", ConfigNode.FromNumber(0)),
          ("manual_trigger", no))),
        ("max_report_age", no))),
      ("coverage", ConfigNode.FromMap(
        ("precision", ConfigNode.FromNumber(2)),
        ("round", ConfigNode.FromText("down")),
        ("range", ConfigNode.FromList(ConfigNode.FromNumber(60), ConfigNode.FromNumber(80))),
        ("status", ConfigNode.FromMap(
          ("project", yes),
          ("patch", yes),
          ("changes", no),
          ("default_rules", ConfigNode.FromMap(
            ("flag_coverage_not_uploaded_behavior", ConfigNode.FromText("include")))))))),
      ("comment", ConfigNode.FromMap(
        ("layout", ConfigNode.FromText("header, diff, flags, files, footer")),
        ("behavior", ConfigNode.FromText("default")),
        ("require_changes", no),
        ("show_carryforward_flags", no))),
      ("parsers", ConfigNode.FromMap(
        ("cobertura", PartialsAsMisses(no)),
        ("lcov", PartialsAsMisses(no)),
        ("jacoco", PartialsAsMisses(no)))));
  }

  private static ConfigNode PartialsAsMisses(ConfigNode no) =>
    ConfigNode.FromMap(("partials_as_hits", no));

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="CcIconPaths.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Frozen;

namespace CodeCoverage.Web.Components.Ui.Icons;

/// <summary>The SVG markup drawn inside the <c>&lt;svg&gt;</c> of each <see cref="CcIconName" />.</summary>
/// <remarks>The paths come from the validated mockup (<c>docs/design/ui-mockup.html</c>).</remarks>
internal static class CcIconPaths
{
  #region Fields

  private static readonly FrozenDictionary<CcIconName, string> Paths =
    new Dictionary<CcIconName, string>
    {
      [CcIconName.Repo] = """<path d="M5 4h11a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5z"/><path d="M5 16h13"/><path d="M9 4v12"/>""",
      [CcIconName.Commit] = """<circle cx="12" cy="12" r="3"/><path d="M12 3v6M12 15v6"/>""",
      [CcIconName.PullRequest] = """<circle cx="6" cy="18" r="2"/><circle cx="6" cy="6" r="2"/><circle cx="18" cy="18" r="2"/><path d="M6 8v8M11 6h5a2 2 0 0 1 2 2v8"/><path d="m14 9-3-3 3-3"/>""",
      [CcIconName.Gear] = """<path d="M10.3 4.3c.4-1.8 3-1.8 3.4 0a1.7 1.7 0 0 0 2.6 1.1c1.5-.9 3.3.8 2.4 2.4a1.7 1.7 0 0 0 1 2.5c1.8.5 1.8 3 0 3.5a1.7 1.7 0 0 0-1 2.5c.9 1.6-.9 3.3-2.4 2.4a1.7 1.7 0 0 0-2.6 1.1c-.4 1.8-3 1.8-3.4 0a1.7 1.7 0 0 0-2.6-1.1c-1.5.9-3.3-.8-2.4-2.4a1.7 1.7 0 0 0-1-2.5c-1.8-.5-1.8-3 0-3.5a1.7 1.7 0 0 0 1-2.5c-.9-1.6.9-3.3 2.4-2.4a1.7 1.7 0 0 0 2.6-1.1z"/><circle cx="12" cy="12" r="3"/>""",
      [CcIconName.Key] = """<circle cx="8" cy="15" r="4"/><path d="m10.85 12.15 7.65-7.65M18 5l2 2M15 8l2 2"/>""",
      [CcIconName.User] = """<circle cx="12" cy="8" r="4"/><path d="M6 21v-2a4 4 0 0 1 4-4h4a4 4 0 0 1 4 4v2"/>""",
      [CcIconName.Shield] = """<path d="M12 3a12 12 0 0 0 8.5 3A12 12 0 0 1 12 21 12 12 0 0 1 3.5 6 12 12 0 0 0 12 3"/>""",
      [CcIconName.Activity] = """<path d="M3 12h4l3 8 4-16 3 8h4"/>""",
      [CcIconName.List] = """<path d="M9 6h11M9 12h11M9 18h11M5 6v.01M5 12v.01M5 18v.01"/>""",
      [CcIconName.Search] = """<circle cx="10" cy="10" r="7"/><path d="m21 21-6-6"/>""",
      [CcIconName.External] = """<path d="M12 6H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-6M11 13l9-9M15 4h5v5"/>""",
      [CcIconName.Refresh] = """<path d="M20 11A8.1 8.1 0 0 0 4.5 9M4 5v4h4M4 13a8.1 8.1 0 0 0 15.5 2m.5 4v-4h-4"/>""",
      [CcIconName.Undo] = """<path d="M9 14 4 9l5-5"/><path d="M4 9h10.5a5.5 5.5 0 0 1 0 11H11"/>""",
      [CcIconName.Check] = """<path d="m5 12 5 5L20 7"/>""",
      [CcIconName.Close] = """<path d="M18 6 6 18M6 6l12 12"/>""",
      [CcIconName.Alert] = """<path d="M12 9v4M12 17h.01"/><path d="M10.24 3.96 2.82 17a2 2 0 0 0 1.76 3h14.84a2 2 0 0 0 1.76-3L13.76 3.96a2 2 0 0 0-3.52 0z"/>""",
      [CcIconName.Info] = """<circle cx="12" cy="12" r="9"/><path d="M12 8h.01M11 12h1v4h1"/>""",
      [CcIconName.Chart] = """<path d="M4 19h16M4 15l4-6 4 2 4-5 4 4"/>""",
      [CcIconName.File] = """<path d="M14 3v4a1 1 0 0 0 1 1h4"/><path d="M17 21H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h7l5 5v11a2 2 0 0 1-2 2"/>""",
      [CcIconName.Folder] = """<path d="M5 4h4l3 3h7a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2"/>""",
      [CcIconName.Copy] = """<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/>""",
      [CcIconName.Plus] = """<path d="M12 5v14M5 12h14"/>""",
      [CcIconName.Menu] = """<path d="M4 6h16M4 12h16M4 18h16"/>""",
      [CcIconName.Moon] = """<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9"/>""",
      [CcIconName.Upload] = """<path d="M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2M7 9l5-5 5 5M12 4v12"/>""",
      [CcIconName.Tag] = """<path d="M7.5 7.5h.01"/><path d="M3 6v5.17a2 2 0 0 0 .59 1.42l7.71 7.71a2.41 2.41 0 0 0 3.4 0l5.6-5.6a2.41 2.41 0 0 0 0-3.4L12.6 3.59A2 2 0 0 0 11.17 3H6a3 3 0 0 0-3 3"/>""",
      [CcIconName.Clock] = """<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 3"/>""",
      [CcIconName.ArrowDown] = """<path d="M12 5v14M18 13l-6 6-6-6"/>""",
      [CcIconName.Rocket] = """<path d="M4 13a8 8 0 0 1 7 7 6 6 0 0 0 3-5 9 9 0 0 0 6-8 3 3 0 0 0-3-3 9 9 0 0 0-8 6 6 6 0 0 0-5 3"/><path d="M7 14a6 6 0 0 0-3 6 6 6 0 0 0 6-3"/><circle cx="15" cy="9" r="1"/>""",
      [CcIconName.GitHub] = """<path d="M9 19c-4.3 1.4-4.3-2.5-6-3m12 5v-3.5c0-1 .1-1.4-.5-2 2.8-.3 5.5-1.4 5.5-6a4.6 4.6 0 0 0-1.3-3.2 4.2 4.2 0 0 0-.1-3.2s-1.1-.3-3.5 1.3a12.3 12.3 0 0 0-6.2 0C6.5 2.8 5.4 3.1 5.4 3.1a4.2 4.2 0 0 0-.1 3.2A4.6 4.6 0 0 0 4 9.5c0 4.6 2.7 5.7 5.5 6-.6.6-.6 1.2-.5 2V21"/>""",
      [CcIconName.Eye] = """<circle cx="12" cy="12" r="2"/><path d="M22 12c-2.67 4.67-6 7-10 7s-7.33-2.33-10-7c2.67-4.67 6-7 10-7s7.33 2.33 10 7"/>""",
      [CcIconName.ChevronDown] = """<path d="m6 9 6 6 6-6"/>""",
      [CcIconName.Bell] = """<path d="M10 5a2 2 0 1 1 4 0 7 7 0 0 1 4 6v3a4 4 0 0 0 2 3H4a4 4 0 0 0 2-3v-3a7 7 0 0 1 4-6"/><path d="M9 17v1a3 3 0 0 0 6 0v-1"/>""",
      [CcIconName.Filter] = """<path d="M4 6h8M16 6h4M4 12h2M10 12h10M4 18h11M19 18h1"/><circle cx="14" cy="6" r="2"/><circle cx="8" cy="12" r="2"/><circle cx="17" cy="18" r="2"/>""",
      [CcIconName.Dots] = """<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>""",
      [CcIconName.Export] = """<path d="M12 3v12M8 7l4-4 4 4"/><path d="M5 12v6a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-6"/>""",
      [CcIconName.Layers] = """<path d="m12 4-8 4 8 4 8-4-8-4"/><path d="m4 12 8 4 8-4M4 16l8 4 8-4"/>""",
      [CcIconName.Target] = """<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>""",
      [CcIconName.Book] = """<path d="M3 19a9 9 0 0 1 9 0 9 9 0 0 1 9 0M3 6a9 9 0 0 1 9 0 9 9 0 0 1 9 0M3 6v13M12 6v13M21 6v13"/>""",
      [CcIconName.Lines] = """<path d="M4 6h16M4 10h10M4 14h16M4 18h7"/>""",
      [CcIconName.Sort] = """<path d="m3 9 4-4 4 4M7 5v14M21 15l-4 4-4-4M17 19V5"/>""",
      [CcIconName.Sun] = """<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>""",
    }.ToFrozenDictionary();

  #endregion

  #region Methods

  /// <summary>Gets the SVG markup of an icon.</summary>
  /// <param name="name">The icon.</param>
  /// <returns>The <c>path</c>, <c>circle</c> and <c>rect</c> elements of the icon; empty for an unknown value.</returns>
  public static string Get(CcIconName name) =>
    Paths.GetValueOrDefault(name, string.Empty);

  #endregion
}

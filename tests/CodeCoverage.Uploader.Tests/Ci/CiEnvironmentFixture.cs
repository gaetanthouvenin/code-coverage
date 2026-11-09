// ------------------------------------------------------------------------------------------------
// <copyright file="CiEnvironmentFixture.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace CodeCoverage.Uploader.Tests.Ci;

internal sealed class CiEnvironmentFixture
{
  #region Fields

  private readonly Dictionary<string, string> _variables;
  private string _event;

  #endregion

  #region Ctors

  private CiEnvironmentFixture(Dictionary<string, string> variables, string @event)
  {
    _variables = variables;
    _event = @event;
  }

  #endregion

  #region Methods

  public static CiEnvironmentFixture Load(string name)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "ci-env", name);

    using var document = JsonDocument.Parse(File.ReadAllText(path));

    var variables = document.RootElement
                            .GetProperty("environment")
                            .EnumerateObject()
                            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
    var @event = document.RootElement.TryGetProperty("event", out var payload)
                   ? payload.GetRawText()
                   : string.Empty;

    return new CiEnvironmentFixture(variables, @event);
  }

  public CiEnvironmentFixture With(string name, string value)
  {
    _variables[name] = value;

    return this;
  }

  public CiEnvironmentFixture WithEvent(string payload)
  {
    _event = payload;

    return this;
  }

  public string? Environment(string name)
  {
    return _variables.GetValueOrDefault(name);
  }

  public string ReadFile(string path)
  {
    return path == _variables["GITHUB_EVENT_PATH"]
             ? _event
             : throw new FileNotFoundException("Unexpected file.", path);
  }

  #endregion
}

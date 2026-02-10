using System.Text;
using Urban.Application.Logging.Interfaces;

namespace Urban.Application.Logging;

public class GeoLogger(string? testName = null) : IGeoLogger, IDisposable
{
    private readonly StringBuilder _htmlBuilder = new();
    private readonly bool _isTestMode = !string.IsNullOrEmpty(testName);
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _lockObject = new();

    public void LogSvg(params (object geo, string style)[] layers)
    {
        var svg = SvgTools.ComposeSvgString(_isTestMode ? 400 : 300, _isTestMode ? 400 : 300, layers);
        AppendToHtml($"<div style='display:inline-block;'>{svg}</div>");
    }

    public void LogSvg(string message, params (object geo, string style)[] layers)
    {
        AppendToHtml($"<br/><div style='display: inline-block;'>{message}:</div><br/>");
        LogSvg(layers);
    }

    public void LogMessage(string message)
    {
        AppendToHtml($"<div style='display:inline-block; white-space:pre; font-family:monospace;'>{message}</div>");
    }

    public string GetHtml()
    {
        lock (_lockObject)
            return _isTestMode ? $"<html><body>{_htmlBuilder}</body></html>" : _htmlBuilder.ToString();
    }

    private void AppendToHtml(string htmlFragment)
    {
        lock (_lockObject)
            _htmlBuilder.AppendLine(htmlFragment);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
namespace Urban.Application.Logging.Interfaces;

public interface IGeoLogger
{
    void LogSvg(params (object geo, string style)[] layers);
    void LogSvg(string message, params (object geo, string style)[] layers);
    void LogMessage(string message);
    public string GetHtml();
}
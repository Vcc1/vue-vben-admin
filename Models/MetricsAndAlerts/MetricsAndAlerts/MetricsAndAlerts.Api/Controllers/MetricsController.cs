using MetricsAndAlertsApp;
using Microsoft.AspNetCore.Mvc;

namespace MetricsAndAlerts.Controllers;

[ApiController]
public sealed class MetricsController : ControllerBase
{
    readonly IUrlMonitorMetricExporter _exporter;

    public MetricsController(IUrlMonitorMetricExporter exporter)
    {
        _exporter = exporter;
    }

    [HttpGet("/metrics")]
    public ContentResult Get()
    {
        return Content(_exporter.Export(), "text/plain; version=0.0.4; charset=utf-8");
    }
}

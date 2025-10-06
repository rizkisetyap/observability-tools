namespace ratings.Models;

public class OpenTelemetryOptions
{
    public ResourceAttributesOptions ResourceAttributes { get; set; } = new();
    public ExportersOptions Exporters { get; set; } = new();
    public OtlpOptions Otlp { get; set; } = new();
}

public class ResourceAttributesOptions
{
    public bool IncludeFormattedMessage { get; set; } = true;
    public bool IncludeScopes { get; set; } = true;
    public bool ParseStateValues { get; set; } = true;
    public string ServiceName { get; set; } = "ratings";
    public string? NameSpace { get; set; }
    public string? ServiceVersion { get; set; } = "1.0.1";
    public string ServiceInstanceId { get; set; } = "ratings-001";
}

public class ExportersOptions
{
    public string? Traces { get; set; } = "none";
    public string? Metrics { get; set; } = "none";
    public string? Logs { get; set; } = "none";
}

public class OtlpOptions
{
    public string Endpoint { get; set; } = "http://localhost:4317";
    public string? BearerToken { get; set; } = null;
}
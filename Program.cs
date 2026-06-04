using InfinityAI.Pipeline.Contracts;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    component = "InfinityAI.Component.PiiVerification"
}));

app.MapPost("/pipeline/process", (PipelineRequest request) =>
{
    var findings = new List<PipelineFinding>();

    foreach (var message in request.Messages)
    {
        var text = ExtractText(message.Content);

        if (string.IsNullOrWhiteSpace(text))
            continue;

        if (LooksLikeSwedishPersonalNumber(text))
        {
            findings.Add(new PipelineFinding
            {
                Type = "pii.swedish-personal-number",
                Severity = "high",
                Message = "Text may contain a Swedish personal identity number.",
                Field = "messages.content"
            });
        }

        if (LooksLikeEmail(text))
        {
            findings.Add(new PipelineFinding
            {
                Type = "pii.email",
                Severity = "medium",
                Message = "Text may contain an email address.",
                Field = "messages.content"
            });
        }
    }

    var blocked = findings.Any(x => x.Severity == "high");

    return Results.Ok(new PipelineResponse
    {
        RequestId = request.RequestId,
        Component = "InfinityAI.Component.PiiVerification",
        Status = blocked
            ? PipelineStatus.Rejected
            : PipelineStatus.Approved,

        Action = blocked
            ? PipelineAction.Block
            : PipelineAction.Continue,

        Reason = blocked
            ? "PII verification failed."
            : "PII verification passed.",

        Findings = findings
    });
});

app.Run();

static string ExtractText(object? content)
{
    if (content is null)
        return "";

    if (content is string text)
        return text;

    if (content is JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.String)
            return json.GetString() ?? "";

        if (json.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();

            foreach (var item in json.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) &&
                    type.GetString() == "text" &&
                    item.TryGetProperty("text", out var textProp))
                {
                    parts.Add(textProp.GetString() ?? "");
                }
            }

            return string.Join("\n", parts);
        }
    }

    return content.ToString() ?? "";
}

static bool LooksLikeEmail(string text)
{
    return Regex.IsMatch(
        text,
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase);
}

// Century prefix (19|20) and separator [-+] are both mandatory so that arbitrary
// digit sequences in documents (VLAN IDs, account numbers, phone numbers) do not
// produce false-positive blocks.  Month (01-12) and day (01-31) ranges are
// validated to further reduce coincidental matches.
// Supports both long format (YYYYMMDD-XXXX) and short format (YYMMDD-XXXX).
static bool LooksLikeSwedishPersonalNumber(string text)
{
    return Regex.IsMatch(
        text,
        @"\b((19|20)\d{2}|\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[-+]\d{4}\b");
}
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5099");
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
var app = builder.Build();

app.MapPost("/v1/responses", async (HttpRequest request) =>
{
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer);
    PrintCapture(request, buffer.ToArray());
    return Results.Json(new { status = "captured" });
});

app.Run();

static void PrintCapture(HttpRequest request, byte[] body)
{
    Console.WriteLine("CAPTURE");
    Console.WriteLine($"Method: {request.Method}");
    Console.WriteLine($"Path: {request.Path}");
    Console.WriteLine($"HttpVersion: {request.Protocol}");
    Console.WriteLine($"Content-Type: {HeaderOrAbsent(request, "Content-Type")}");
    Console.WriteLine($"Content-Length: {HeaderOrAbsent(request, "Content-Length")}");
    Console.WriteLine($"Transfer-Encoding: {HeaderOrAbsent(request, "Transfer-Encoding")}");
    Console.WriteLine($"Expect: {HeaderOrAbsent(request, "Expect")}");
    Console.WriteLine(request.Headers.ContainsKey("Authorization")
        ? "Authorization: present"
        : "Authorization: absent");

    foreach (var header in request.Headers)
    {
        if (IsSensitiveHeader(header.Key) || IsPrintedHeader(header.Key))
        {
            continue;
        }

        Console.WriteLine($"{header.Key}: {header.Value}");
    }

    Console.WriteLine($"BodyBytes: {body.Length}");
    if (TryReadJsonSummary(body, out var names, out var model))
    {
        Console.WriteLine($"TopLevelProperties: {names}");
        Console.WriteLine($"Model: {model}");
    }
    else
    {
        Console.WriteLine("TopLevelProperties: invalid-json");
        Console.WriteLine("Model: absent");
    }

    Console.WriteLine("Body:");
    Console.WriteLine(Encoding.UTF8.GetString(body));
    Console.WriteLine();
}

static bool TryReadJsonSummary(byte[] body, out string names, out string model)
{
    names = "";
    model = "absent";
    try
    {
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            names = "not-an-object";
            return true;
        }

        var collected = new List<string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            collected.Add(property.Name);
            if (!property.NameEquals("model"u8))
            {
                continue;
            }

            model = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "null",
                JsonValueKind.Null => "null",
                _ => "non-string"
            };
        }

        names = string.Join(',', collected);
        return true;
    }
    catch (JsonException)
    {
        return false;
    }
}

static string HeaderOrAbsent(HttpRequest request, string name)
{
    return request.Headers.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value)
        ? value.ToString()
        : "absent";
}

static bool IsPrintedHeader(string name)
{
    return name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Expect", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Authorization", StringComparison.OrdinalIgnoreCase);
}

static bool IsSensitiveHeader(string name)
{
    return name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
        || name.Contains("api-key", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("token", StringComparison.OrdinalIgnoreCase);
}

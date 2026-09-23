using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var baseUrl = Environment.GetEnvironmentVariable("EQUIPFLOW_API_URL") ?? "http://localhost:5036";
var tokenFile = Path.Combine(Environment.CurrentDirectory, ".equipflow-token");
var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };

using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

// Load persisted JWT if it exists
if (File.Exists(tokenFile))
{
    var token = await File.ReadAllTextAsync(tokenFile);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
}

if (args.Length == 0)
{
    PrintHelp();
    return;
}

var command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "login": await LoginAsync(args); break;
        case "ingest": await IngestAsync(args); break;
        case "ask": await AskAsync(args); break;
        case "analyze": await AnalyzeAsync(args); break;
        case "approve": await ReviewAsync(args, "approve"); break;
        case "reject": await ReviewAsync(args, "reject"); break;
        case "trace": await TraceAsync(args); break;
        case "spend": await SpendAsync(); break;
        default: PrintHelp(); break;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Fatal Error: {ex.Message}");
    Console.ResetColor();
}

// --- Command Implementations ---

async Task LoginAsync(string[] args)
{
    if (args.Length < 3) { Console.WriteLine("Usage: login <username> <password>"); return; }
    
    var res = await client.PostAsJsonAsync("/api/auth/login", new { username = args[1], password = args[2] });
    if (!res.IsSuccessStatusCode) { Console.WriteLine($"Login failed: {res.StatusCode}"); return; }
    
    var json = await res.Content.ReadFromJsonAsync<JsonElement>();
    var token = json.GetProperty("accessToken").GetString();
    await File.WriteAllTextAsync(tokenFile, token!);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Login successful. Token saved to .equipflow-token");
    Console.ResetColor();
}

async Task IngestAsync(string[] args)
{
    if (args.Length < 2) { Console.WriteLine("Usage: ingest <filepath>"); return; }
    var path = args[1];
    if (!File.Exists(path)) { Console.WriteLine("File not found."); return; }
    
    using var form = new MultipartFormDataContent();
    var fileBytes = await File.ReadAllBytesAsync(path);
    var fileContent = new ByteArrayContent(fileBytes);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(fileContent, "file", Path.GetFileName(path));
    
    var metadata = JsonSerializer.Serialize(new { id = Guid.NewGuid().ToString(), type = "Manual", equipment = "P-101" });
    form.Add(new StringContent(metadata), "metadata");

    var res = await client.PostAsync("/api/documents", form);
    Console.WriteLine(res.IsSuccessStatusCode ? "Ingestion queued (202 Accepted)." : $"Error: {res.StatusCode}");
}

async Task AskAsync(string[] args)
{
    if (args.Length < 2) { Console.WriteLine("Usage: ask <query>"); return; }
    EnsureAuthenticated();
    
    var query = string.Join(" ", args.Skip(1));
    var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat") {
        Content = JsonContent.Create(new { message = query, stream = true })
    };
    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    
    using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    if (!response.IsSuccessStatusCode) { Console.WriteLine($"Error: {response.StatusCode}"); return; }
    
    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);
    
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Agent: ");
    Console.ResetColor();
    
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        if (line != null && line.StartsWith("data: "))
        {
            var data = line.Substring(6);
            try
            {
                var evt = JsonSerializer.Deserialize<JsonElement>(data);
                if (evt.TryGetProperty("eventType", out var type))
                {
                    var eventType = type.GetString();
                    if (eventType == "token" && evt.TryGetProperty("data", out var d) && d.TryGetProperty("content", out var c))
                    {
                        Console.Write(c.GetString());
                    }
                    else if (eventType == "done")
                    {
                        Console.WriteLine("\n");
                    }
                    else if (eventType == "budget.exhausted")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n[Budget Exhausted]");
                        Console.ResetColor();
                    }
                }
            }
            catch { /* ignore non-JSON SSE comments/lines */ }
        }
    }
}

async Task AnalyzeAsync(string[] args)
{
    if (args.Length < 2) { Console.WriteLine("Usage: analyze <symptom>"); return; }
    EnsureAuthenticated();
    
    var symptom = string.Join(" ", args.Skip(1));
    var res = await client.PostAsJsonAsync("/api/ai/analyze", new { symptomDescription = symptom });
    
    if (!res.IsSuccessStatusCode) { Console.WriteLine($"Error: {res.StatusCode}"); return; }
    
    var json = await res.Content.ReadAsStringAsync();
    Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), jsonOpts));
}

async Task ReviewAsync(string[] args, string action)
{
    if (args.Length < 2) { Console.WriteLine($"Usage: {action} <workOrderId> [comment]"); return; }
    EnsureAuthenticated();
    
    var id = args[1];
    var comment = args.Length > 2 ? string.Join(" ", args.Skip(2)) : null;
    
    var res = await client.PostAsJsonAsync($"/api/workorders/{id}/{action}", new { comment });
    
    if (res.IsSuccessStatusCode)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{action} successful.");
        Console.ResetColor();
    }
    else
    {
        var error = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"Error: {res.StatusCode} - {error}");
    }
}

async Task TraceAsync(string[] args)
{
    if (args.Length < 2) { Console.WriteLine("Usage: trace <runId>"); return; }
    EnsureAuthenticated();
    
    var res = await client.GetAsync($"/api/runs/{args[1]}");
    if (!res.IsSuccessStatusCode) { Console.WriteLine($"Error: {res.StatusCode}"); return; }
    
    var json = await res.Content.ReadAsStringAsync();
    Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), jsonOpts));
}

async Task SpendAsync()
{
    EnsureAuthenticated();
    // Using /api/budget/me as mapped in CostGovernorEndpoints
    var res = await client.GetAsync("/api/budget/me"); 
    if (!res.IsSuccessStatusCode) { Console.WriteLine($"Error: {res.StatusCode}"); return; }
    
    var json = await res.Content.ReadAsStringAsync();
    Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json), jsonOpts));
}

// --- Helpers ---

void EnsureAuthenticated()
{
    if (client.DefaultRequestHeaders.Authorization == null)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("You must login first. Usage: login <username> <password>");
        Console.ResetColor();
        Environment.Exit(1);
    }
}

void PrintHelp()
{
    Console.WriteLine("EquipFlow CLI (Minimal Operational Surface)");
    Console.WriteLine("Usage: equipflow <command> [args]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  login <user> <pass>    Authenticate and store JWT");
    Console.WriteLine("  ingest <filepath>      Upload a PDF/DOCX for ingestion");
    Console.WriteLine("  ask <query>            Grounded RAG chat with SSE streaming");
    Console.WriteLine("  analyze <symptom>      Run D5 agentic workflow");
    Console.WriteLine("  approve <id> [comment] Supervisor approves work order");
    Console.WriteLine("  reject <id> <comment>  Supervisor rejects work order");
    Console.WriteLine("  trace <runId>          View agent run observability");
    Console.WriteLine("  spend                  View T3 Cost Governor budget");
}
using Microsoft.Extensions.Options;
using Voixla.Api;
using Voixla.Api.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PiperOptions>(builder.Configuration.GetSection(PiperOptions.SectionName));
builder.Services.AddSingleton<PiperService>();
builder.Services.AddHostedService<CacheJanitor>();

const string DevCors = "dev-cors";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5174", "http://127.0.0.1:5174")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCors);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/voices", (PiperService piper) => Results.Ok(piper.ListVoices()));

app.MapPost("/api/prepare", async (PrepareRequest req, PiperService piper, IOptions<PiperOptions> opts, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.BadRequest(new { error = "text is required" });
    }

    if (string.IsNullOrWhiteSpace(req.Voice))
    {
        return Results.BadRequest(new { error = "voice is required" });
    }

    if (req.Text.Length > opts.Value.MaxTextChars)
    {
        return Results.BadRequest(new { error = $"text exceeds {opts.Value.MaxTextChars} characters" });
    }

    if (!piper.IsValidVoice(req.Voice))
    {
        return Results.BadRequest(new { error = "unknown voice" });
    }

    var pieces = TextChunker.Chunk(req.Text, opts.Value.MaxChunkChars);

    var chunks = new List<ChunkDto>(pieces.Count);
    for (var i = 0; i < pieces.Count; i++)
    {
        var hash = await piper.RegisterChunkAsync(req.Voice, pieces[i], ct);
        chunks.Add(new ChunkDto(i, hash, pieces[i]));
    }

    return Results.Ok(new PrepareResponse(req.Voice, chunks));
});

app.MapGet("/api/audio/{hash}.wav", async (string hash, PiperService piper, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    try
    {
        var path = await piper.GetOrSynthesizeAsync(hash, ct);
        if (path is null)
        {
            return Results.NotFound(new { error = "unknown or unsynthesizable chunk" });
        }
        return Results.File(File.OpenRead(path), "audio/wav", enableRangeProcessing: true);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = "voice model not installed" });
    }
    catch (Exception ex)
    {
        loggerFactory.CreateLogger("Audio").LogError(ex, "Synthesis failed for chunk {Hash}", hash);
        return Results.Problem("Audio synthesis failed.", statusCode: 500);
    }
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapFallbackToFile("index.html");

app.Run();

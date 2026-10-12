using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventsManager.Api;
using EventsManager.Application;
using EventsManager.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Deux jeux d'options JSON distincts, qui doivent dire la même chose : les formateurs MVC
// sérialisent les corps de réponse, DefaultProblemDetailsWriter sérialise les erreurs des
// middlewares. N'en configurer qu'un donnerait deux conventions dans la même API.
// Référentiel : docs/api-conventions.md, section 2.7.
builder.Services.AddControllers()
       .AddJsonOptions(options => ConfigureSerialization(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigureSerialization(options.SerializerOptions));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = CustomizeProblem);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();

// La génération OpenAPI au build lance ce point d'entrée dans un hôte factice, nommé
// GetDocument.Insider (doc Microsoft) : aucune chaîne de connexion n'y est disponible et
// l'infrastructure n'y sert à rien. Sans cette garde, dotnet build échouerait sur le throw
// ci-dessous. Référentiel : docs/api-conventions.md, section 7.4.
if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    var connectionString = builder.Configuration.GetConnectionString("EventsManager")
        ?? throw new InvalidOperationException("Chaîne de connexion « EventsManager » absente de la configuration.");

    builder.Services.AddInfrastructure(connectionString);
}

var app = builder.Build();

// En tête de pipeline : toute réponse d'erreur sort en application/problem+json, l'exception
// non gérée (UseExceptionHandler) comme le statut nu produit par le routage
// (UseStatusCodePages). Référentiel : docs/api-conventions.md, section 4.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

await app.RunAsync();

static void ConfigureSerialization(JsonSerializerOptions options)
{
    // snake_case partout (section 2.1) et champ sans valeur absent du JSON (section 2.2).
    options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
}

static void CustomizeProblem(ProblemDetailsContext context)
{
    var problem = context.ProblemDetails;
    var status = problem.Status ?? context.HttpContext.Response.StatusCode;

    problem.Status = status;
    problem.Instance ??= context.HttpContext.Request.Path.Value;

    var (catalogueType, catalogueTitle) = ProblemCatalogue.For(status);

    // Un endpoint qui a déjà posé son propre slug relatif garde la main ; on ne remplace que
    // l'absence ou le défaut du framework, qui est une URI RFC absolue.
    // Référentiel : docs/api-conventions.md, section 4.7.
    if (problem.Type is null || !problem.Type.StartsWith("/problems/", StringComparison.Ordinal))
    {
        problem.Type = catalogueType;
    }

    // Le titre se décide indépendamment du type : un endpoint qui pose un slug fin sans titre
    // laisserait sortir le titre anglais du framework. La section 4.4 ne doit pas dépendre de
    // la discipline de l'appelant — écrire un titre non français redevient un acte délibéré.
    if (IsFrameworkTitle(context, status, problem.Title))
    {
        problem.Title = catalogueTitle;
    }

    // ASP.NET Core pose lui-même une extension « traceId » en camelCase, qui violerait la
    // section 2.1. On la retire pour n'en garder qu'une, écrite en snake_case (section 4.6).
    // La valeur est identique : cette clé est un renommage, pas un doublon.
    problem.Extensions.Remove("traceId");
    problem.Extensions["trace_id"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
}

/// <summary>
/// Un titre est « du framework » s'il est absent, ou s'il est exactement celui qu'ASP.NET Core
/// aurait posé pour ce statut. La comparaison se fait à la source (ApiBehaviorOptions), pas sur
/// une liste de phrases anglaises recopiée ici, qui divergerait en silence.
/// </summary>
static bool IsFrameworkTitle(ProblemDetailsContext context, int status, string? title)
{
    if (title is null)
    {
        return true;
    }

    var mapping = context.HttpContext.RequestServices
                         .GetRequiredService<IOptions<ApiBehaviorOptions>>()
                         .Value.ClientErrorMapping;

    return mapping.TryGetValue(status, out var data)
        && string.Equals(title, data.Title, StringComparison.Ordinal);
}

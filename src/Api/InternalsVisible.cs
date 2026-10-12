using System.Runtime.CompilerServices;

// Program est interne (top-level statements) : WebApplicationFactory<Program> ne peut le cibler
// qu'à travers cette ouverture. Convention du repo : fichier dédié, attribut d'assembly, jamais
// dans le csproj.
[assembly: InternalsVisibleTo("EventsManager.ApiTests")]

using EventsManager.IntegrationTests;

// Fixtures d'assembly xUnit v3 : un seul conteneur PostgreSQL pour toute l'assembly,
// injecté dans les constructeurs des classes de test.
[assembly: AssemblyFixture(typeof(PostgreSqlContainerFixture))]

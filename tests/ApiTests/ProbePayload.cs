namespace EventsManager.ApiTests;

/// <summary>
/// Charge utile de sonde : elle porte, dans un seul objet, les quatre formes que la
/// sérialisation doit produire — un nom composé (snake_case), un champ optionnel non
/// renseigné (absent du JSON), une collection vide (rendue en tableau vide) et une DateOnly
/// (ISO 8601). Réf. docs/api-conventions.md, sections 2.1 à 2.5.
/// </summary>
internal sealed record ProbePayload(
    Guid Id,
    string CustomerName,
    DateOnly EventDate,
    string? Note,
    IReadOnlyList<string> Tags);

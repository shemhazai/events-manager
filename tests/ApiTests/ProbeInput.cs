namespace EventsManager.ApiTests;

/// <summary>
/// Corps d'entrée de la sonde. Uniquement des références : un type valeur en entrée de
/// contrôleur déclenche S6964 (sous-postage), hors sujet ici.
/// </summary>
public sealed record ProbeInput(string Label);

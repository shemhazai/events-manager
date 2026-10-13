namespace EventsManager.Application.Events;

/// <summary>
/// Lecture de l'évènement courant. Aucun paramètre : la ressource est un singleton, et
/// « aujourd'hui » n'est pas une entrée du client — c'est le handler qui le tire du point de
/// vérité unique (<see cref="TimeProviderExtensions.TodayLocal"/>).
/// </summary>
public sealed record GetCurrentEventQuery;

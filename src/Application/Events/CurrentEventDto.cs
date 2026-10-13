namespace EventsManager.Application.Events;

/// <summary>
/// Contrat de sortie de la lecture de l'évènement courant, <b>dédié au front public</b>.
/// Distinct de <see cref="EventDto"/> et volontairement non réutilisé : le jour où le contrat
/// du back-office gagne un catalogue de produits ou des statistiques, le front public ne les
/// reçoit pas.
/// <para>
/// Quatre champs, et seulement quatre. Le <b>début</b> de fenêtre n'est pas exposé : par
/// construction un évènement rendu ici a sa fenêtre ouverte, le début n'apporte donc plus rien
/// au front. La date de fin, elle, n'est pas nullable — une candidate a nécessairement une
/// fenêtre, et le type le dit.
/// </para>
/// </summary>
public sealed record CurrentEventDto(Guid Id, string Name, DateOnly Date, DateOnly OrderWindowEndDate);

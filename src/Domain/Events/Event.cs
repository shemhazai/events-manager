namespace EventsManager.Domain.Events;

/// <summary>
/// Racine d'agrégat : un évènement géré par le back-office.
/// Invariants et préconditions garantis ici, indépendamment de toute validation amont :
/// nom non vide (ni blanc) et limité à <see cref="NameMaxLength"/> caractères après trim ;
/// à la création, date au moins à J+1 — le domaine ne lit pas l'horloge, « aujourd'hui »
/// est fourni par l'appelant.
/// La fenêtre de commande est optionnelle, et c'est elle — jamais la date de l'évènement —
/// qui détermine si les commandes en ligne sont ouvertes.
/// </summary>
public sealed class Event
{
    public const int NameMaxLength = 100;

    public EventId Id { get; }

    public string Name { get; }

    public DateOnly Date { get; }

    /// <summary>
    /// Premier jour d'ouverture des commandes, inclus. Null si et seulement si
    /// <see cref="OrderWindowEndDate"/> l'est aussi : aucune surcharge de <c>Create</c>
    /// n'accepte une seule des deux dates.
    /// </summary>
    public DateOnly? OrderWindowStartDate { get; }

    /// <summary>Dernier jour d'ouverture des commandes, inclus.</summary>
    public DateOnly? OrderWindowEndDate { get; }

    private Event(
        EventId id,
        string name,
        DateOnly date,
        DateOnly? orderWindowStartDate,
        DateOnly? orderWindowEndDate)
    {
        Id = id;
        Name = name;
        Date = date;
        OrderWindowStartDate = orderWindowStartDate;
        OrderWindowEndDate = orderWindowEndDate;
    }

    /// <summary>
    /// Crée un évènement <b>sans fenêtre de commande</b> : les commandes en ligne sont donc
    /// fermées. Ce n'est pas un défaut de configuration, c'est l'état normal d'un évènement
    /// dont l'ouverture n'est pas encore décidée.
    /// </summary>
    public static Event Create(string name, DateOnly date, DateOnly today)
    {
        return CreateCore(name, date, today, orderWindowStartDate: null, orderWindowEndDate: null);
    }

    /// <summary>
    /// Crée un évènement <b>avec sa fenêtre de commande</b>, en une seule opération.
    /// Les deux dates sont obligatoires ensemble : la signature rend inexprimable un évènement
    /// qui ne porterait qu'une des deux. Invariant d'ordre vérifié à la création :
    /// <paramref name="orderWindowStartDate"/> &lt;= <paramref name="orderWindowEndDate"/>
    /// &lt;= <paramref name="date"/>. Une fenêtre d'un seul jour (début égal à la fin) est valide.
    /// </summary>
    public static Event Create(
        string name,
        DateOnly date,
        DateOnly today,
        DateOnly orderWindowStartDate,
        DateOnly orderWindowEndDate)
    {
        return CreateCore(name, date, today, orderWindowStartDate, orderWindowEndDate);
    }

    /// <summary>
    /// Validation unique des deux surcharges, dans un ordre stable : nom, puis date de
    /// l'évènement, puis fenêtre de commande. Les deux dates de fenêtre arrivent ici
    /// nullables parce que l'absence de fenêtre est un état légitime ; le « les deux ou
    /// aucune » est porté par les signatures publiques, pas par un contrôle.
    /// </summary>
    private static Event CreateCore(
        string name,
        DateOnly date,
        DateOnly today,
        DateOnly? orderWindowStartDate,
        DateOnly? orderWindowEndDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();

        if (trimmedName.Length > NameMaxLength)
        {
            throw new ArgumentException(
                $"Le nom de l'évènement ne peut pas dépasser {NameMaxLength} caractères (reçu : {trimmedName.Length}).",
                nameof(name));
        }

        if (date <= today)
        {
            throw new ArgumentOutOfRangeException(
                nameof(date),
                $"La date de l'évènement doit être au moins à J+1 (date reçue : {date:o}, aujourd'hui : {today:o}).");
        }

        if (orderWindowStartDate is { } start && orderWindowEndDate is { } end)
        {
            if (start > end)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orderWindowStartDate),
                    $"La fenêtre de commande ne peut pas commencer après sa fin (début : {start:o}, fin : {end:o}).");
            }

            if (end > date)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orderWindowEndDate),
                    $"La fenêtre de commande ne peut pas se fermer après l'évènement (fin : {end:o}, évènement : {date:o}).");
            }
        }

        return new Event(EventId.New(), trimmedName, date, orderWindowStartDate, orderWindowEndDate);
    }
}

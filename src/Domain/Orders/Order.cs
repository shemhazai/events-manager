using EventsManager.Domain.Events;

namespace EventsManager.Domain.Orders;

/// <summary>
/// Racine d'agrégat : une commande passée pour un évènement, créée complète et
/// immuable — la racine et toutes ses lignes, ou rien.
/// Invariants et préconditions garantis ici, indépendamment de toute validation
/// amont : référence d'évènement obligatoire (l'existence de l'évènement relève de
/// la couche Application) ; nom du client non vide (ni blanc) et limité à
/// <see cref="CustomerNameMaxLength"/> caractères après trim ; au moins une ligne,
/// sans plafond numérique ; libellés de lignes uniques (après trim, insensible à
/// la casse). Le total n'est ni saisi ni stocké : il est calculé (somme des
/// sous-totaux). Aucune contrainte entre le moment de la saisie et la date de
/// l'évènement — le domaine ne lit pas l'horloge, la date de création est fournie
/// par l'appelant.
/// </summary>
public sealed class Order
{
    public const int CustomerNameMaxLength = 100;

    private readonly List<OrderLine> _lines;

    public OrderId Id { get; }

    public EventId EventId { get; }

    public string CustomerName { get; }

    /// <summary>
    /// Date de création (UTC), fournie par l'appelant. Porte le tri « ordre de
    /// création » côté SQL — jamais l'ID (cf. <see cref="OrderId"/>).
    /// </summary>
    public DateTime CreatedAt { get; }

    // AsReadOnly : vue non downcastable — la List vivante resterait mutable après un
    // cast, ce qui contournerait les invariants (au moins une ligne, pas de doublon).
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public Money Total => _lines.Select(line => line.Subtotal).Aggregate((left, right) => left + right);

    private Order(OrderId id, EventId eventId, string customerName, DateTime createdAt)
    {
        Id = id;
        EventId = eventId;
        CustomerName = customerName;
        CreatedAt = createdAt;
        _lines = [];
    }

    public static Order Create(
        EventId eventId,
        string customerName,
        IReadOnlyCollection<OrderLine> lines,
        DateTime createdAtUtc)
    {
        if (eventId == default)
        {
            throw new ArgumentException(
                "Une commande doit référencer un évènement.",
                nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(customerName);

        var trimmedCustomerName = customerName.Trim();

        if (trimmedCustomerName.Length > CustomerNameMaxLength)
        {
            throw new ArgumentException(
                $"Le nom du client ne peut pas dépasser {CustomerNameMaxLength} caractères (reçu : {trimmedCustomerName.Length}).",
                nameof(customerName));
        }

        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new ArgumentException(
                "Une commande doit contenir au moins une ligne.",
                nameof(lines));
        }

        var duplicatedLabel = lines
            .GroupBy(line => line.Label, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedLabel is not null)
        {
            throw new ArgumentException(
                $"Deux lignes d'une même commande ne peuvent pas porter le même libellé (doublon : « {duplicatedLabel.Key} »).",
                nameof(lines));
        }

        var order = new Order(OrderId.New(), eventId, trimmedCustomerName, createdAtUtc);
        order._lines.AddRange(lines);

        return order;
    }
}

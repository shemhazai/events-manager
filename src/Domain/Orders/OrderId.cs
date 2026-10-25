namespace EventsManager.Domain.Orders;

/// <summary>
/// Identité de l'agrégat <see cref="Order"/>, au format UUIDv7 (RFC 9562) :
/// 48 bits de timestamp Unix (ms) en tête + 74 bits aléatoires, donc unique et
/// triable chronologiquement côté .NET (à la milliseconde près — pas d'ordre
/// garanti entre deux IDs créés dans la même milliseconde).
/// Le tri chronologique côté SQL est porté par une colonne dédiée (CreatedAt pour Order),
/// jamais par cet ID. PostgreSQL trie pourtant le type uuid octet par octet de gauche
/// à droite, donc un UUIDv7 y est bien chronologique (mesuré le 2026-10-12) : la colonne
/// de tri explicite reste la règle parce qu'elle ne dépend d'aucun provider, et c'est ce
/// motif — et non le comportement d'un moteur donné — qui l'a fait choisir.
/// </summary>
public readonly record struct OrderId
{
    public Guid Value { get; }

    private OrderId(Guid value)
    {
        Value = value;
    }

    public static OrderId New()
    {
        return new OrderId(Guid.CreateVersion7());
    }

    public static OrderId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Un OrderId ne peut pas être construit à partir d'un Guid vide.",
                nameof(value));
        }

        return new OrderId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}

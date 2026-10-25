using EventsManager.Domain.Events;
using EventsManager.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsManager.Infrastructure.Orders;

/// <summary>
/// Configuration fluent de l'agrégat <see cref="Order"/> → tables app.Orders et
/// app.OrderLines (lignes owned : chargées systématiquement avec la racine,
/// persistées avec elle dans le même SaveChanges — la commande est atomique).
/// Les colonnes d'audit des deux tables (AddedBy, AddedDate, UpdatedBy, UpdatedDate)
/// n'existent volontairement pas dans ce modèle, même pas en shadow : créées par la
/// migration InitialCreate et alimentées côté SQL (DEFAULT + trigger), elles restent
/// invisibles d'EF (cf. <c>EventConfiguration</c>).
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Pas de HasTrigger, ici ni sur OrderLines : cf. EventConfiguration — la déclaration ne
        // contournait que la clause OUTPUT de SQL Server, et Npgsql emploie RETURNING.
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
               .HasConversion(id => id.Value, value => OrderId.From(value))
               .ValueGeneratedNever(); // UUIDv7 généré par le domaine, jamais par la DB

        builder.Property(o => o.EventId)
               .HasConversion(id => id.Value, value => EventId.From(value));

        // FK physique vers app.Events, sans navigation (référence inter-agrégats par
        // identité). Restrict : aucun use case de suppression ; un cascade silencieux
        // supprimerait des commandes.
        builder.HasOne<Event>()
               .WithMany()
               .HasForeignKey(o => o.EventId)
               .OnDelete(DeleteBehavior.Restrict)
               .IsRequired();

        builder.Property(o => o.CustomerName)
               .HasMaxLength(Order.CustomerNameMaxLength); // varchar(100) ; NOT NULL par convention NRT

        // Le tri métier « ordre de création » de la liste par évènement est porté par CreatedAt,
        // jamais par l'ID. PostgreSQL trie pourtant uuid octet par octet, donc un UUIDv7 y est
        // chronologique : la colonne explicite reste la règle parce qu'elle ne dépend d'aucun
        // provider (cf. OrderId).
        builder.HasIndex(o => new { o.EventId, o.CreatedAt });

        // Total : calculé par le domaine à partir des lignes, ni saisi ni stocké.
        builder.Ignore(o => o.Total);

        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("OrderLines");

            lines.WithOwner().HasForeignKey("OrderId");

            // PK composite (OrderId, Label) : aucune génération côté DB, et unicité physique
            // EXACTE des libellés par commande. Elle est conservée telle quelle — une PRIMARY KEY
            // PostgreSQL ne peut pas porter d'expression (mesuré le 2026-10-12), elle ne peut
            // donc pas porter elle-même l'insensibilité à la casse.
            lines.HasKey("OrderId", nameof(OrderLine.Label));

            lines.Property(l => l.Label)
                 .HasMaxLength(OrderLine.LabelMaxLength);

            // LabelLower porte l'insensibilité à la casse de la règle du domaine (libellés
            // uniques après trim et sans égard à la casse, cf. Order.Create). Avant la bascule,
            // la collation CI par défaut de SQL Server la donnait gratuitement à la PK, alors
            // que PostgreSQL est sensible à la casse : il faut donc l'écrire. L'index unique
            // s'AJOUTE à la PK et ne la remplace pas : la PK reste la clé, cible de la FK et
            // identité de la ligne.
            lines.Property<string>("LabelLower")
                 .HasMaxLength(OrderLine.LabelMaxLength)
                 .IsRequired()
                 .HasComputedColumnSql("lower(\"Label\")", stored: true);

            lines.HasIndex("OrderId", "LabelLower").IsUnique();

            lines.Property(l => l.UnitPrice)
                 .HasConversion(money => money.Amount, value => Money.From(value))
                 .HasPrecision(9, Money.MaxDecimalPlaces); // precision 9 : jusqu'à 9 999 999,99 € l'unité, large pour des commandes d'évènement

            // Déclaration explicite requise : une auto-propriété get-only n'est pas
            // découverte par convention, et le binding par constructeur exige que
            // chaque paramètre corresponde à une propriété mappée.
            lines.Property(l => l.Quantity);

            // Sous-total : calculé par le domaine, jamais stocké.
            lines.Ignore(l => l.Subtotal);
        });
    }
}

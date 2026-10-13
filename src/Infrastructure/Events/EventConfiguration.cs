using EventsManager.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsManager.Infrastructure.Events;

/// <summary>
/// Configuration fluent de l'agrégat <see cref="Event"/> → table app.Events.
/// EF matérialise l'agrégat par son constructeur privé (liaison par nom de paramètre).
/// Les colonnes d'audit (AddedBy, AddedDate, UpdatedBy, UpdatedDate) n'existent
/// volontairement pas dans ce modèle, même pas en shadow : créées par la migration
/// EventsAudit et alimentées côté SQL (DEFAULT + trigger), elles restent invisibles
/// d'EF — ses INSERT ne les mentionnent jamais et le snapshot ne les connaît pas.
/// </summary>
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", table =>
        {
            // HasTrigger obligatoire : depuis EF Core 7, SaveChanges utilise une clause OUTPUT
            // sans INTO, que SQL Server refuse sur une table portant un trigger. Déclarer le
            // trigger fait basculer EF sur la stratégie compatible.
            table.HasTrigger("TR_Events_Update_Audit");

            // Fenêtre de commande : les mêmes invariants que ceux d'Event.Create, mais garantis
            // côté SQL Server. Ce n'est pas un doublon décoratif — la donnée n'entre pas
            // toujours par le domaine : les scripts de db/seed/ écrivent en SQL direct, et
            // c'est précisément le chemin dont une faute de frappe produirait un état
            // incohérent. Les deux gardes visent deux écrivains différents.
            table.HasCheckConstraint(
                "CK_Events_OrderWindow_BothOrNeither",
                "([OrderWindowStartDate] IS NULL AND [OrderWindowEndDate] IS NULL) "
                + "OR ([OrderWindowStartDate] IS NOT NULL AND [OrderWindowEndDate] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_Events_OrderWindow_Order",
                "[OrderWindowStartDate] IS NULL "
                + "OR ([OrderWindowStartDate] <= [OrderWindowEndDate] AND [OrderWindowEndDate] <= [Date])");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
               .HasConversion(id => id.Value, value => EventId.From(value))
               .ValueGeneratedNever(); // UUIDv7 généré par le domaine, jamais par la DB

        builder.Property(e => e.Name)
               .HasMaxLength(Event.NameMaxLength); // nvarchar(100) ; NOT NULL par convention NRT

        // Date : DateOnly → date, mapping natif SQL Server.
        // Unicité métier : un évènement (par nom) n'a lieu qu'une fois par année civile.
        // Year est calculée par SQL Server (persistée), en shadow : dérivée de Date,
        // jamais écrite par le code.
        builder.Property<int>("Year").HasComputedColumnSql("YEAR([Date])", stored: true);
        builder.HasIndex("Name", "Year").IsUnique();

        builder.HasIndex(e => e.Date); // le tri chronologique métier est porté par Date

        // Déclaration explicite requise : une auto-propriété get-only n'est pas découverte par
        // convention, et le binding par constructeur exige que chaque paramètre corresponde à
        // une propriété mappée. DateOnly? → date NULL, mapping natif SQL Server.
        // Pas d'index sur ces deux colonnes : la table est bornée à quelques dizaines de lignes,
        // et un index composite ne sert pas un prédicat d'encadrement (début <= @jour <= fin),
        // dont seule la colonne de tête serait exploitable. La question redeviendra réelle avec
        // la contrainte de non-chevauchement des fenêtres (hors de cette tranche).
        builder.Property(e => e.OrderWindowStartDate);
        builder.Property(e => e.OrderWindowEndDate);
    }
}

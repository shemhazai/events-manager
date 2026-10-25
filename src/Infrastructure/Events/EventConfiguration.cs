using EventsManager.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsManager.Infrastructure.Events;

/// <summary>
/// Configuration fluent de l'agrégat <see cref="Event"/> → table app.Events.
/// EF matérialise l'agrégat par son constructeur privé (liaison par nom de paramètre).
/// Les colonnes d'audit (AddedBy, AddedDate, UpdatedBy, UpdatedDate) n'existent
/// volontairement pas dans ce modèle, même pas en shadow : créées par la migration
/// InitialCreate et alimentées côté SQL (DEFAULT + trigger), elles restent invisibles
/// d'EF — ses INSERT ne les mentionnent jamais et le snapshot ne les connaît pas.
/// </summary>
internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", table =>
        {
            // Pas de HasTrigger, et c'est voulu : la déclaration ne servait qu'à faire renoncer
            // EF à la clause OUTPUT sans INTO, que SQL Server refuse sur une table à trigger.
            // Npgsql emploie RETURNING, que PostgreSQL accepte aussi bien sur une table à
            // trigger que sur une colonne générée. L'y laisser serait inerte et trompeur.

            // Fenêtre de commande : les mêmes invariants que ceux d'Event.Create, mais garantis
            // côté base. Ce n'est pas un doublon décoratif — la donnée n'entre pas
            // toujours par le domaine : les scripts de db/seed/ écrivent en SQL direct, et
            // c'est précisément le chemin dont une faute de frappe produirait un état
            // incohérent. Les deux gardes visent deux écrivains différents.
            table.HasCheckConstraint(
                "CK_Events_OrderWindow_BothOrNeither",
                "(\"OrderWindowStartDate\" IS NULL AND \"OrderWindowEndDate\" IS NULL) "
                + "OR (\"OrderWindowStartDate\" IS NOT NULL AND \"OrderWindowEndDate\" IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_Events_OrderWindow_Order",
                "\"OrderWindowStartDate\" IS NULL "
                + "OR (\"OrderWindowStartDate\" <= \"OrderWindowEndDate\" AND \"OrderWindowEndDate\" <= \"Date\")");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
               .HasConversion(id => id.Value, value => EventId.From(value))
               .ValueGeneratedNever(); // UUIDv7 généré par le domaine, jamais par la DB

        builder.Property(e => e.Name)
               .HasMaxLength(Event.NameMaxLength); // varchar(100) ; NOT NULL par convention NRT

        // Date : DateOnly → date, mapping natif Npgsql.
        // Unicité métier : un évènement (par nom) n'a lieu qu'une fois par année civile.
        // Year est calculée par la base (colonne générée STORED), en shadow : dérivée de Date,
        // jamais écrite par le code.
        builder.Property<int>("Year")
               .HasComputedColumnSql("EXTRACT(YEAR FROM \"Date\")", stored: true);

        // NameLower porte l'insensibilité à la casse d'EV-U2, et elle la porte EXPLICITEMENT.
        // Avant la bascule, cette insensibilité venait de la collation CI par défaut de
        // SQL Server, donc d'un effet de bord. PostgreSQL est sensible à la casse : sans cette
        // colonne, « Souper » et « SOUPER » dans la même année civile passeraient tous les deux,
        // en silence et sans qu'aucun test ne tombe — rien en amont ne garantit cette règle,
        // ni Event.Create, ni le handler de création.
        // L'index unique porte donc sur (NameLower, Year) et REMPLACE (Name, Year) : l'unicité
        // du nom replié implique celle du nom exact, un second index ne garderait rien de plus.
        builder.Property<string>("NameLower")
               .HasMaxLength(Event.NameMaxLength)
               .IsRequired()
               .HasComputedColumnSql("lower(\"Name\")", stored: true);

        builder.HasIndex("NameLower", "Year").IsUnique();

        builder.HasIndex(e => e.Date); // le tri chronologique métier est porté par Date

        // Déclaration explicite requise : une auto-propriété get-only n'est pas découverte par
        // convention, et le binding par constructeur exige que chaque paramètre corresponde à
        // une propriété mappée. DateOnly? → date NULL, mapping natif Npgsql.
        // Pas d'index sur ces deux colonnes : la table est bornée à quelques dizaines de lignes,
        // et un index composite ne sert pas un prédicat d'encadrement (début <= @jour <= fin),
        // dont seule la colonne de tête serait exploitable. La question redeviendra réelle avec
        // la contrainte de non-chevauchement des fenêtres (hors de cette tranche).
        builder.Property(e => e.OrderWindowStartDate);
        builder.Property(e => e.OrderWindowEndDate);
    }
}

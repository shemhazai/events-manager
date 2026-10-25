/*
    Évènement « Saint-Nicolas » — données de démonstration.

    POURQUOI CE SCRIPT N'EST PAS UNE MIGRATION
    Les tests d'intégration migrent la base de leur conteneur avec les migrations réelles
    (PostgreSqlContainerFixture → MigrateAsync). Une insertion portée par une migration
    apparaîtrait donc dans la base de TOUS les tests, et pourrait fausser leurs attendus —
    en particulier ceux de la lecture de l'évènement courant, qui interroge la table entière.
    Ce script vit donc hors migrations et s'applique À LA MAIN.

    APPLICATION
    Dans le conteneur « db » du docker-compose, sur une base déjà migrée :
        docker compose exec db bash -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=on -f /seed/saint-nicolas.sql'
    Trois détails de cette commande, tous nécessaires. ON_ERROR_STOP=on est l'équivalent du
    -b de sqlcmd : sans lui, psql poursuit après une erreur SQL et sort en 0, donc un seed à
    moitié appliqué passerait pour un succès. Les guillemets simples laissent le shell DU
    CONTENEUR évaluer les variables, au lieu de celui du poste. Et sous Git Bash uniquement,
    préfixer par MSYS_NO_PATHCONV=1, sans quoi la conversion de chemins MSYS réécrit
    /seed/... en chemin Windows.
    Aucun client PostgreSQL n'est requis sur le poste : psql est dans l'image.
    Depuis le poste malgré tout, si un client y est installé (le port 5432 du conteneur est
    publié sur la loopback) :
        psql -h localhost -p 5432 -U postgres -d EventsManager -v ON_ERROR_STOP=on -f db/seed/saint-nicolas.sql
    Ou exécution directe dans pgAdmin / DBeaver sur la base cible.
    Idempotent : une seconde exécution ne fait rien et ne lève pas d'erreur.

    IDENTIFIANT
    Littéral au format UUIDv7 (RFC 9562), généré une fois par Guid.CreateVersion7() et figé
    ici. Jamais gen_random_uuid() : ce serait un UUIDv4, et il changerait à chaque exécution,
    ce qui rendrait le script non idempotent et l'identifiant inutilisable dans un test manuel.

    COLONNES D'AUDIT
    AddedBy et AddedDate ne sont pas mentionnées : elles sont remplies par les contraintes
    DEFAULT (current_user, now() AT TIME ZONE 'utc') posées par la migration InitialCreate.

    COLONNES GÉNÉRÉES
    Year et NameLower ne sont pas mentionnées non plus, et ne POURRAIENT pas l'être :
    PostgreSQL refuse une valeur explicite pour une colonne GENERATED ALWAYS. C'est NameLower,
    avec son index unique, qui porte l'insensibilité à la casse de l'unicité des noms (EV-U2).

    FENÊTRE DE COMMANDE
    2026-10-01 → 2026-11-22, donc ouverte à la date de rédaction (2026-10-13) :
    GET /api/events/current rend un 200 juste après l'application du script.
    L'invariant début <= fin <= date de l'évènement est tenu, et les contraintes
    CK_Events_OrderWindow_* de la base le vérifient de toute façon.
*/

-- Identifiants quotés, et ce n'est pas un détail de style : PostgreSQL replie en minuscules
-- tout identifiant non quoté, alors que le modèle EF crée les tables et les colonnes en
-- PascalCase. Sans guillemets, cet INSERT échouerait sur une relation « app.events »
-- inexistante. Même contrainte pour toute autre écriture manuelle sur app."Events", un
-- DELETE compris.
--
-- ON CONFLICT porte sur "Id", et pas sur rien : c'est la seconde exécution de CE script qu'il
-- doit absorber. Un conflit sur (NameLower, Year) est un tout autre cas — un évènement
-- portant déjà ce nom cette année-là, à la casse près — et il doit continuer à lever.
INSERT INTO app."Events" ("Id", "Name", "Date", "OrderWindowStartDate", "OrderWindowEndDate")
VALUES ('01a13361-398e-7fcb-a44f-2774c3a1fd36'::uuid, 'Saint-Nicolas',
        '2026-12-06', '2026-10-01', '2026-11-22')
ON CONFLICT ("Id") DO NOTHING;

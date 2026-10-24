/*
    Évènement « Saint-Nicolas » — données de démonstration.

    POURQUOI CE SCRIPT N'EST PAS UNE MIGRATION
    Les tests d'intégration migrent la base de leur conteneur avec les migrations réelles
    (SqlServerContainerFixture → MigrateAsync). Une insertion portée par une migration
    apparaîtrait donc dans la base de TOUS les tests, et pourrait fausser leurs attendus —
    en particulier ceux de la lecture de l'évènement courant, qui interroge la table entière.
    Ce script vit donc hors migrations et s'applique À LA MAIN.

    APPLICATION
    Sur l'instance SQL Server locale, en authentification Windows :
        sqlcmd -S localhost -d EventsManager -E -i db\seed\saint-nicolas.sql
    Dans le conteneur « db » du docker-compose, sur une base déjà migrée :
        docker compose exec db bash -lc '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d EventsManager -i /seed/saint-nicolas.sql'
    Trois détails de cette commande, tous nécessaires : l'outil est sous /opt/mssql-tools18/bin
    et non /opt/mssql-tools ; -C fait accepter le certificat auto-signé du conteneur ; les
    guillemets simples laissent le shell DU CONTENEUR évaluer le mot de passe, au lieu de celui
    du poste. Sous Git Bash uniquement, préfixer par MSYS_NO_PATHCONV=1, sans quoi la conversion
    de chemins MSYS réécrit /opt/... en C:/Program Files/Git/opt/...
    Ou exécution directe dans SSMS / Azure Data Studio sur la base cible.
    Idempotent : une seconde exécution ne fait rien et ne lève pas d'erreur.

    IDENTIFIANT
    Littéral au format UUIDv7 (RFC 9562), généré une fois par Guid.CreateVersion7() et figé
    ici. Jamais NEWID() : ce serait un UUIDv4, et il changerait à chaque exécution, ce qui
    rendrait le script non idempotent et l'identifiant inutilisable dans un test manuel.

    COLONNES D'AUDIT
    AddedBy et AddedDate ne sont pas mentionnées : elles sont remplies par les contraintes
    DEFAULT (SUSER_SNAME(), SYSUTCDATETIME()) posées par la migration EventsAudit.

    FENÊTRE DE COMMANDE
    2026-10-01 → 2026-11-22, donc ouverte à la date de rédaction (2026-10-13) :
    GET /api/events/current rend un 200 juste après l'application du script.
    L'invariant début <= fin <= date de l'évènement est tenu, et les contraintes
    CK_Events_OrderWindow_* de la base le vérifient de toute façon.
*/

-- QUOTED_IDENTIFIER doit être ON dans le lot qui écrit : l'index unique (Name, Year) porte sur une
-- colonne calculée, et SQL Server refuse alors l'INSERT si l'option est OFF (Msg 1934). sqlcmd la
-- met à OFF par défaut là où SSMS la met à ON, ce qui a masqué le défaut jusqu'ici. Le script la
-- pose donc lui-même plutôt que de dépendre du drapeau -I du client. Même contrainte pour toute
-- autre écriture manuelle sur app.Events, un DELETE compris.
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

DECLARE @Id uniqueidentifier = CAST('01a13361-398e-7fcb-a44f-2774c3a1fd36' AS uniqueidentifier);

IF NOT EXISTS (SELECT 1 FROM app.Events WHERE Id = @Id)
BEGIN
    INSERT INTO app.Events (Id, Name, [Date], OrderWindowStartDate, OrderWindowEndDate)
    VALUES (@Id, N'Saint-Nicolas', '2026-12-06', '2026-10-01', '2026-11-22');

    PRINT 'Évènement Saint-Nicolas inséré.';
END
ELSE
BEGIN
    PRINT 'Évènement Saint-Nicolas déjà présent : aucune insertion.';
END

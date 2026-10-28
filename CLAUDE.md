# Projet : back-office événements + commandes

## But

Application web. Back-office (gestion d'événements et commandes, export Excel de stats) +
front public (formulaire de commande, QR de paiement EPC SEPA). Le projet est le terrain
d'apprentissage de l'agentic coding : maîtriser l'agentic est le but, l'appli est le véhicule.

## Stack

- API : C# / .NET 10, ASP.NET Core. EF Core 10. PostgreSQL.
- Front : Angular 22 (signals, standalone, Signal Forms, OnPush/Zoneless par défaut), Angular Material. Gestionnaire de paquets : **pnpm** (jamais `npm` ni `npx` — `pnpm dlx` remplace `npx`) ; détails dans `web/CLAUDE.md`.
- Design tokens : custom properties `--app-*` **maintenues à la main** dans `web/src/styles/_tokens.scss`. Elles comblent uniquement ce que Material 3 ne fournit pas (espacements, couleurs sémantiques succès/avertissement). Aucun outil de design en amont, aucune génération.
- Tests : xUnit + Testcontainers PostgreSQL (intégration), Playwright (E2E fin, parcours critiques uniquement).

## Architecture (Clean Architecture, 4 couches)

- `Domain` : entités, value objects, invariants. **NE DÉPEND DE RIEN.**
- `Application` : commands/queries + handlers, validators (FluentValidation), mappers (méthodes d'extension). Dépend de Domain.
- `Infrastructure` : EF Core, DbContext. Dépend de Application.
- `Api` : endpoints, DI. Dépend de Application (et Infrastructure pour le wiring).
- Un test d'architecture (NetArchTest) garantit ces dépendances à chaque build.

## Conventions NON NÉGOCIABLES

- **PAS de MediatR**, **PAS d'AutoMapper**.
- CQRS light : objets `Command`/`Query` + handler dédié, dispatch par **injection DI directe**.
- Mappers = méthodes d'extension (`ToDto()`, `ToEntity()`), **aucune logique métier dedans**, testés.
- DDD tactique léger : `Money`, `EmailAddress`, IDs typés (`EventId`, `OrderId`) ; agrégats `Event` et `Order` avec invariants garantis dans la racine d'agrégat. Pas de DDD stratégique, pas de bus de domain events.
- **IDs typés** : `readonly record struct` calqué sur `EventId` (`src/Domain/Events/EventId.cs`) — ctor privé, `New()` → `Guid.CreateVersion7()` (UUIDv7), `From(Guid)` qui rejette `Guid.Empty`. Pas d'UUIDv8/UUIDNext, pas de génération côté DB. Le tri chronologique côté SQL est porté par une colonne dédiée (ex. `Date` pour `Event`), jamais par l'ID. PostgreSQL trie pourtant `uuid` octet par octet de gauche à droite, donc un UUIDv7 y **est** chronologique (mesuré le 2026-10-12) : la colonne de tri explicite reste quand même la règle, parce qu'elle ne dépend d'aucun provider — c'est son motif, et il a survécu au changement de base.
- **Colonnes d'audit DB-only** : chaque table porte `AddedBy`, `AddedDate`, `UpdatedBy`, `UpdatedDate` (UTC), invisibles du code — ni Domain, ni modèle EF, pas même en shadow property. `Added*` = contraintes DEFAULT (`current_user`, `now() AT TIME ZONE 'utc'`) ; `Updated*` = trigger `BEFORE UPDATE`, qui écrit `NEW` directement — donc ni UPDATE récursif, ni garde de niveau d'imbrication. Pour chaque nouvelle table : compléter la migration à la main (colonnes + trigger, réf. migration `InitialCreate`), la fonction `app.set_audit_columns()` étant partagée par les trois tables et agnostique de la table. **Pas de `HasTrigger` dans la config fluent** : c'était un contournement de la clause OUTPUT de SQL Server, et Npgsql emploie `RETURNING`, que PostgreSQL accepte sur une table à trigger. L'y laisser serait inerte et trompeur.
- **Données de démonstration : jamais dans une migration.** Un script d'insertion vit dans
  `db/seed/`, en SQL, et s'applique **à la main**. Motif : les tests d'intégration migrent la base
  du conteneur avec les migrations réelles (`MigrateAsync`), donc toute donnée posée par une
  migration apparaîtrait dans la base de tous les tests et pourrait fausser leurs attendus. Un
  script de seed est idempotent (`ON CONFLICT ("Id") DO NOTHING`), porte ses identifiants en
  littéral au format UUIDv7 (jamais `gen_random_uuid()`, qui produirait un v4 et changerait à
  chaque exécution) et ne mentionne jamais les colonnes d'audit.
- **Frontière Api ↔ Domain** : l'Api ne référence jamais un type Domain (verrouillé par test d'architecture NetArchTest). Corollaire : les contrats de l'Application (`*Command`, `*Query`, `*Dto`, retours de handlers) n'exposent que des primitifs/BCL — la conversion (`EventId.From(...)`) vit dans les handlers, l'invariant reste garanti par le Domain.
- **Classes à dépendances injectées** (handlers, services…) : primary constructor **+** champs `private readonly` initialisés depuis les paramètres ; les méthodes n'utilisent que les champs, jamais les paramètres capturés (garantie de non-réassignation, pas de double capture CS9124). Référence : `CreateEventCommandHandler`. Le primary constructor est **obligatoire sauf impossibilité technique** — cas connu : un validator FluentValidation, dont les `RuleFor` doivent s'enregistrer dans le corps du constructeur (un primary constructor n'a pas de corps) ; on utilise alors un constructeur classique, le champ reste `private readonly` et l'esprit de la règle est respecté (réf. `CreateEventCommandValidator`). Cette exception est conforme et **ne doit pas être remontée en revue**.
- Tests : **pyramide**. Gros volume en unitaire (domaine) + intégration (handlers + EF). E2E fin (3-5 parcours critiques max).
- **Global usings** : toujours dans un fichier `GlobalUsings.cs` dédié à la racine du projet. Jamais via `<Using Include="...">` dans les csproj.
- **InternalsVisibleTo** : toujours dans un fichier `InternalsVisible.cs` dédié à la racine du projet concerné, sous forme d'attribut d'assembly : `[assembly: InternalsVisibleTo("EventsManager.UnitTests")]`. Jamais via `<InternalsVisibleTo Include="...">` dans les csproj.
- **Méthodes** : toujours en bloc `{ ... }`, jamais en expression body (`=>`), même pour les implémentations d'une ligne. La règle vise les **méthodes** uniquement, pas les propriétés : une propriété calculée à corps d'expression (ex. `DbSet<Event> Events => Set<Event>();`, idiome EF standard) est conforme et **ne doit pas être remontée en revue**.

## Documentation à jour (MCP Context7, Microsoft Learn, angular-cli)

Trois serveurs MCP déclarés dans `.mcp.json` à la racine, tous pré-approuvés dans `.claude/settings.json` : deux de documentation (`context7`, `microsoft-learn`) et un d'outillage (`angular-cli`). Entre les deux serveurs de doc, **périmètres disjoints** : ne jamais interroger l'un sur le domaine de l'autre, une même question ne doit pas avoir deux sources.

- **`microsoft-learn`** (HTTP, `https://learn.microsoft.com/api/mcp`, sans authentification) : tout l'écosystème Microsoft. .NET 10, ASP.NET Core, EF Core 10, outillage `dotnet`. Trois outils : `microsoft_docs_search` pour la recherche, `microsoft_code_sample_search` pour les exemples de code, `microsoft_docs_fetch` pour une page complète en markdown.
- **`context7`** : les librairies tierces hors Microsoft. FluentValidation, xUnit, Testcontainers, **Npgsql et son provider EF Core**, **PostgreSQL lui-même**, Angular 22, Angular Material, Vitest. Pour l'activer dans un prompt : ajoute `use context7` ou demande explicitement la doc de la librairie visée.

**Règle d'usage, non négociable.** Toute affirmation sur une API ou une configuration Microsoft **qui ne casse pas au build** doit être adossée à la doc first-party avant d'être écrite : configuration d'ASP.NET Core et ordre du pipeline, conventions et comportements EF Core. Les spécificités de PostgreSQL, de Npgsql et de son provider EF Core relèvent de `context7` — périmètres disjoints obligent — et la même règle s'y applique mot pour mot. Motif : le compilateur, les analyseurs et `/check` attrapent déjà la syntaxe et les signatures. L'angle mort qui reste est le code qui compile parfaitement et fait autre chose que prévu, et c'est exactement ce que ces serveurs couvrent. Un MCP disponible n'est pas un MCP utilisé : une réponse qui repose sur un souvenir de version est un défaut à signaler, pas un raccourci acceptable.

## Workflow de vérification

- **Toute nouvelle tranche commence par `/cadrage`**, avant tout plan et tout code. Si les critères d'acceptation de la tranche n'existent pas dans `docs/acceptance/`, le signaler et **s'arrêter là**. Motif : les valeurs attendues d'un test doivent venir d'une décision métier écrite avant le code. Écrites après, elles décrivent ce que le code fait et le valident contre lui-même.
- Après toute modification de code : lancer `/check` et corriger jusqu'au vert **avant de s'arrêter**. **3 passes maximum**, jamais plus : la boucle exacte, les interdits anti-triche et le critère d'arrêt sont définis dans `.claude/commands/check.md`, s'y référer.
- **Ordre de vérification, de bout en bout** : `/check` vert → `zalando-reviewer` (seulement si la surface d'API a changé) → `reviewer` → proposition de commit. Chaque étape suppose la précédente verte ; aucune ne se saute, aucune ne s'inverse.
- Les conventions de la surface HTTP (URL, codes de statut, format d'erreur, sérialisation JSON, pagination) vivent dans `docs/api-conventions.md`, pas ici. Toute décision qui touche à la forme de l'API s'y écrit **avant** d'être codée, et les règles Zalando écartées y restent listées avec leur motif.
- Pour toute tâche non triviale : proposer un **plan d'abord**, attendre validation, puis exécuter.

## Sous-agents

- **zalando-reviewer** : vérificateur de conformité de la surface HTTP au référentiel `docs/api-conventions.md` (sous-ensemble Zalando adopté). **Ordre non négociable : après `/check` vert, avant `reviewer`.** Motif : il vérifie un contrat externe sur du code déjà compilé et testé, et ce qu'il remonte peut changer la forme des endpoints que le `reviewer` relira ensuite. Invoqué **uniquement quand la surface d'API a changé** — un fichier touché sous `src/Api/**` ou un `*Dto.cs` de `src/Application/**`. Lecture seule, ne modifie jamais le code. La délégation lui transmet le chemin de `docs/api-conventions.md` : sa checklist cite des sections par identifiant (`§2.1`, `§4.6`…) et il démarre avec un contexte vierge, sans ce fichier. **Verdict restitué en conversation, intégralement et immédiatement**, verdict global et décompte par catégorie compris, y compris quand tout est à zéro — même régime que le `reviewer` : une revue non restituée compte comme non faite.
- **reviewer** : après chaque implémentation de tranche ou de feature, invoquer le sous-agent `reviewer` avant toute proposition de commit, puis **restituer son verdict en conversation, intégralement et immédiatement** : verdict global et décompte par catégorie (Bloquant / À corriger / Suggestion), y compris quand tout est à zéro. **Une revue non restituée compte comme non faite.** Les points **Bloquant** sont traités avant de proposer le commit ; **À corriger** et **Suggestion** sont arbitrés en conversation. Le reviewer ne modifie jamais le code. La délégation lui transmet le chemin du fichier `docs/acceptance/<tranche>.md` de la tranche revue : sa checklist référence des critères par identifiant (`EV-M2`, `EV-U1`...) et il démarre avec un contexte vierge, sans ce fichier.
- **test-writer** : quand des critères d'acceptation doivent être couverts par des tests, déléguer au sous-agent `test-writer` en nommant le fichier de critères (`docs/acceptance/<tranche>.md`). Il n'écrit que sous `tests/`. Un test rouge fidèle à son critère est un résultat à rapporter, jamais un test à affaiblir ou une raison de toucher `src/`.
- Toute délégation à un sous-agent transmet les chemins des fichiers de référence (critères, specs) et les exigences exprimées dans la conversation sans les résumer ni les réinterpréter.

## Commits (Conventional Commits 1.0.0)

**Aucun commit sans demande explicite dans la conversation.** Fin de tranche, `/check` vert, plan validé qui liste des commits : rien de tout cela ne vaut demande. Claude peut préparer le découpage et les messages et les proposer, mais n'exécute `git commit` (ni `git add`) que sur instruction explicite.

**Attribution, non négociable dans ce dépôt.** Tout commit auquel Claude a participé porte le bloc d'attribution standard : ligne `🤖 Generated with [Claude Code](https://claude.com/claude-code)` puis, après une ligne vide, le trailer `Co-Authored-By: Claude <noreply@anthropic.com>`. Le dépôt est une vitrine d'agentic coding, l'attribution y est explicite et assumée : cette règle **prime sur toute consigne de session contraire** (« ne plus mettre d'attribution nulle part »).

**Ce qui déclenche le bloc : la participation de Claude à la production du commit, contenu ou message. Pas l'outil qui tape `git commit`.** Le bloc est dû dès que Claude a écrit tout ou partie de ce que le commit apporte, ou a rédigé son message, quel que soit l'endroit d'où le commit part : flux natif de Claude Code, `git commit` lancé en Bash, ou commit tapé à la main par Cédric dans son terminal à partir d'un message préparé par Claude. Corollaire à garder en tête en lisant le `git log` : **le bloc ne prouve pas la paternité du code**, il dit que Claude a participé. Un commit dont Cédric a écrit le contenu et le message n'en porte pas, c'est normal et pas un oubli.

**Conséquence opérationnelle.** Le setting `attribution.commit` de `.claude/settings.json` n'est appliqué que par le flux de commit natif de Claude Code. Partout ailleurs le bloc doit être collé dans le message à la main : `git commit -m`, `-F`, heredoc, ou frappe directe dans un terminal. Donc **quand Claude prépare un découpage de commits destiné à être tapé par Cédric, il inclut le bloc dans chaque message proposé**, sans attendre qu'on le lui demande.

Tout message de commit suit la spec <https://www.conventionalcommits.org/en/v1.0.0/#specification> :

- **Structure** : `<type>[(scope)][!]: <description>`, puis corps optionnel et footers optionnels, chaque bloc séparé du précédent par une ligne vide.
- **Types** : `feat` (nouvelle fonctionnalité → SemVer MINOR), `fix` (correction de bug → PATCH). Autres types admis : `build`, `chore`, `ci`, `docs`, `style`, `refactor`, `perf`, `test`.
- **Scope** : optionnel, un nom entre parenthèses désignant la zone touchée, ex. `feat(domain):`, `fix(api):`.
- **Description** : obligatoire, immédiatement après `: `. Usage du repo : description et corps en français, concis, sans point final dans la description (types et scopes restent en anglais).
- **Breaking change** : `!` juste avant le `:` (`feat(api)!: ...`) et/ou footer `BREAKING CHANGE: <description>` (→ MAJOR). `BREAKING CHANGE` obligatoirement en majuscules ; `BREAKING-CHANGE` accepté comme équivalent en footer uniquement.
- **Corps** : libre, une ligne vide après la description ; y expliquer le *pourquoi* et les décisions non évidentes (cf. historique du repo).
- **Footers** : format git trailer `Token: valeur`, token multi-mots avec `-` (ex. `Reviewed-by:`), seule exception `BREAKING CHANGE`.
- Un commit = un seul type de changement : si plusieurs types s'appliquent, découper en plusieurs commits.

## Exécution locale en conteneurs

`docker-compose.yml` monte la pile complète : `db` (PostgreSQL 18.6, la même image que la
fixture Testcontainers), `api`, `web` (nginx), plus le service outil `migrate` rangé dans le
profil `tools`, que `docker compose up` ne démarre donc jamais. Rien ici ne vise la production :
le déploiement sur le NAS est une autre unité.

**Préalable, une fois.** Copier `.env.example` en `.env` et y mettre un mot de passe. PostgreSQL
n'impose **aucune** politique de complexité (contrairement à SQL Server), mais il doit être
**non vide** (l'image refuse de s'initialiser sans), **sans `$`** (compose interpole les valeurs
du `.env`) et **sans `;`** (il couperait la chaîne de connexion). `.env` n'est jamais versionné.

Le mot de passe de `appsettings.Development.json` n'est pas dans le fichier : la chaîne y est
volontairement sans mot de passe, et le cas « je lance l'API au F5, hors Docker » le prend dans
`dotnet user-secrets`. Attention, une chaîne de connexion **ne se fusionne pas par morceaux**
entre deux sources de configuration : le secret doit porter la clé entière
`ConnectionStrings:EventsManager`, mot de passe compris. Ce chemin est optionnel — tout tourne
en conteneur.

**Premier lancement, dans cet ordre.** Les migrations ne s'appliquent jamais au démarrage de
l'API, et le seed non plus : les deux sont des gestes explicites.

```bash
docker compose up -d db
docker compose run --rm migrate
MSYS_NO_PATHCONV=1 docker compose exec db bash -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=on -f /seed/saint-nicolas.sql'
docker compose up -d
```

`-v ON_ERROR_STOP=on` n'est pas décoratif : c'est l'équivalent du `-b` de `sqlcmd`. Sans lui,
psql poursuit après une erreur SQL et sort en 0, donc un seed à moitié appliqué passerait pour
un succès. Le préfixe `MSYS_NO_PATHCONV=1` ne sert que sous Git Bash, dont la conversion de
chemins réécrirait `/seed/...` en chemin Windows. Le seed passe par le client **du conteneur** :
aucun client PostgreSQL n'est à installer sur le poste. Contrôle :
`curl -i http://localhost:8080/api/events/current` rend un `200` à travers nginx. Hors de la
fenêtre de commande du seed, du 2026-10-01 au 2026-11-22, le `404` est le comportement correct
(`EV-014`) et non une panne.

**Relancement courant.** `docker compose up -d` et `docker compose down` : la base survit, elle
vit dans le volume nommé `db-data`. `docker compose down -v` est le geste de remise à zéro, et
impose de refaire `migrate` puis le seed. Après un changement de code, reconstruire avec
`docker compose up -d --build`. Une nouvelle migration s'applique par un
`docker compose run --rm migrate`, qui ne fait rien s'il n'y a rien à appliquer.

**Quatre comportements attendus, à ne pas prendre pour des défauts.** Le service `api` ne publie
aucun port : nginx est le seul point d'entrée, donc `http://localhost:8080/api/...` est le seul
chemin vers l'API. L'environnement reste `Production`, `MapOpenApi()` n'étant câblé qu'en
Development : le conteneur ne sert aucun document OpenAPI, c'est voulu. Et l'image d'exécution de
l'API est l'image `aspnet:10.0` **complète** et non une variante chiseled, qui n'embarque pas
tzdata : toute route lisant la date du jour y rendrait un `500` sur un conteneur pourtant « Up »
(voir le commentaire de `src/Application/TimeProviderExtensions.cs`).

Quatrième, apparu avec Npgsql et mesuré le 2026-10-25 : les conteneurs `api` et `migrate`
écrivent `Cannot load library libgssapi_krb5.so.2` suivi d'un `Error: ...` **à leur première
connexion, et une seule fois**. C'est la sonde GSSAPI de Npgsql, que l'image `aspnet:10.0`
n'embarque pas. Elle est **non fatale** et sans effet ici : l'authentification se fait par mot de
passe, la requête qui suit la ligne d'erreur s'exécute normalement, et le `migrate` sort en 0.
Le mot « Error » est trompeur, pas le comportement. Rien n'est ajouté à l'image pour le faire
taire — ce serait embarquer Kerberos pour une fonction qu'on n'utilise pas. À reconsidérer dans
l'unité de déploiement NAS, où l'hygiène des journaux compte.

## Déploiement sur le NAS Synology

Premier déploiement réussi le 2026-10-28. L'application tourne sur la machine qui la servira,
avec la base qui la servira, et la page de l'évènement courant est joignable **depuis le réseau
local**, sur le port **8080** du NAS.

**Ce que ce déploiement n'atteint pas, et il faut le dire :** un parent ne peut pas encore y
accéder. L'exposition publique demande un nom de domaine, un certificat et le reverse proxy de
DSM — zéro Docker, trois réglages dans DSM. C'est une unité à part, et la confondre avec
celle-ci ferait croire la cible atteinte alors que personne d'extérieur ne pourrait commander.
La page a été ouverte **depuis une autre machine du réseau** le 2026-10-28, et non depuis le seul
poste de développement : le relais nginx, la route réseau et l'indépendance au poste sont donc
tous les trois prouvés, ce qui était l'objet du critère.

**La cible, et où elle est décrite.** Les coordonnées d'accès — adresse, port SSH, utilisateur,
chemin de la clé — ne sont **plus** dans les fichiers versionnés. Elles vivent dans
`deploy-nas.local.psd1`, dont `deploy-nas.local.example.psd1` est le modèle, et une seconde fois
dans `.claude/settings.local.json`, qui porte les règles de permission des commandes distantes et
dont `.claude/settings.local.example.json` est le modèle. Les deux fichiers réels sont ignorés par
git ; leurs deux modèles sont versionnés, de sorte qu'un clone neuf sache quoi reconstituer.

**Mais « plus » ne veut pas dire « jamais », et il faut le dire ici plutôt que le découvrir.** Les
quatre commits qui ont introduit cette section — `c4cb0e6` à `2ea0666`, **déjà poussés** — portent
les valeurs en clair. Un `git log -p -- CLAUDE.md .claude/settings.json` les rend toujours, sur le
dépôt distant. Le retrait vaut donc pour la suite, pas pour le passé, et **ces coordonnées sont à
considérer comme publiques**. Réécrire l'historique d'une branche déjà publiée n'est pas la
réponse proportionnée pour des données de ce niveau — aucune clé, aucun mot de passe, et une
adresse RFC 1918 non routable. La réponse proportionnée est côté NAS, et elle prend une minute :
**décaler le port SSH**, ce qui périme l'information publiée. Tant que ce n'est pas fait, le port
et l'utilisateur sont connus de qui lit le dépôt, ce qui ne coûte rien aujourd'hui et coûtera le
jour où le NAS sera exposé — l'unité suivante.

L'utilisateur SSH doit porter une règle sudoers NOPASSWD sur `/usr/local/bin/docker`, et le
**chemin absolu** de `docker` est obligatoire : le `secure_path` de sudo n'inclut pas
`/usr/local/bin`. Tout vit dans `/volume1/docker/events-manager/`, qui est aussi le **répertoire
de projet** de compose — c'est de lui que viennent le `.env` lu automatiquement et la résolution
de `./seed`.

**Ce qui n'est pas versionné et vit uniquement sur le NAS :** le `.env`, en `-rw-------`. Son
mot de passe est généré **par le NAS lui-même** et ne quitte jamais le NAS : il est donc
différent de celui du poste par construction. `docker-compose.nas.yml` du dépôt est la **copie
de référence** de la pile ; il ne porte que des `${POSTGRES_*}`, jamais une valeur.

**Déploiement courant.** `./deploy-nas.ps1` lit ses coordonnées dans `deploy-nas.local.psd1`,
construit, synchronise le compose et le seed, pousse les trois images et démarre la pile, puis
**lit la réponse** de l'API et de la page. `-SkipBuild` redéploie sans reconstruire ; `-Migrate`
applique les migrations avant le démarrage ; chaque coordonnée reste surchargeable par le
paramètre du même nom (`-NasHost`, `-Port`, `-User`, `-IdentityFile`). Seules ces quatre clés sont
lues dans le fichier local : `RemoteDir` et `BaseUrl` sont des paramètres, et les y écrire n'aurait
aucun effet. Le script vérifie la présence du `.env` avant de démarrer quoi que ce soit, et n'y
touche **jamais**.

**Premier déploiement sur une cible vierge**, dans cet ordre. Chaque bloc ci-dessous porte ses
propres affectations et se colle donc tel quel : les variables ne survivraient pas d'un bloc à
l'autre, et un `$NAS_USER` vide ne produirait pas une erreur lisible mais une cible silencieusement
fausse. Elles sont préfixées `NAS_` pour la même raison — `USER` est déjà peuplé par le shell avec
le login local, et une collision aurait fait pointer la commande vers le mauvais compte sans rien
signaler.

Le secret est fabriqué sur place : il n'est ni saisi, ni transmis, ni affiché, et personne n'a à
le connaître — `docker compose exec db psql` n'en a pas besoin, et il reste lisible sur le NAS si
le besoin s'en présente. Noter le partage du travail entre les deux shells : `$NAS_PORT`,
`$NAS_KEY`, `$NAS_USER` et `$NAS_HOST` sont hors des guillemets simples, donc évalués **sur le
poste**, tandis que `$(openssl rand -hex 24)` est à l'intérieur, donc évalué **sur le NAS** —
c'est exactement ce qui fait que le mot de passe ne traverse jamais le réseau.

```bash
NAS_HOST=<adresse>; NAS_PORT=<port>; NAS_USER=<utilisateur>; NAS_KEY=~/.ssh/<clé>
ssh -p $NAS_PORT -i $NAS_KEY $NAS_USER@$NAS_HOST 'mkdir -p /volume1/docker/events-manager && umask 077 && printf "POSTGRES_DB=EventsManager\nPOSTGRES_USER=postgres\nPOSTGRES_PASSWORD=%s\n" "$(openssl rand -hex 24)" > /volume1/docker/events-manager/.env && chmod 600 /volume1/docker/events-manager/.env'
```

```bash
./deploy-nas.ps1 -Migrate
```

```bash
NAS_HOST=<adresse>; NAS_PORT=<port>; NAS_USER=<utilisateur>; NAS_KEY=~/.ssh/<clé>
ssh -p $NAS_PORT -i $NAS_KEY $NAS_USER@$NAS_HOST 'sudo /usr/local/bin/docker compose -f /volume1/docker/events-manager/docker-compose.yml exec -T db psql -U postgres -d EventsManager -v ON_ERROR_STOP=on -f /seed/saint-nicolas.sql'
```

`openssl rand -hex 24` plutôt qu'un base64 : l'hexadécimal ne contient ni `$` ni `;`, les deux
caractères que `.env.example` interdit, donc aucune contrainte de jeu de caractères à vérifier.
`ON_ERROR_STOP=on` reste l'équivalent du `-b` de `sqlcmd` — sans lui, psql poursuit après une
erreur et sort en 0, donc un seed à moitié appliqué passerait pour un succès. Le `-T`, en
revanche, est de la **prudence et non une nécessité** : mesuré le 2026-10-28, ni `exec` ni
`run --rm` n'en ont besoin derrière un `ssh` non interactif sur compose v2.20.1, qui détecte
l'absence de TTY et s'y adapte. On le garde parce qu'il est explicite et qu'il protège d'une
version de compose moins tolérante, pas parce qu'il débloque quoi que ce soit ici.

**Six comportements à ne pas prendre pour des défauts.** Les quatre premiers tiennent à l'écart
entre les deux démons : le poste est en **Docker 29.7.2, compose v5.4.0, magasin d'images
containerd** ; le NAS en **Docker 24.0.2, compose v2.20.1, btrfs**.

1. **Pas de `gzip` dans le tube.** Mesuré le 2026-10-26 sur les trois images : 236 316 672 octets
   bruts contre 234 748 013 gzippés, soit **0,66 %**. La cause est le **magasin d'images
   containerd du poste**, qui conserve les couches telles que le registre les a livrées, donc
   compressées — la documentation Docker l'écrit, « containerd stores images in both compressed
   and uncompressed formats ». Ce n'est **pas** une propriété du numéro de version, et la nuance
   compte : un poste revenu au pilote de stockage classique pourrait produire un tar non
   compressé, et les ~840 Mo redeviendraient d'actualité sans que rien n'alerte. Si le magasin
   change, **re-mesurer avant de conclure**.
2. **Construire les trois images en une passe n'est pas cosmétique.** `api` et `migrate`
   partagent l'étage `runtime-base` du Dockerfile, mais construites à des moments différents
   elles reposent sur des tirages différents d'`aspnet:10.0` et ne partagent alors **aucune**
   couche. Mesuré : 225,4 MiB d'archive. Reconstruites en une passe, 8 couches sur 9 sont
   communes et l'archive tombe à **133,1 MiB**. D'où le `docker compose --profile tools build
   --pull` unique du script. Le `--profile tools` est obligatoire : sans lui `migrate` est exclu
   du build, son service étant dans un profil.
3. **Le démon 24.0.2 du NAS charge bien l'archive OCI du poste** — mesuré le 2026-10-28, avec et
   sans `--platform`. Le script garde `--platform linux/amd64` par hygiène : il aplatit l'index et
   écarte le manifeste d'attestation `unknown/unknown` que BuildKit ajoute et dont le NAS n'a
   aucun usage. Ce n'est pas une nécessité, c'est un choix.
4. **`docker compose ps --format` ne prend pas de template Go** sur compose v2.20.1, alors que la
   v5.4.0 du poste l'accepte. Pour une preuve précise des ports publiés, passer par le démon :
   `docker inspect --format '{{json .HostConfig.PortBindings}}'`.
5. **`scp` nu échoue** sur `subsystem request failed on channel 0`. OpenSSH moderne fait passer
   `scp` par **SFTP**, que le serveur SSH de DSM n'expose pas. Le drapeau **`-O`** force le
   protocole SCP historique et règle la question. Repli sans aucune dépendance :
   `ssh ... 'cat > /chemin/cible' < fichier.local`, le même tube que pour les images.
6. **`#Requires -Version 7.4` dans `deploy-nas.ps1`, et pas 7.2.** `about_Pipelines` l'écrit :
   « Before PowerShell 7.4, piping or redirecting output from a native program that outputs raw
   byte data converted the output to .NET strings. This conversion caused corruption of the raw
   data output », et « In PowerShell 7.4 or higher, the `PSNativeCommandPreserveBytePipe`
   experimental feature is mainstream ». Le `docker save | ssh` du script en dépend entièrement.

**Un piège qui ne vient d'aucune version : le `.env` manquant.** Une variable non définie
s'interpole en **chaîne vide**. Compose le signale — mesuré le 2026-10-28, il écrit sur stderr
`The "POSTGRES_PASSWORD" variable is not set. Defaulting to a blank string.` —, mais il
**n'échoue pas pour autant** : `config` sort en 0 et rend `POSTGRES_PASSWORD: ""`. La panne
n'apparaît qu'ensuite, quand l'image postgres refuse de s'initialiser sans mot de passe, et son
message ne désigne pas la cause. L'avertissement existe donc, mais il se noie dans la sortie d'un
`up` : c'est pourquoi `deploy-nas.ps1` contrôle explicitement la présence du `.env` avant de
démarrer.

**Mesures du 2026-10-28, pile debout.** La pile consomme **101,5 MiB** au total — `web` 5,16,
`api` 51,84, `db` 44,5 — sur un NAS qui dispose de **18 Go libres** sur 19,4. `api` et `db` ne
publient **aucun port** (`PortBindings` vide) : nginx, sur 8080, est le seul point d'entrée. Les
trois services portent `restart: unless-stopped` ; `migrate` non, car il est lancé par
`run --rm`, et `--rm` supprime le conteneur même si une politique est déclarée.

**Hors de cette unité, et à ne pas perdre.** L'exposition publique. La **sauvegarde du volume** —
aujourd'hui `events-manager_db-data` est la seule copie des commandes des parents ; à trancher
avant les premières vraies commandes : `pg_dump` périodique vers un partage, ou snapshots DSM. Et
le **login applicatif à droits restreints** : `.env.example` renvoie ce travail à « l'unité de
déploiement sur le NAS », alors que la pile y tourne avec le superutilisateur `postgres` et que
le référentiel de l'unité ne le mentionnait pas. Le point reste donc ouvert, et ce paragraphe est
là pour qu'il ne se découvre pas par surprise.

## Structure du repo

```
/
  CLAUDE.md
  global.json                # pin du SDK .NET 10
  .mcp.json                  # serveurs MCP de scope projet (Context7, angular-cli, Microsoft Learn)
  Directory.Build.props      # global : TreatWarningsAsErrors, EnforceCodeStyleInBuild, SonarAnalyzer
  Directory.Packages.props   # Central Package Management, versions NuGet épinglées
  .editorconfig              # règles de style, bloquantes au build
  .gitattributes             # autorité unique sur les fins de ligne (LF au dépôt)
  .git-blame-ignore-revs     # masque le commit de normalisation des fins de ligne
  events-manager.slnx        # solution à la racine
  docker-compose.yml         # pile locale : db, api, web, plus l'outil migrate (profil tools)
  docker-compose.nas.yml     # copie de référence de la pile déployée sur le NAS
  deploy-nas.ps1             # build, poussée par tube SSH et démarrage de la pile sur le NAS
  deploy-nas.local.example.psd1  # modèle des coordonnées du NAS ; le .local.psd1 n'est PAS versionné
  .dockerignore              # contexte de l'image API (la racine) ; le front a le sien sous web/
  .env.example               # modèle du .env local, non versionné (voir .gitignore)
  .config/
    dotnet-tools.json        # manifeste d'outils .NET locaux (dotnet-ef)
  .claude/
    commands/                # /check, /cadrage
    agents/                  # reviewer, zalando-reviewer, test-writer, acceptance-writer
    skills/                  # design-ui
    launch.json              # serveur de dev front (prévisualisation), cible `web`
    settings.json            # permissions, MCP pré-approuvés, attribution
    settings.local.json      # NON VERSIONNÉ : permissions des commandes distantes vers le NAS
    settings.local.example.json  # modèle du précédent, lui VERSIONNÉ
  db/
    seed/                    # scripts SQL de données, appliqués À LA MAIN, hors migrations
  docs/
    acceptance/              # critères d'acceptation par tranche (source du test-writer)
    api-conventions.md       # conventions HTTP, sous-ensemble Zalando (oracle du zalando-reviewer)
    openapi.json             # GÉNÉRÉ au build et versionné, ne jamais éditer à la main
    harnais.html             # schéma du harnais agentic, à tenir à jour à chaque évolution
  src/                       # projets .NET uniquement
    Api/
      Dockerfile             # image de l'API ; contexte de build = la racine du dépôt
    Application/
    Domain/
    Infrastructure/
  web/                       # app Angular, avec son propre CLAUDE.md
    Dockerfile               # build pnpm puis service des statiques par nginx
    .dockerignore
    docker/
      nginx.conf             # relais /api/ vers le conteneur api, repli SPA
  tests/
    UnitTests/
    ApiTests/                # pipeline HTTP via WebApplicationFactory, sans Docker
    IntegrationTests/
    e2e/                     # Playwright (semaine 12)
```

## Style de réponse attendu

Direct, sans fioritures. Explications techniques complètes et explicites.
Challenge les choix : si une direction est mauvaise ou qu'il y a mieux, le dire immédiatement.

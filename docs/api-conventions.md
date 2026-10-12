# Conventions d'API — sous-ensemble Zalando adopté

> Oracle du sous-agent `zalando-reviewer`, comme `docs/acceptance/*.md` l'est pour
> `test-writer`. Ce document énonce les **décisions du projet**, pas une copie du guide :
> les numéros entre parenthèses (`Zalando 118`) renvoient à la règle d'origine dans les
> [Zalando RESTful API Guidelines](https://opensource.zalando.com/restful-api-guidelines/),
> ils justifient la décision, ils ne la remplacent pas.
>
> Périmètre : une API JSON synchrone CRUD, un seul client (le back-office Angular, puis le
> front public). Tout ce que le guide prévoit hors de ce cadre est écarté explicitement en
> §8, avec un motif d'une ligne. Un périmètre choisi, pas subi.
>
> Décisions du cadrage du 2026-09-07, complétées le 2026-09-10.

## Sommaire

| § | Sujet |
|---|---|
| §1 | Ressources et URL |
| §2 | Sérialisation JSON |
| §3 | Codes de statut |
| §4 | Réponses d'erreur : Problem JSON |
| §5 | Types de médias |
| §6 | Pagination |
| §7 | Spécification OpenAPI |
| §8 | Règles Zalando écartées |
| §9 | Confrontation à l'existant |

---

## 1. Ressources et URL

- **§1.1** — Nom de ressource au **pluriel** (Zalando 134). `/events`, jamais `/event`.
- **§1.2** — Segments de chemin en **kebab-case ASCII**, `^[a-z][a-z\-0-9]*$` (Zalando 129).
  Pas de majuscule, pas d'underscore, pas d'accent dans une URL.
- **§1.3** — **URL sans verbe** (Zalando 141). L'action est portée par la méthode HTTP, jamais
  par un segment. `POST /events` et non `POST /events/create-event`.
- **§1.4** — Sous-ressources par **segments de chemin**, motif
  `/ressources/{id}/sous-ressources/{id}` (Zalando 143). Trois niveaux d'imbrication maximum
  (Zalando 147).
- **§1.5** — Paramètres de requête en **snake_case** (Zalando 130), et conventionnels quand un
  nom conventionnel existe : `cursor`, `limit` (Zalando 137). Pas de `pageSize`, pas de `page`.
- **§1.6** — **Aucun versioning d'API.** Un seul client, déployé avec le serveur : le coût du
  versioning n'a pas de contrepartie. Conforme à Zalando 113 (*avoid versioning*) et 115
  (*not use URL versioning*). Une évolution incompatible se traite par une nouvelle ressource,
  pas par un `/v2`.

### Routes de la tranche 10a

| Verbe | Chemin | Rôle |
|---|---|---|
| `GET` | `/api/events` | liste des événements (objet de page, §6) |
| `GET` | `/api/events/{id}` | lecture par identifiant |
| `POST` | `/api/events` | création |
| `PUT` | `/api/events/{id}` | modification |
| `DELETE` | `/api/events/{id}` | suppression |

Le préfixe `/api` est une **déviation assumée** de Zalando 135, motivée en §8.

---

## 2. Sérialisation JSON

- **§2.1** — Toutes les propriétés JSON en **snake_case**, `^[a-z_][a-z_0-9]*$`
  (Zalando 118). `customer_name`, `unit_price`, `event_id`. Jamais de camelCase, jamais de
  PascalCase. Réglage : `PropertyNamingPolicy` **et** `DictionaryKeyPolicy` à
  `JsonNamingPolicy.SnakeCaseLower`, sur les deux jeux d'options (voir §2.7).
- **§2.2** — **Un champ sans valeur est absent du JSON**, jamais émis à `null`. Zalando 123
  impose de traiter `null` et absent comme équivalents ; on prend donc la forme la plus propre
  et on n'en émet qu'une. Réglage : `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`.
  Corollaire pour le client : l'absence d'un champ optionnel n'est **pas** une erreur.
- **§2.3** — **Un tableau vide est `[]`**, jamais absent, jamais `null` (Zalando 124). §2.2 ne
  s'applique pas aux collections : une collection vide est une information, une collection
  absente en serait une autre, on n'en veut qu'une.
- **§2.4** — Noms de tableaux au **pluriel** (Zalando 120) : `items`, `lines`, `errors`.
- **§2.5** — **Dates.** Une `DateOnly` se sérialise en ISO 8601 date, `"2026-09-12"` — le
  comportement natif de `System.Text.Json`, aucun convertisseur maison. Le nom de la propriété
  porte le mot `date`, `time` ou `timestamp`, ou se termine par `_at` (Zalando 235) : `date`,
  `order_window_start_date`, `created_at`.
- **§2.6** — **Le corps d'une réponse est toujours un objet JSON**, jamais un tableau au niveau
  racine (Zalando 110). Une collection est enveloppée dans l'objet de page de §6. Motif : un
  tableau nu n'a nulle part où accueillir un champ supplémentaire, toute évolution y est
  cassante.

### §2.7 — Où vivent ces réglages

Deux jeux d'options distincts, qui doivent dire la même chose :

| Options | Configurées par | Qui les lit |
|---|---|---|
| `Microsoft.AspNetCore.Mvc.JsonOptions` | `AddControllers().AddJsonOptions(...)` | les formateurs MVC, donc **les corps de réponse des endpoints** |
| `Microsoft.AspNetCore.Http.Json.JsonOptions` | `ConfigureHttpJsonOptions(...)` | `DefaultProblemDetailsWriter`, donc **les réponses d'erreur des middlewares** |

N'en configurer qu'un produit une API qui sérialise ses succès et ses erreurs dans deux
conventions différentes. `src/Api/Program.cs` les configure tous les deux avec la même
fonction.

---

## 3. Codes de statut

Seuls les codes de ce tableau sont émis (Zalando 150 : *only use most common status codes*).
Un code absent de ce tableau qui apparaît dans le code est un défaut.

| Code | Quand |
|---|---|
| `200 OK` | `GET` réussi ; `PUT` réussi, avec la ressource à jour dans le corps |
| `201 Created` | `POST` réussi. **Corps = la ressource créée, header `Location` obligatoire** vers son URL |
| `204 No Content` | `DELETE` réussi. Aucun corps |
| `400 Bad Request` | tout payload invalide (§3.5) |
| `404 Not Found` | identifiant inconnu |
| `409 Conflict` | l'état courant de la ressource interdit l'opération (§3.5) |
| `500 Internal Server Error` | exception non gérée. Jamais volontaire |

- **§3.1** — **Pas de `422`.** Zalando le classe *do-not-use* : `400` et `422` couvrent les
  mêmes cas et deux codes pour une même situation coûtent plus qu'ils ne rapportent.
- **§3.2** — `PUT` sur un identifiant inconnu → **`404`**, jamais de création implicite :
  l'identifiant est généré côté serveur, un client ne peut pas en inventer un.
- **§3.3** — Le `409` est **documenté** dans l'OpenAPI de chaque opération susceptible de
  l'émettre (exigence explicite de Zalando pour ce code).
- **§3.4** — Le code le plus spécifique gagne (Zalando 220).

### §3.5 — La ligne de partage 400 / 409

**Le payload est mauvais en soi → `400`. La ressource est dans un état qui interdit
l'opération → `409`.** Le test : *est-ce que la même requête aurait pu réussir sur une autre
ressource, ou à un autre moment ?* Si oui, c'est un `409`.

| Situation (tranche Event) | Code |
|---|---|
| nom absent, vide, ou 101 caractères (EV-N1, EV-N3) | `400` |
| date absente ou malformée | `400` |
| une seule des deux dates de fenêtre renseignée (EV-W2) | `400` |
| ordre de fenêtre invalide, début > fin (EV-W3) | `400` |
| date de l'événement à J+0 ou avant (EV-D2, EV-D3) | `400` |
| identifiant inconnu (EV-M7, EV-S5) | `404` |
| nom déjà pris pour l'année civile (EV-U1) | `409` |
| date déjà prise par un autre événement (EV-U3) | `409` |
| événement passé, donc figé (EV-M1) | `409` |
| début de fenêtre atteint, donc figé (EV-W7, EV-W8) | `409` |
| suppression bloquée par une fenêtre ouverte (EV-S1, EV-S2) | `409` |
| suppression d'un événement passé (EV-S3) | `409` |

Les collisions d'unicité sont des `409` et non des `400` : le payload est valide, c'est le
contenu de la base qui le refuse.

---

## 4. Réponses d'erreur : Problem JSON

Toute réponse `4xx` et `5xx` sort en **`application/problem+json`** (Zalando 176, RFC 9457) —
y compris une exception non gérée et un statut nu produit par le routage. Aucune réponse
d'erreur en texte brut, aucune réponse d'erreur au corps vide. **Portée exacte : tout client
qui accepte le JSON**, ce qui couvre tout client réel du projet ; la limite mesurée est
consignée en §9.4.

| Membre | Contenu |
|---|---|
| `type` | `/problems/<slug>`, **URI relative**. C'est le **discriminant machine** du front |
| `title` | court, **stable par `type`**, en français. Ne dépend jamais de l'occurrence |
| `status` | le code HTTP, recopié |
| `detail` | français, **spécifique à l'occurrence**. C'est ici que va ce qui varie |
| `instance` | le chemin de la requête |
| `trace_id` | extension. Identifiant de corrélation avec les logs serveur |
| `errors` | extension. Dictionnaire `champ → messages`, sur les `400` de validation |

- **§4.1** — `type` est **relatif** (`/problems/name-taken`), jamais absolu. Zalando 176 le
  recommande explicitement pour `type` et `instance`. C'est aussi ce qui rend le discriminant
  indépendant de l'hôte de déploiement.
- **§4.2** — **Pas de champ `code`.** `type` est le seul discriminant machine. Deux
  discriminants dériveraient l'un de l'autre.
- **§4.3** — Le slug de `type` désigne une **catégorie de problème**, pas un message. Deux
  occurrences du même problème partagent `type` et `title`, et ne diffèrent que par `detail`.
- **§4.4** — **Tous les messages sont en français**, `title` comme `detail`. Y compris ceux
  produits par le framework : le titre anglais par défaut d'ASP.NET Core est traduit par le
  customizer.
- **§4.5** — **Jamais de stack trace, jamais de message d'exception brut** dans une réponse
  (Zalando 177). Un `500` dit qu'une erreur interne est survenue et donne un `trace_id` ; le
  détail vit dans les logs serveur. **Portée : hors `Development`.** En environnement de
  développement, `WebApplication` insère de lui-même sa page d'exception de développement en
  tête de pipeline ; c'est une commodité voulue du framework, qu'on garde. Les tests du
  contrat d'erreur tournent donc en `Production`, seul environnement où la garantie doit
  tenir.
- **§4.6** — `trace_id` vaut `Activity.Current?.Id` s'il existe, sinon
  `HttpContext.TraceIdentifier`. Clé écrite **`trace_id`** et non `traceId` : snake_case comme
  le reste (Zalando 118). Le nommage de cette clé est **littéral** —
  `ProblemDetails.Extensions` porte `[JsonExtensionData]`, dont les clés sont écrites verbatim,
  hors de portée de toute policy de nommage.
  **Corollaire non évident, découvert par le test.** ASP.NET Core pose de lui-même une
  extension `traceId`, en camelCase. Régler la policy de nommage ne la corrige pas — voir
  ci-dessus — et la laisser produirait deux clés pour une même valeur, dont une en violation
  de §2.1. Le customizer la **retire** avant d'écrire `trace_id`. C'est un renommage, pas un
  doublon : la valeur est identique.

### §4.7 — Catalogue des slugs

Slugs de catégorie dérivés du code de statut. La table du code
(`src/Api/ProblemCatalogue.cs`) et celle-ci ne doivent jamais diverger.

| Statut | `type` | `title` |
|---|---|---|
| `400` | `/problems/validation` | Requête invalide |
| `404` | `/problems/not-found` | Ressource introuvable |
| `405` | `/problems/method-not-allowed` | Méthode HTTP non autorisée |
| `409` | `/problems/conflict` | Conflit avec l'état de la ressource |
| `415` | `/problems/unsupported-media-type` | Type de média non supporté |
| `500` | `/problems/internal-error` | Erreur interne |
| tout autre `4xx` | `/problems/client-error` | Requête refusée |
| tout autre `5xx` | `/problems/internal-error` | Erreur interne |

`405` et `415` ne sont jamais émis volontairement (§3) : ils sont produits par le routage et
par la négociation de contenu. Ils figurent ici parce qu'ils **atteignent réellement le
client** — mesuré le 2026-09-11, tous deux sortent en `application/problem+json` — et qu'un
titre anglais du framework y serait une violation de §4.4. Chacun est pincé par un test.

Pas de ligne `406` : `ReturnHttpNotAcceptable` vaut `false` par défaut, une requête dont
l'`Accept` exclut le JSON reçoit un `200 application/json`, pas un `406`. Mesuré le
2026-09-11. Une entrée pour ce code serait du code mort ; elle reviendra le jour où la
négociation stricte sera activée.

Les deux dernières lignes du tableau sont le filet : aucun code, même imprévu, ne sort avec le
titre du framework. Elles sont pincées par un test direct du catalogue, puisque aucune requête
ne les produit aujourd'hui.

**Règle d'extension.** Un endpoint peut poser un slug plus fin (`/problems/name-taken`,
`/problems/event-frozen`) en renseignant `type` **et** `title`. Tout slug fin ajouté en 10a se
déclare dans ce tableau.

Le customizer de `Program.cs` arbitre les deux membres **indépendamment** :

- `type` n'est remplacé que s'il est nul ou s'il ne commence pas par `/problems/`. Un slug
  relatif déjà posé n'est jamais écrasé ; l'URI RFC absolue du framework l'est toujours.
- `title` est remplacé dès qu'il est nul **ou qu'il est exactement celui qu'ASP.NET Core aurait
  posé pour ce statut** — comparaison faite à la source, sur `ApiBehaviorOptions.ClientErrorMapping`,
  jamais sur une liste de phrases anglaises recopiée qui divergerait en silence.

La séparation n'est pas cosmétique : le framework **remplit** `title` (« Conflict »,
« Not Found »), il ne le laisse pas nul. Lier le sort du titre à celui du type laissait donc
sortir un titre anglais dès qu'un endpoint posait un slug fin sans titre — mesuré le
2026-09-11, corrigé, et pincé par un test. Écrire un titre non français reste possible, mais
c'est devenu un acte délibéré.

---

## 5. Types de médias

- **§5.1** — Requêtes et réponses de succès : **`application/json`** (Zalando 167, 172).
- **§5.2** — Réponses d'erreur : **`application/problem+json`**.
- **§5.3** — Aucun type de média maison, aucun paramètre de version sur le type de média
  (conséquence de §1.6).

---

## 6. Pagination

**Mécanisme : keyset / curseur. Jamais d'offset** (Zalando 159, 160). Un `OFFSET` fait payer
au serveur les lignes qu'il saute, et décale la fenêtre dès qu'une ligne est insérée ou
supprimée entre deux appels.

### §6.0 — Paramètres de requête

| Paramètre | Règle |
|---|---|
| `cursor` | opaque. Absent = première page |
| `limit` | défaut **20**, maximum **100**, **plafonné côté serveur** — une valeur supérieure est ramenée à 100, pas rejetée |

### Le curseur

- **§6.1** — Le curseur est **opaque** : Base64Url d'un objet JSON. Le client ne l'inspecte
  jamais, ne le construit jamais, ne le stocke pas au-delà d'une session de navigation
  (Zalando 160). Sa structure interne peut changer sans préavis, c'est tout l'intérêt.
- **§6.2** — Contenu du curseur :
  - `position` : la clé de tri de la dernière ligne rendue, plus un identifiant unique en
    tiebreaker quand la clé de tri n'est pas unique à elle seule ;
  - `direction` : `next` ou `prev` ;
  - `filters` : les filtres de requête appliqués. **Absent tant que la liste n'a pas de
    filtre** — un champ vide n'apporterait rien et §2.2 l'interdit.
- **§6.3** — Un curseur illisible ou incohérent avec les filtres de la requête → `400`.

### L'objet de page

Réponse d'une liste (Zalando 248), toujours un objet (§2.6) :

    {
      "items": [],
      "self": "https://api.example.test/api/events?limit=20",
      "next": "https://api.example.test/api/events?cursor=eyJ...&limit=20"
    }

- **§6.4** — `items` est le **seul membre requis**. Toujours présent, `[]` si la page est vide.
- **§6.5** — `self` toujours présent. `next` présent **seulement s'il existe une page
  suivante**. `prev` présent **seulement si ce n'est pas la première page**. Un lien absent
  signifie « il n'y a rien de ce côté ».
- **§6.6** — Les liens sont des **URL absolues** que le client suit tel quel, jamais des
  curseurs bruts à recomposer (Zalando 161, 217). C'est la seule entorse au principe
  « relatif » de §4.1 : Zalando 217 est un MUST pour les contrôles hypertexte, Zalando 176
  recommande le relatif pour le Problem JSON. Deux règles différentes, appliquées chacune
  chez elle.
- **§6.7** — **Pas de `first` ni de `last`** : les calculer demande de connaître la taille de
  la collection, exactement ce que le curseur évite.
- **§6.8** — **Pas de total** (Zalando 254). Une fois qu'un client en dépend, le retirer
  devient impossible.

### La requête EF Core

Écrite **à la main**, pas de librairie de pagination.

    var page = await query
        .OrderBy(x => x.Cle).ThenBy(x => x.Id)
        .Where(x => x.Cle > derniereCle || (x.Cle == derniereCle && x.Id > dernierId))
        .Take(limit + 1)
        .ToListAsync(cancellationToken);

- **§6.9** — Le `Take(limit + 1)` sert à **détecter l'existence d'une page suivante** sans
  `COUNT` : si la requête rend `limit + 1` lignes, `next` existe ; la ligne excédentaire est
  jetée avant sérialisation.
- **§6.10** — La forme `a > x || (a == x && b > y)` est **obligatoire**. SQL Server sait
  comparer des row-values (`WHERE (a, b) > (@x, @y)`), plus court et plus efficace, mais **EF
  Core ne sait pas l'exprimer en LINQ**
  ([efcore#26822](https://github.com/dotnet/efcore/issues/26822)). Écrire la forme développée
  n'est pas une maladresse, c'est la seule qui se traduise.
- **§6.11** — **Index composite obligatoire** sur `(clé de tri, id)`, dans l'ordre exact du
  `ORDER BY`. Sans lui la pagination keyset perd tout son intérêt.
- **§6.12** — L'ordre de tri doit être **totalement unique** — sinon des lignes sont sautées
  ou rendues deux fois entre deux pages. D'où le tiebreaker de §6.2, sauf clé déjà unique.

### §6.13 — Application aux deux collections du projet

**Liste des événements (10a).** Triée par `Date` croissante (EV-R2). `Date` est globalement
unique (EV-U3), c'est donc une clé de tri complète : **pas de tiebreaker**. La collection est
bornée à ~30 lignes, elle tient toujours en une page. Concrètement :
`{ "items": [...], "self": "..." }`, `next` et `prev` **jamais présents**, `?cursor` accepté
sans effet, `?limit` honoré. **L'objet de page est en place dès le premier endpoint**, la
mécanique du curseur n'est simplement pas exercée ici. C'est délibéré : la forme de la réponse
ne changera pas le jour où une collection en aura besoin.

**Liste des commandes (~sem. 11).** Triée par `CreatedAt` puis `OrderId` — `CreatedAt` n'est
pas unique, le tiebreaker est nécessaire. Index composite dédié. Curseur, `next` et `prev`
réellement exercés. Si cette liste gagne des filtres, `filters` (§6.2) est renseigné à ce
moment-là.

---

## 7. Spécification OpenAPI

- **§7.1** — Le document OpenAPI est **généré au build** et **versionné** dans
  `docs/openapi.json`. Paquets first-party : `Microsoft.AspNetCore.OpenApi` pour le document,
  `Microsoft.Extensions.ApiDescription.Server` pour l'écrire sur disque à chaque
  `dotnet build`.
- **§7.2** — **Le fichier ne s'édite jamais à la main.** `/check` régénère et compare
  (`git diff --exit-code -- docs/openapi.json`) : un document désynchronisé est un rouge, au
  même titre qu'un test qui échoue. Même discipline que
  `web/src/styles/_theme-colors.scss`.
  **La cible de génération est incrémentale**, et c'est le piège : un `dotnet build` où rien
  n'a bougé dans `src/Api` la court-circuite, et une édition manuelle du document **survit
  alors au build**. Vérifié le 2026-09-11. Deux conséquences : la comparaison de `/check` est
  le seul filet réel, et la régénération forcée passe par
  `dotnet build src/Api/EventsManager.Api.csproj --no-incremental`.
- **§7.3** — Deux usages : **artefact de déploiement** (contrat publiable) et **index
  structurel** pour le `zalando-reviewer` — chemins, verbes, noms de schémas, codes déclarés.
  L'index ne prouve **jamais** un comportement : ce que le serveur renvoie réellement se
  vérifie sur la source.
- **§7.4** — La génération au build **exécute le point d'entrée de l'application** dans un
  hôte factice (doc Microsoft). Tout ce qui, dans le démarrage, exige une ressource externe
  doit être neutralisé sous la garde
  `Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider"`, sans quoi
  `dotnet build` échoue. C'est le cas de la chaîne de connexion et du câblage EF Core.

---

## 8. Règles Zalando écartées

Chaque ligne est une règle du guide **volontairement non retenue**. Le `zalando-reviewer` ne
doit **jamais** remonter une déviation listée ici.

| Règle | Décision | Motif |
|---|---|---|
| 135 — *not use `/api` as base path* | **écartée** | l'API et le front sont servis sous le même hôte ; `/api` sépare sans ambiguïté les deux, la contrepartie que Zalando évite (un sous-domaine dédié) n'existe pas ici |
| 163, 164, 165, 166 — HATEOAS, contrôles hypertexte | **écartées**, sauf les liens de page de §6 | maturité REST 2 assumée (Zalando 162) : un seul client, qui connaît ses routes ; seule la pagination y gagne réellement |
| 183, 184, 233 — `X-Flow-ID`, headers propriétaires Zalando | **écartées** | headers propres à l'écosystème Zalando ; la corrélation est portée par `trace_id` (§4.6) |
| 179, 180 — `Content-Location` | **écartée** | `Location` sur `201` suffit (§3) ; Zalando 180 déconseille lui-même `Content-Location` |
| 152 — `207` pour les requêtes batch | **écartée** | aucune opération batch au périmètre |
| 182 — `ETag` + `If-Match` / `If-None-Match` | **écartée** | back-office mono-utilisateur, pas de concurrence à arbitrer — cohérent avec le retrait assumé de `RowVersion` |
| 230 — `Idempotency-Key` | **écartée** | pas de retry automatique côté client, pas d'opération financière |
| 181 — `Prefer` | **écartée** | aucune préférence de traitement à négocier |
| 153 — `429` et headers de quota | **écartée** | pas de limitation de débit ; à rouvrir si le front public est exposé sans protection en amont |
| 113, 114, 115 — versioning | **sans objet** | pas de versioning (§1.6) ; la position du projet est celle que 113 recommande |
| déprécation par header (`Deprecation`, `Sunset`) | **écartée** | aucun consommateur externe à prévenir |
| 254 — `Prefer: return=total-count` | **écartée** | pas de total du tout (§6.8) |
| 173 — objet monétaire commun | **non tranchée**, voir §9.1 | écart connu de l'existant, à arbitrer au cadrage de la tranche commandes |

---

## 9. Confrontation à l'existant

Constats relevés en confrontant ce document au code en place au **2026-09-10**. Un écart est
un **défaut à noter**, jamais un critère à réaligner sur le code.

### 9.1 Montants exposés en `decimal` nu — écart réel, non corrigé

`OrderDto.Total`, `OrderSummaryDto.Total`, `OrderLineDto.UnitPrice` et `OrderLineDto.Subtotal`
exposent un `decimal` nu. **Zalando 173 est un MUST** : un montant se représente par un objet
`{ "amount": 42.00, "currency": "EUR" }`.

Le domaine porte bien un value object `Money`, mais **sa devise est implicite et absente du
modèle** (`src/Domain/Orders/Money.cs`, commentaire de tête), et `OrderMappings.ToDto`
l'aplatit en `decimal`. L'écart est donc double : dans le DTO **et** dans le domaine.

**Non corrigé par cette unité** : les DTO et le domaine sont hors périmètre, et le sujet n'est
pas dans les décisions du cadrage. À arbitrer au **cadrage de la tranche commandes
(~sem. 11)**, avec deux options réelles — enrichir `Money` d'une devise et propager, ou
écarter 173 avec le motif « mono-devise, euro implicite ». Les deux sont défendables ; aucune
ne se décide en écrivant du code.

### 9.2 Collection renvoyée en liste nue — contrainte pour la 10a

`GetOrdersByEventQueryHandler` rend une liste de `OrderSummaryDto`. Ce n'est pas un défaut du
handler — la couche Application n'a pas à connaître la forme HTTP — mais l'endpoint qui la
consommera devra l'**envelopper dans l'objet de page** de §6, sous peine de violer §2.6
(Zalando 110). Noté ici pour que ce ne soit pas redécouvert au moment d'écrire l'endpoint.

### 9.3 Types de médias négociables plus larges que §5.1 — écart connu, non corrigé

`AddControllers()` installe les formateurs par défaut : `SystemTextJsonOutputFormatter` annonce
`application/json` **et** `text/json`, et `StringOutputFormatter` annonce `text/plain`. Un
client qui demanderait `Accept: text/json` recevrait donc un succès étiqueté `text/json`, ce
que §5.1 ne prévoit pas.

L'écart est **latent** : aucun client ne demande ces types, et rien dans l'API ne les produit
spontanément. Le corriger proprement passe par `[Produces("application/json")]` ou une
convention MVC globale — deux choses qui se posent **avec le premier contrôleur**, que cette
unité s'interdit d'écrire. À traiter en 10a, en même temps que le premier endpoint.

Relevé par le `zalando-reviewer` lors de sa mise à l'épreuve : c'est un point durable de
`Program.cs`, pas un artefact du contrôleur jetable qui servait au test.

### 9.4 Le statut nu dégrade quand le client refuse le JSON — écart mesuré, non corrigé

§4 est écrit en absolu : « Toute réponse `4xx` et `5xx` sort en `application/problem+json` ».
Ce n'est **pas** vrai pour un client dont l'en-tête `Accept` exclut le JSON. Mesuré le
2026-09-11 sous `Accept: text/html` :

| Chemin | Comportement observé |
|---|---|
| exception non gérée (`UseExceptionHandler`) | **tient** : `application/problem+json`. Pincé par un test |
| statut nu du routage (`UseStatusCodePages`) | **dégrade** : repli `text/plain` du handler par défaut, message anglais |

Cause : `DefaultProblemDetailsWriter` n'écrit que si l'`Accept` accepte `application/json`,
`application/problem+json` ou un joker. Un `Accept` absent vaut acceptation — c'est le cas de
l'`HttpClient` d'Angular, donc du seul client réel du projet. L'exposition pratique se réduit à
un humain qui ouvre une URL d'API dans un navigateur.

**Non corrigé** : fermer le trou demande un `IProblemDetailsWriter` maison qui ignore l'`Accept`,
ou un handler explicite passé à `UseStatusCodePages` qui duplique la logique du customizer.
Dans les deux cas c'est de la machinerie nouvelle, pour un cas qu'aucun client du projet ne
produit. À arbitrer ; en attendant, la garantie de §4 se lit « pour tout client qui accepte le
JSON ».

Relevé par le `zalando-reviewer` à sa première invocation sur l'unité, puis mesuré — sa
formulation initiale couvrait les deux chemins, la mesure a montré que seul le second dégrade.

### 9.5 Conforme

`EventDto(Guid Id, string Name, DateOnly Date)` sort en `id`, `name`, `date` : conforme §2 tel
quel, y compris Zalando 235 (`date` porte le mot `date`). Aucun `*Dto.cs` du projet ne porte
de propriété camelCase ou de tableau au niveau racine.

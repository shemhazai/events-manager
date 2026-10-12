# Critères d'acceptation : agrégat Event (semaines 4-5, complété en 10a, puis au cadrage du 2026-10-12)

> Source de vérité du test-writer. La valeur attendue d'un test vient d'ici,
> jamais de l'implémentation.

## Périmètre

- Use cases couverts : création, lecture par id, liste, modification, suppression,
  **lecture de l'événement courant** (cadrage du 2026-10-12)
- Les critères marqués **(10a)** portent sur des règles décidées le 2026-09-02 et
  non encore implémentées. Les critères `EV-001` à `EV-019` portent sur des règles
  décidées le 2026-10-12, également non implémentées — leur périmètre exact est porté
  par l'introduction de la section « Événement courant ». Les autres décrivent le
  comportement attendu de l'existant : ils sont à confronter au code en place, un
  écart est un défaut à signaler, pas un critère à réaligner sur le code
- Deux critères d'une section plus ancienne sont touchés par le cadrage du
  2026-10-12 : **EV-W1 est amendé**, et la formulation d'**EV-W10 est à amender**
  (note en fin de section « Fenêtre de commande »). Tout le reste des sections
  antérieures est inchangé, EV-R2 et EV-W2 à EV-W9 compris
- Hors périmètre du fichier : catalogue de produits par événement (semaine 10bis). Le
  **rendu** de l'événement courant côté front public reste hors périmètre
  (semaine 14) ; la **lecture serveur** qui l'alimente y entre au 2026-10-12
- Hors périmètre **de la tranche du 2026-10-12**, sans que leurs critères changent :
  tout chemin d'écriture, le déplacement des dates de fenêtre (EV-W6 à EV-W10) et la
  liste du back-office (EV-R2)

## Convention d'identifiants

Deux schémas cohabitent dans ce fichier, et c'est une décision, pas un oubli.

- **Schéma à lettre de groupe** (`EV-N1`, `EV-W7`, `EV-P4`…) : les 40 critères
  antérieurs au 2026-10-12. La lettre code le thème, le chiffre l'indice dans le thème
- **Schéma à plat, par ordre d'arrivée** (`EV-001` à `EV-019`) : tout critère décidé à
  partir du cadrage du 2026-10-12. Un nouveau critère prend le numéro libre suivant,
  quel que soit son thème, et **ne change jamais de numéro ensuite** : les sections se
  réorganisent sans invalider un identifiant, et insérer un critère n'en renumérote aucun

Les deux formes sont structurellement distinctes — `EV-<lettre><chiffre>` contre
`EV-<trois chiffres>` — donc **aucune collision n'est possible** : un futur `EV-W11`
n'entre pas en conflit avec `EV-011`. Le zéro-padding sur trois chiffres rend la
recherche exacte (`EV-[0-9]{3}`), ce qui compte pour des sous-agents qui démarrent avec
un contexte vierge et travaillent par recherche d'identifiant.

**Pourquoi les 40 antérieurs ne sont pas renumérotés.** Ils sont cités 168 fois hors de
`docs/acceptance/` : 24 fichiers sous `tests/`, `docs/api-conventions.md` (§3.5 et
§6.13), `CLAUDE.md`, deux définitions de sous-agents — et **4 messages de commit déjà
dans `git log`**. Renuméroter rendrait ces quatre messages définitivement faux, sans
recours : l'historique git est ici la pièce qui prouve que les attendus précèdent le
code. Décision du 2026-10-12 : la frontière entre les deux schémas est assumée, le gain
d'un schéma unique ne paie pas ce prix.

## Convention d'horloge

Le domaine ne lit jamais l'horloge. « Aujourd'hui » est une `DateOnly` fournie par
l'appelant, ce qui rend chaque critère temporel déterministe et testable sans
attendre. **Tous les exemples de ce document prennent `aujourd'hui = 2026-06-15`.**

L'appelant, lui, tire cette `DateOnly` d'un **point de vérité unique** :
`TimeProviderExtensions.TodayLocal()` (`src/Application/TimeProviderExtensions.cs`),
qui convertit l'instant courant dans un **fuseau fixé en dur, `Europe/Brussels`** —
jamais le fuseau de la machine hôte (décision du 2026-10-12). Motif : la fenêtre de
commande devient visible sur une page publique ; sous un hébergement en UTC, un
« aujourd'hui » pris sur la machine ouvrirait et fermerait la fenêtre deux heures en
retard l'été, en silence. La journée métier ne dépend plus de la configuration de
l'hôte.

## Nom

- EV-N1 : nom obligatoire. Null, chaîne vide ou blancs seulement : rejeté
- EV-N2 : trim appliqué avant toute validation et avant stockage. `"  Fête  "` est
  stocké `"Fête"`
- EV-N3 : 100 caractères maximum, mesurés après trim. 100 est accepté, 101 est rejeté

## Date de l'événement

- EV-D1 : une seule date, jamais une plage
- EV-D2 : à la création, la date est au moins à J+1, donc strictement postérieure à
  aujourd'hui. `2026-06-16` est accepté, `2026-06-15` (aujourd'hui) et `2026-06-14`
  sont rejetés
- EV-D3 **(10a)** : la même borne s'applique à la modification. Déplacer un
  événement vers `2026-06-15` ou avant est rejeté

## Unicité

- EV-U1 : deux événements ne peuvent pas porter le même nom dans la même année
  civile. `"Souper"` au `2026-09-12` et `"Souper"` au `2026-11-08` : le second est
  rejeté. `"Souper"` au `2027-03-01` est accepté
- EV-U2 : la comparaison de noms de EV-U1 s'applique après trim
- EV-U3 **(10a)** : deux événements ne peuvent pas partager la même date, quel que
  soit leur nom. `"Souper"` au `2026-09-12` et `"Barbecue"` au `2026-09-12` : le
  second est rejeté
- EV-U4 : ces deux unicités sont garanties jusqu'en base, à prouver sur SQL Server
  et pas seulement en mémoire

## Modification **(10a)**

- EV-M1 : un événement dont la date est **passée** est figé en entier. Aucune
  modification n'est acceptée, ni le nom, ni la date, ni la fenêtre de commande.
  Un événement daté du `2026-06-14` est intouchable, un événement daté du
  `2026-06-15` (aujourd'hui) l'est également puisqu'il n'est plus à venir
- EV-M2 : **l'année de la date est immuable**. Seuls le jour et le mois se
  déplacent. Un événement au `2026-09-12` peut aller au `2026-11-08`, mais toute
  cible en `2027` ou en `2025` est rejetée
- EV-M3 : conséquence attendue de EV-M2, à vérifier explicitement : aucune
  modification ne peut casser l'unicité (nom, année civile) de EV-U1, puisque
  l'année ne bouge jamais
- EV-M4 : le nom modifié repasse par EV-N1 à EV-N3 et par EV-U1
- EV-M5 : la date modifiée repasse par EV-D3 et par EV-U3
- EV-M6 : une modification rejetée ne laisse **aucune trace**. L'événement est
  relu strictement identique à son état d'avant la tentative, tous champs confondus
- EV-M7 : modification d'un id inconnu : not found, rien n'est écrit

## Fenêtre de commande **(10a)**

C'est cette fenêtre, et non la date de l'événement, qui détermine si les commandes
en ligne sont ouvertes.

> **Section touchée par le cadrage du 2026-10-12.** Deux critères y bougent :
> **EV-W1 est amendé** ci-dessous, et la formulation d'**EV-W10 est à amender** — voir
> la note en fin de section. Les règles d'EV-W2 à EV-W5 sont inchangées et
> **s'appliquent** à la lecture de l'événement courant ; celles d'EV-W6 à EV-W9 sont
> inchangées et hors de ce périmètre.

- EV-W1 **(amendé le 2026-10-12)** : l'événement porte une date de début et une date
  de fin de commande, **toutes deux optionnelles**, qui **doivent pouvoir être posées
  dès la création**. Un événement au `2026-09-12` créé aujourd'hui (`2026-06-15`) avec
  la fenêtre `2026-06-15` → `2026-06-20` est accepté **en une seule opération**.
  Formulation remplacée : « toutes deux optionnelles **et absentes à la création** ».
  Motif de l'amendement : le `PUT` qui les posait après coup n'existe pas dans la
  tranche de lecture de l'événement courant, qui a besoin d'une fenêtre déjà posée.
  Les dates restent soumises à EV-W2 (les deux ensemble ou aucune) et à EV-W3
- EV-W2 : les deux dates sont soit toutes deux présentes, soit toutes deux
  absentes. Renseigner une seule des deux est rejeté
- EV-W3 : invariant d'ordre : début <= fin <= date de l'événement. Pour un
  événement au `2026-09-12`, la fenêtre `2026-08-01` → `2026-09-12` est valide,
  `2026-08-01` → `2026-09-13` est rejetée, `2026-09-01` → `2026-08-01` est rejetée
- EV-W4 : début et fin peuvent être égaux : une fenêtre d'un seul jour est valide
- EV-W5 : absence de fenêtre signifie commandes fermées. Ce n'est pas un défaut de
  configuration, c'est l'état normal d'un événement dont l'ouverture n'est pas
  encore décidée

### Régime de la date de début

- EV-W6 : tant que la date de début est **strictement future**, elle se déplace
  librement, en avant comme en arrière, avec **aujourd'hui pour plancher**. Un
  début au `2026-07-01` peut aller au `2026-06-20`, au `2026-08-01`, ou au
  `2026-06-15`. Le déplacer au `2026-06-14` est rejeté
- EV-W7 : **dès que la date de début est atteinte, elle est figée**. Un début au
  `2026-06-15` ou antérieur ne peut plus bouger, ni en avant ni en arrière
- EV-W8 : une fenêtre dont le début est atteint **ne peut plus être supprimée**.
  Sans cette règle, le gel de EV-W7 se contournerait en effaçant la fenêtre
  entière puis en la recréant

### Régime de la date de fin

- EV-W9 : la date de fin reste librement déplaçable tant que l'événement n'est pas
  passé, dans l'invariant EV-W3
- EV-W10 : ramener la fin à aujourd'hui ferme les commandes immédiatement. C'est un
  usage légitime, jamais un cas d'erreur

> **EV-W10 est à amender, et l'amendement est hors de cette tranche.** Sa formulation
> contredit la borne de fin **inclusive** posée en EV-002 : avec
> `début <= aujourd'hui <= fin`, ramener la fin au `2026-06-15` (aujourd'hui) laisse
> les commandes **ouvertes aujourd'hui** et ne les ferme qu'à partir du `2026-06-16`.
> Décision du 2026-10-12 : la contradiction est levée **par amendement, pas par
> lecture** — la règle d'EV-W10 n'est donc pas réécrite ici, et son amendement est à
> faire au **cadrage de la tranche de déplacement des dates** (EV-W6 à EV-W10). D'ici
> là, EV-W10 et EV-002 sont en contradiction connue, et c'est **EV-002 qui dit le
> comportement attendu** de la lecture.

## Suppression **(10a)**

- EV-S1 : un événement est supprimable **uniquement si sa période de commande n'a
  jamais commencé** et si l'événement n'est pas passé. Deux cas supprimables :
  aucune fenêtre, ou une fenêtre dont le début est strictement futur
- EV-S2 : une fenêtre dont le début est atteint rend la suppression définitivement
  impossible, y compris après la fermeture de la fenêtre et après la fin des
  commandes. La règle est **monotone** : une fenêtre ouverte ne redevient jamais
  non ouverte
- EV-S3 : un événement passé n'est jamais supprimable, même sans aucune fenêtre
- EV-S4 : conséquence attendue de EV-S1 et EV-S2, à vérifier explicitement :
  **aucun événement portant des commandes ne peut être atteint par une
  suppression**. La FK `Order → Event` en Restrict ne doit jamais avoir à
  s'exprimer, aucun test ne doit pouvoir provoquer son erreur brute
- EV-S5 : suppression d'un id inconnu : not found
- EV-S6 : une suppression refusée ne laisse aucune trace, l'événement est relu intact

## Lecture

- EV-R1 : lecture par id : l'événement est retourné avec son nom, sa date et sa
  fenêtre de commande si elle existe. Id inconnu : not found
- EV-R2 **(10a)** : liste des événements, ordonnée par date d'événement croissante.
  Aucun événement : liste vide, jamais not found
- EV-R3 : les contrats de lecture n'exposent que des primitifs et des types BCL,
  jamais un type du Domain

## Événement courant

Périmètre de cette section, décidé le 2026-10-12 : **une seule opération**, la lecture
`GET /api/events/current`, **forme singleton**. Elle sert le **front public**, qui
affiche le formulaire de commande de l'événement dont la fenêtre est ouverte, et une
page « aucun événement à venir » sinon (décision du 2026-09-07).

- **Aucune opération d'écriture n'est au périmètre** : l'événement est inséré en base
  par un script SQL versionné
- **Tous les critères `EV-001` à `EV-019` portent sur des règles décidées et non encore
  implémentées** ; aucun tag de tranche par critère, le numéro de semaine de ce
  cadrage n'étant pas fixé. Constat du cadrage, à traiter comme un défaut à combler et
  non comme un attendu à réaligner : **la fenêtre de commande n'existe nulle part
  aujourd'hui** — ni dans le Domain, ni dans le modèle EF, ni dans une migration ; et
  `docs/openapi.json` ne déclare aucun chemin, donc `GET /api/events/current` serait
  le **premier endpoint HTTP du projet** (ni EV-R1 ni EV-R2 n'existent sur le fil)
- EV-W2 à EV-W5 **s'appliquent**, au sens de « sont à implémenter », et ne sont pas
  réécrits ici. EV-W6 à EV-W10 (déplacement des dates) sont hors périmètre
- **EV-R2 est inchangé et hors périmètre** : la liste ordonnée sert l'écran de gestion
  du back-office, `current` sert le front public. Son statut **(10a)**, son ordre et
  §6.13 de `docs/api-conventions.md` restent valables tels quels
- EV-R3 et les conventions de `docs/api-conventions.md` (§2 sérialisation, §3 codes de
  statut, §4 Problem JSON, §5 types de médias) s'appliquent sans redite

### Ce qui fait l'événement courant

- EV-001 : un événement est **courant** quand sa fenêtre de commande (EV-W1) est
  **ouverte aujourd'hui**. La date de l'événement ne décide rien, seule la fenêtre
  décide. Un événement **sans fenêtre** n'est jamais courant (EV-W5), même daté du
  `2026-09-12`
- EV-002 : la fenêtre est ouverte **aux deux bornes incluses** :
  `début <= aujourd'hui <= fin`. Le jour même du début compte, le jour même de la fin
  compte. Avec `aujourd'hui = 2026-06-15` : `2026-06-15` → `2026-06-20` est ouverte,
  `2026-06-10` → `2026-06-15` est ouverte, `2026-06-16` → `2026-06-20` n'est pas
  ouverte, `2026-06-01` → `2026-06-14` n'est pas ouverte
- EV-003 : conséquence attendue d'EV-002 et d'EV-W4, à vérifier explicitement : une
  fenêtre d'un seul jour est ouverte **ce jour-là et lui seul**. Fenêtre
  `2026-06-15` → `2026-06-15` : l'événement est courant. Fenêtres
  `2026-06-14` → `2026-06-14` et `2026-06-16` → `2026-06-16` : il ne l'est pas. Une
  fin exclusive rendrait le premier cas vide et contredirait EV-W4

### Invariant de non-chevauchement

- EV-004 : **les fenêtres de commande de deux événements quelconques sont
  disjointes** — aucun jour du calendrier n'est couvert par deux fenêtres. Interdit :
  `"Barbecue"` au `2026-07-04` avec fenêtre `2026-06-01` → `2026-06-20` **et**
  `"Souper"` au `2026-09-12` avec fenêtre `2026-06-15` → `2026-08-31`, les deux
  couvrant du `2026-06-15` au `2026-06-20`. Admis : la même paire avec la fenêtre du
  `"Souper"` en `2026-06-21` → `2026-08-31`. Fondement métier : deux fenêtres ne sont
  jamais ouvertes le même jour, on attend toujours la fin d'un événement avant d'en
  poser un nouveau
- EV-005 : **portée de l'invariant : toutes les paires d'événements, sans
  restriction.** Aucune clause ne limite EV-004 aux événements à venir. Deux
  événements **passés** qui se chevauchent sont également interdits : `2026-03-07`
  avec fenêtre `2026-02-01` → `2026-03-07` et `2026-04-11` avec fenêtre
  `2026-03-01` → `2026-04-11` partagent du `2026-03-01` au `2026-03-07`, la paire est
  refusée
- EV-006 : EV-004 comble un trou réel, et c'est ce qui le justifie. EV-U3 rend la
  **date d'événement** unique, donc chaque jour porte au plus un événement ; EV-W3
  ordonne `début <= fin <= date de l'événement` pour un événement **donné**. Mais rien
  ne contraignait la position d'une fenêtre par rapport à celle d'un **autre**
  événement : une fenêtre pouvant courir sur des semaines, celle d'un événement
  lointain pouvait s'ouvrir avant la fermeture de celle d'un événement proche
- EV-007 : conséquence attendue d'EV-004, à vérifier explicitement : **il y a au plus
  une candidate** à tout instant. Le singleton est donc bien défini **par
  construction**, et la lecture **n'emploie aucun ordre de départage**. L'option d'un
  ordre de résolution déterministe par date d'événement croissante a été proposée au
  cadrage du 2026-10-12 et **écartée** au profit de l'invariant
- EV-008 : **statut d'EV-004 et EV-005.** Aucun chemin d'écriture n'est au périmètre de
  cette tranche : ces deux critères sont **écrits comme invariants et garantis nulle
  part ici**. Leur application — validation à l'écriture, contrainte en base — est
  **différée à la tranche qui introduira l'écriture des dates de fenêtre**.
  Contrepartie signalée et assumée au cadrage du 2026-10-12. Conséquence immédiate :
  la lecture ne **suppose** pas l'invariant tenu, EV-017 dit ce qu'elle fait quand il
  est violé

### Résultat : exactement un événement courant

- EV-009 : exactement une candidate → **`200`**, corps = un objet JSON décrivant cet
  événement. Forme **singleton** : le corps est l'événement lui-même, **jamais l'objet
  de page de §6** (qui ne concerne que les collections), jamais un tableau (§2.6)
- EV-010 : contenu exposé, **exactement quatre champs** : `id`, `name`, `date`,
  `order_window_end_date`. Pour `"Souper"` au `2026-09-12` avec fenêtre
  `2026-06-01` → `2026-06-20`, lu le `2026-06-15` :

      {
        "id": "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
        "name": "Souper",
        "date": "2026-09-12",
        "order_window_end_date": "2026-06-20"
      }

  La valeur de l'`id` est illustrative ; les trois autres champs sont l'oracle.
  Propriétés en snake_case, dates en ISO 8601 date (§2.1, §2.5)
- EV-011 : le **début de fenêtre n'est pas exposé**. Par construction (EV-001, EV-002),
  un événement rendu a sa fenêtre ouverte : le début n'a plus aucune utilité pour le
  front. Un corps qui porterait un `order_window_start_date` est un défaut, pas un
  champ en trop sans conséquence
- EV-012 : l'`id` **est** exposé : il est requis pour poster une commande (OR-C1 de
  `docs/acceptance/order.md` — une commande référence un événement existant). Sans
  lui, le front public ne peut rien faire de la réponse
- EV-013 : le contrat de cette lecture est **dédié au front public** et distinct de
  celui du back-office (EV-R1, EV-R2) ; aucune réutilisation du DTO de lecture par id.
  Conséquence attendue, à vérifier explicitement : **ajouter un champ au contrat du
  back-office ne change pas le corps de `GET /api/events/current`**. Le jour où ce
  contrat gagne un catalogue de produits ou des statistiques, le front public ne les
  reçoit pas

### Résultat : aucun événement courant

- EV-014 : aucune candidate → **`404`** + Problem JSON, `type` =
  **`/problems/not-found`**, `title` = « Ressource introuvable » (§4.7 de
  `docs/api-conventions.md`). **Aucun slug fin n'est créé** : le front sait déjà quelle
  route il a appelée, un slug dédié n'apporterait aucune information nouvelle, et §4.2
  refuse deux discriminants qui dérivent l'un de l'autre
- EV-015 : quatre situations rendent le même `404`, chacune à vérifier — base sans
  aucun événement ; un seul événement, sans fenêtre (EV-W5) ; un seul événement dont la
  fenêtre n'est **pas encore** ouverte (`2026-06-16` → `2026-06-20`) ; un seul
  événement dont la fenêtre est **déjà fermée** (`2026-06-01` → `2026-06-14`).
  Conséquence d'EV-014, à vérifier explicitement : **jamais un `200` au corps vide ou
  nul, jamais un `204`**
- EV-016 : ce `404` est un **état normal et attendu**, pas une anomalie : il pilote la
  page « aucun événement à venir ». Côté serveur il **ne se journalise pas comme une
  erreur** ; côté front il **ne déclenche ni toast ni message d'erreur**. C'est
  précisément ce qui le distingue du `404` d'un identifiant inconnu (EV-R1), lequel
  signale une demande erronée

### Résultat : invariant violé

- EV-017 : deux candidates ou plus — EV-004 violé malgré le processus, par exemple une
  faute de frappe dans le script SQL versionné — → **`500`** + Problem JSON, `type` =
  **`/problems/internal-error`**, `title` = « Erreur interne » (§4.7). Ce `500` est
  **posé délibérément par le chemin de lecture** : il ne vient pas d'une exception
  subie
- EV-018 : conséquence attendue d'EV-017, à vérifier explicitement : la lecture
  **échoue et ne choisit jamais silencieusement** une candidate. Avec `"Barbecue"` au
  `2026-07-04` (fenêtre `2026-06-01` → `2026-06-20`) et `"Souper"` au `2026-09-12`
  (fenêtre `2026-06-10` → `2026-08-31`), tous deux ouverts le `2026-06-15` : **aucun
  corps d'événement ne sort**, ni le premier par date d'événement, ni le dernier, ni le
  plus récemment inséré
- EV-019 : le corps de ce `500` respecte §4.5 et §4.6 comme tout autre : **aucune stack
  trace, aucun message d'exception brut**, et un `trace_id` de corrélation. Le
  diagnostic de l'incohérence vit dans les logs serveur, pas dans la réponse

### Nomenclature retenue au 2026-10-12

Noms arrêtés au cadrage, le choix ayant été laissé ouvert :

- contrat de sortie : **`CurrentEventDto`** (famille d'`EventDto`, `OrderSummaryDto`) ;
- lecture : **`GetCurrentEventQuery`** / **`GetCurrentEventQueryHandler`** (famille de
  `GetEventQuery`) ;
- propriété JSON de la date de fin : **`order_window_end_date`** — §2.5 donne
  littéralement `order_window_start_date` en exemple, la fin suit la même forme et
  porte bien le mot `date` (Zalando 235).

Seule la propriété JSON est un oracle de test (EV-010) ; les noms .NET sont une
convention de lisibilité, pas un comportement observable.

### Dette ouverte hors de ce fichier

§3 de `docs/api-conventions.md` dit aujourd'hui « `500 Internal Server Error` |
exception non gérée. **Jamais volontaire** ». Cette ligne est à amender pour autoriser
le `500` délibéré d'EV-017 sur violation d'invariant détectée côté serveur. Consignée
ici pour mémoire : elle s'écrit dans `docs/api-conventions.md`, pas dans ce fichier.

### Questions restées ouvertes au 2026-10-12

- **Mécanisme interne de remontée des deux échecs distincts** (EV-014 et EV-017).
  Constat du cadrage : aucun type `Result` n'existe dans le projet — ni `Result<T>`, ni
  `OneOf`, ni `ErrorOr` ; la convention en place est le nullable (`Task<EventDto?>`,
  `Task<OrderDto?>`, `Task<IReadOnlyList<OrderSummaryDto>?>`), où `null` veut dire « pas
  trouvé » et n'a pas d'autre sens possible. **Un nullable ne peut pas porter deux
  échecs distincts** : « aucun événement courant » et « invariant violé » y rendraient
  tous deux `null`. La demande est de rester dans le result pattern ; le choix du
  mécanisme (introduire un `Result<T>`, lever une exception, autre) est une **décision
  due au moment du plan d'implémentation**, délibérément non tranchée au cadrage. Les
  critères `EV-001` à `EV-019` ne portent que sur les **valeurs observables en
  sortie**, jamais sur le mécanisme
- **Numéro de semaine ou de tranche de ce cadrage** : non fixé. D'où l'absence de tag
  par critère dans cette section
- **`GET /api/events/<segment non-guid>`** : aucun code de statut défini. Relève de
  `docs/api-conventions.md`, pas des critères d'acceptation. Devient un vrai sujet le
  jour où `/{id}` coexiste avec `/current`

## Persistance

- EV-P1 : conventions existantes appliquées, colonnes d'audit DB-only comprises
- EV-P2 : round-trip create → get → list → update → delete via les vrais handlers
  sur SQL Server (Testcontainers)
- EV-P3 : un événement invalide ne persiste rien
- EV-P4 **(10a)** : les deux nouvelles dates sont persistées et relues à
  l'identique, y compris absentes des deux côtés

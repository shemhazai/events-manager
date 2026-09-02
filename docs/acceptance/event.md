# Critères d'acceptation : agrégat Event (semaines 4-5, complété en 10a)

> Source de vérité du test-writer. La valeur attendue d'un test vient d'ici,
> jamais de l'implémentation.

## Périmètre

- Use cases couverts : création, lecture par id, liste, modification, suppression
- Les critères marqués **(10a)** portent sur des règles décidées le 2026-09-02 et
  non encore implémentées. Les autres décrivent le comportement attendu de
  l'existant : ils sont à confronter au code en place, un écart est un défaut à
  signaler, pas un critère à réaligner sur le code
- Hors périmètre : catalogue de produits par événement (semaine 10bis),
  comportement de la fenêtre de commande côté front public (semaine 14)

## Convention d'horloge

Le domaine ne lit jamais l'horloge. « Aujourd'hui » est une `DateOnly` fournie par
l'appelant, ce qui rend chaque critère temporel déterministe et testable sans
attendre. **Tous les exemples de ce document prennent `aujourd'hui = 2026-06-15`.**

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

- EV-W1 : l'événement porte une date de début et une date de fin de commande,
  **toutes deux optionnelles et absentes à la création**
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

## Persistance

- EV-P1 : conventions existantes appliquées, colonnes d'audit DB-only comprises
- EV-P2 : round-trip create → get → list → update → delete via les vrais handlers
  sur SQL Server (Testcontainers)
- EV-P3 : un événement invalide ne persiste rien
- EV-P4 **(10a)** : les deux nouvelles dates sont persistées et relues à
  l'identique, y compris absentes des deux côtés

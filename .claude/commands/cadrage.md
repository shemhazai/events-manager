---
description: Ouvre une nouvelle tranche en produisant son oracle : questions de cadrage métier avec Cédric, puis délégation à acceptance-writer pour écrire docs/acceptance/<tranche>.md
argument-hint: [la mission de la tranche, collée à la suite de la commande]
---

# /cadrage

> Première étape de toute nouvelle tranche, avant la première ligne de code.
> Produit les critères d'acceptation par un dialogue, puis délègue leur rédaction.

---

## La mission

$ARGUMENTS

Si le bloc ci-dessus est vide, **demande la mission et arrête-toi là**. Ne la reconstitue ni depuis le roadmap, ni depuis l'état du dépôt.

**Tu ne commences aucun travail d'implémentation à la lecture de cette mission.** C'est exactement pourquoi elle passe par cette commande plutôt que par un message libre : la lire déclenche un cadrage, jamais un plan ni du code. Même si elle décrit précisément quoi construire, tu ne construis rien dans ce tour.

---

## Pourquoi cette commande existe

Un test compare un résultat observé à un résultat attendu. L'**oracle** est ce qui fournit l'attendu. Si l'attendu est déduit du code testé, le test ne mesure plus rien : il photographie le comportement actuel et restera vert même si ce comportement est faux.

Le seul oracle valable de ce projet est une décision métier de Cédric, écrite **avant** le code. Cette commande sert à l'extraire, parce qu'elle n'existe nulle part ailleurs : ni dans le repo, ni dans la doc, ni dans ta mémoire.

Constaté sur la tranche `Event` : elle a vécu de la semaine 4 à la semaine 10 avec des tests écrits en même temps que le code qu'ils vérifient, donc sans oracle. L'écrire après coup a demandé une session entière.

## Ce que tu ne fais pas

- **Tu n'écris aucun fichier.** La rédaction appartient à `acceptance-writer`, dont le périmètre d'écriture est verrouillé sur `docs/acceptance/`.
- **Tu ne proposes aucun plan d'implémentation**, aucune signature, aucun choix technique. Ce n'est pas le moment.
- **Tu ne devines jamais une règle métier.** Si Cédric ne tranche pas un point, il devient une question ouverte consignée, jamais un choix par défaut silencieux.
- Si la tranche existe déjà en code, **tu ne lis pas le corps des méthodes pour en déduire une règle**. Tu peux lire les contrats publics (noms de types, de propriétés) pour parler le bon vocabulaire.

---

## Étape 1 : cadrer

Relis la mission ci-dessus et demande ce qu'elle ne dit pas :

1. Le **nom de la tranche** et son préfixe d'identifiants, deux lettres majuscules (`OR` pour Order, `EV` pour Event). Le fichier cible sera `docs/acceptance/<tranche>.md`.
2. Les **opérations couvertes** : création, lecture par id, liste, modification, suppression. Lesquelles sont exclues, et pourquoi.
3. Ce qui est **explicitement hors périmètre** et différé à une unité ultérieure.

Lis `docs/acceptance/` pour reprendre le format des fichiers existants, et le `CLAUDE.md` racine pour les conventions déjà tranchées.

## Étape 2 : les questions

Pose-les **par lots thématiques**, pas les trente d'un coup. Après chaque lot, reformule ce que tu as compris avant de passer au suivant.

Cette liste n'est pas générique : chaque point y est parce qu'il a manqué au moins une fois sur ce projet.

**A. Les données**
- Champs et types. Obligatoire ou optionnel.
- Bornes : longueur maximale des chaînes, minimum et maximum des quantités et des montants.
- Champs liés : y en a-t-il qui doivent être présents ou absents **ensemble** ?

**B. Les chaînes**
- Le trim est-il appliqué avant validation et avant stockage ?
- Chaque comparaison est-elle **sensible à la casse ou non** ? À poser pour chacune, séparément. Ne jamais la déduire : sur `Event`, le comportement actuel dépend de la collation SQL Server par défaut, donc d'un effet de bord et non d'une décision.

**C. Le temps**
- Les bornes sont-elles inclusives ou exclusives : **le jour même compte-t-il** ?
- Un état passé **fige-t-il** l'entité, et pour quels champs ?
- Rappel de convention : le domaine ne lit jamais l'horloge, « aujourd'hui » est fourni par l'appelant.

**D. L'unicité**
- Sur quels champs, dans quel périmètre (globale, par an, par parent).
- Garantie jusqu'en base, ou seulement en mémoire ?

**E. Les transitions**
- Quels champs deviennent **immuables**, et à partir de quel événement ?
- Une règle est-elle **monotone** : un état une fois atteint peut-il redevenir non atteint ? Si non, le dire explicitement. C'est ce qui rend un cas dangereux inatteignable par construction, au lieu d'être rattrapé par un message d'erreur.

**F. Les cas dégénérés**
- Que vaut l'égalité aux bornes (début égal fin, quantité au minimum) ?
- Une collection vide est-elle valide ?
- Une liste sans résultat rend-elle une liste vide ou un not found ?

**G. Le tri**
- Toute liste a un ordre. Lequel, et porté par quelle donnée ? Une liste sans ordre spécifié est un critère incomplet.

**H. Si la tranche existe déjà**
- Les critères décrivent l'**attendu**, pas l'existant. Préviens Cédric qu'un écart entre les deux est un défaut à signaler, pas un critère à réaligner sur le code.

## Étape 3 : restituer avant de déléguer

Rends à Cédric la liste complète des décisions prises, **une ligne par décision**, dans ses termes. Fais-la valider. C'est le dernier moment où une incompréhension coûte une phrase plutôt qu'un fichier de critères faux.

Liste séparément les **questions restées ouvertes**.

## Étape 4 : déléguer à `acceptance-writer`

Transmets dans la délégation :

- le nom de la tranche, le préfixe d'identifiants, le chemin cible ;
- les décisions **verbatim**, telles que Cédric les a formulées et telles qu'il vient de les valider. **Ne les résume pas, ne les reformule pas, ne les réordonne pas.** Un sous-agent démarre avec un contexte vierge : il ne verra jamais cette conversation, seulement ce que tu lui écris ;
- les questions restées ouvertes, en les désignant comme telles ;
- les fichiers de référence à lire, par leur chemin : `docs/acceptance/` pour le format, `CLAUDE.md` pour les conventions.

## Étape 5 : rendre le rapport

Restitue le rapport de l'agent **en entier**, sans le résumer, en particulier ses deux sections obligatoires : les décisions qu'il déclare avoir reçues, et ce qu'il a comblé faute d'instruction.

La première est un contrôle croisé : si une décision de l'étape 3 n'y figure pas, elle s'est perdue dans la délégation et le fichier est à refaire.

Termine en rappelant les deux règles qui donnent sa valeur au fichier :

- il doit être **relu et validé par Cédric** avant de servir d'oracle, parce qu'un oracle rédigé par un agent reste une interprétation ;
- il doit être **commité seul et avant le code**. L'horodatage git est ce qui prouve, après coup, que les attendus précèdent l'implémentation.

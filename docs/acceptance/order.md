# Critères d'acceptation : tranche Order (semaine 7, identité revue le 2026-09-07)

> Source de vérité du test-writer. La valeur attendue d'un test vient d'ici,
> jamais de l'implémentation.

## Périmètre

- Use cases : création d'une commande, lecture par id, liste des commandes d'un événement
- Hors périmètre (différé) : modification, annulation, catalogue de viandes
  (semaine 10bis), numéro de commande lisible, référence de paiement
- Les critères marqués **(10bis/11)** portent sur des décisions du 2026-09-07 pas
  encore implémentées ; les autres décrivent la tranche livrée en semaine 7
- Référence de paiement : ce sera une communication structurée belge (OGM/VCS,
  12 chiffres, contrôle mod 97), attribuée à la validation, unique par événement,
  traitée en semaine 14. ISO 11649 (RF) est écartée (décision du 2026-09-07)
- Les lignes sont des snapshots : libellé et prix figés à la création. La tranche
  livrée les saisit librement ; le catalogue de viandes (semaine 10bis) les
  pré-remplira sans changer leur nature de snapshot
- Une gratuité ne s'encode pas dans une commande : tout montant est strictement positif

## Money (Value Object)

- OR-M1 : montant en euros. Devise implicite EUR, non représentée dans le modèle
- OR-M2 : deux décimales maximum. 4.50 est valide, 4.505 est rejeté
- OR-M3 : montant strictement positif. Zéro et négatif sont rejetés
- OR-M4 : l'addition de deux Money et la multiplication par un entier sont exactes
  (aucune division dans le modèle, donc aucune règle d'arrondi)

## Ligne de commande

- OR-L1 : libellé obligatoire, trim appliqué, non vide, 100 caractères maximum
- OR-L2 : prix unitaire : Money (donc strictement positif, deux décimales max)
- OR-L3 : quantité entière, minimum 1, maximum 20
- OR-L4 : sous-total de ligne = prix unitaire × quantité

## Commande (racine d'agrégat)

- OR-C1 : une commande référence un événement existant. Création vers un événement
  inconnu : rejetée, rien n'est persisté
- OR-C2 **(10bis/11)** : identité de la commande. Nom **et** prénom de l'enfant,
  deux champs distincts, chacun obligatoire, trim appliqué, non vide, 100
  caractères maximum chacun. Sémantique scolaire : sert à distribuer les commandes
  en classe. Remplace le « nom du client » unique de la semaine 7 (décision du
  2026-09-07). Modifie l'entité, le DTO, le validator, les tests OR-* et la
  migration ; à faire avant l'exposition HTTP de `Order`
- OR-C3 : au moins 1 ligne. Pas de plafond numérique : la borne naturelle viendra
  du catalogue de viandes (semaine 10bis), les doublons interdits (OR-C4)
  limitant déjà les lignes à des libellés distincts
- OR-C4 : deux lignes de même libellé (après trim, insensible à la casse) sont
  interdites dans une même commande
- OR-C5 : total = somme des sous-totaux des lignes. Le total n'est ni saisi ni
  stocké, il est calculé. Exemple : 2 × 4.00 + 3 × 1.50 = 12.50
- OR-C6 : la commande est créée complète et atomique : la racine et toutes ses
  lignes, ou rien
- OR-C7 : identifiant OrderId typé, généré à la création (même famille qu'EventId)
- OR-C8 : aucune contrainte entre le moment de la saisie et la date de
  l'événement (avant, jour même, après : tout est accepté)

## Lecture

- OR-R1 : lecture par id : la commande est retournée avec ses lignes et son total.
  Id inconnu : not found (même comportement que GetEvent)
- OR-R2 : liste des commandes d'un événement : retourne les commandes de cet
  événement uniquement (à prouver avec plusieurs événements persistés simultanément),
  en résumé (id, identité selon OR-C2, total), ordonnées par ordre de création
- OR-R3 : événement existant sans commandes : liste vide. Événement inconnu : not found

## Persistance

- OR-P1 : conventions existantes appliquées : colonnes d'audit DB-only, FK physique
  vers app.Events
- OR-P2 : round-trip create → get → list via les vrais handlers sur PostgreSQL
  (Testcontainers)
- OR-P3 : une commande invalide ne persiste rien, ni racine ni lignes
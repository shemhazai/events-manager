# Critères d'acceptation : tranche Order (semaine 7)

> Source de vérité du test-writer. La valeur attendue d'un test vient d'ici,
> jamais de l'implémentation.

## Périmètre

- Use cases : création d'une commande, lecture par id, liste des commandes d'un événement
- Hors périmètre (différé) : modification, annulation, catalogue de produits par
  événement, numéro de commande lisible, référence de paiement ISO 11649
- Les lignes sont des snapshots : libellé et prix saisis librement, figés à la
  création. Aucune référence à un catalogue
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
- OR-C2 : nom du client obligatoire, trim appliqué, non vide, 100 caractères maximum
- OR-C3 : au moins 1 ligne. Pas de plafond numérique : la borne naturelle viendra
  du catalogue de produits par événement (tranche future), les doublons interdits
  (OR-C4) limitant déjà les lignes à des libellés distincts
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
  en résumé (id, nom du client, total), ordonnées par ordre de création
- OR-R3 : événement existant sans commandes : liste vide. Événement inconnu : not found

## Persistance

- OR-P1 : conventions existantes appliquées : colonnes d'audit DB-only, FK physique
  vers app.Events
- OR-P2 : round-trip create → get → list via les vrais handlers sur SQL Server
  (Testcontainers)
- OR-P3 : une commande invalide ne persiste rien, ni racine ni lignes
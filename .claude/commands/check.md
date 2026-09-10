---
description: Boucle de vérification par zones (dotnet, web) — build, tests, format jusqu'au vert (3 passes max, garde-fous anti-triche)
allowed-tools: Bash(dotnet build *), Bash(dotnet test *), Bash(dotnet format *), Bash(pnpm -C web install *), Bash(pnpm -C web run build *), Bash(pnpm -C web test *), Bash(git status *), Bash(git diff *), Read, Grep, Edit(/src/**), Edit(/tests/**), Edit(/web/**)
---

# /check

> Boucle de vérification locale par zones : détecte ce qui a changé (dotnet, web ou les deux), lance les boucles correspondantes jusqu'au vert. 3 passes maximum, aucun raccourci autorisé.

---

## Détection des zones

1. Fichiers modifiés non commités (staged + unstaged + untracked) : `git status --porcelain`.
   (`git status --porcelain` plutôt que `git diff` seul : le diff ignore les fichiers untracked.)
2. Liste vide (arbre propre, ex. /check juste après un commit) : reprendre les fichiers du
   dernier commit — `git diff --name-only HEAD~1..HEAD`.
3. Classement, dans cet ordre (la première règle qui matche gagne) :
   - neutres (ne déclenchent rien à eux seuls) : `**/*.md`, `docs/**`, `.claude/**`, `.mcp.json`, `.gitignore` ;
   - `web/**` → zone **web** ;
   - `src/**`, `tests/**`, `events-manager.slnx`, `global.json`, `Directory.Build.props`,
     `Directory.Packages.props`, `.editorconfig` → zone **dotnet** ;
   - tout autre fichier → **doute**.
4. Décision :
   - uniquement des neutres → annoncer « aucune boucle requise » et s'arrêter là ;
   - zone web seule → boucle web ; zone dotnet seule → boucle dotnet ;
   - les deux zones, ou au moins un fichier en doute → **les deux boucles**.

En cas d'hésitation sur un classement : c'est un doute. Trop vérifier n'est jamais une faute.

---

## Mission

Quand je lance `/check`, détermine d'abord les zones à vérifier (section ci-dessus), puis exécute jusqu'à 3 passes des boucles correspondantes. À chaque échec, corrige la cause réelle (jamais le symptôme), puis recommence une passe complète. Arrête-toi dès que tout est vert, ou après la 3e passe si ça ne l'est toujours pas.

### Une passe = un cycle complet des zones actives

Une passe = toutes les étapes de toutes les zones actives, dans l'ordre : boucle dotnet complète, puis boucle web complète. Une passe est **verte** seulement si toutes ses étapes le sont, dans la même passe. Si une étape échoue, corrige, puis recommence la passe complète **depuis la première étape de la première zone active** (une correction peut avoir un effet de bord ailleurs, ne fais confiance à rien qui n'a pas été re-vérifié en entier).

**Maximum 3 passes.** Si la 3e passe n'est toujours pas verte, applique le critère d'arrêt ci-dessous. Ne tente jamais de 4e passe.

### Boucle dotnet

1. **Build** : `dotnet build events-manager.slnx`
   `TreatWarningsAsErrors=true` + `EnforceCodeStyleInBuild=true` + `SonarAnalyzer.CSharp` sont définis globalement dans `Directory.Build.props` : toute violation de compilation, de style ou d'analyse Sonar remonte donc déjà comme `error` ici. Si ça casse, identifie la cause exacte dans la sortie et corrige le code source concerné. Ne passe pas à l'étape suivante tant que ce n'est pas vert.

   Si l'échec n'est pas une erreur de compilation/style/Sonar mais un problème d'environnement (SDK introuvable, résolution de packages, etc.), ne tente pas 3 passes de correction de code : documente-le immédiatement via le critère d'arrêt, une seule tentative de diagnostic suffit.

2. **Tests** : `dotnet test --solution events-manager.slnx`
   Le runner MTP (contrairement à `dotnet build`/`dotnet format`) n'accepte pas de chemin positionnel : `--solution` est obligatoire. Runner Microsoft Testing Platform (xUnit v3), configuré via `global.json` (`test.runner`). Projets `tests/UnitTests/EventsManager.UnitTests.csproj` et `tests/IntegrationTests/EventsManager.IntegrationTests.csproj` — ce dernier utilise Testcontainers : **Docker doit être actif**, sinon c'est un problème d'environnement (critère d'arrêt immédiat, pas 3 passes). Si un test échoue, corrige la cause réelle : dans l'immense majorité des cas c'est un bug dans le code de production, pas dans le test.

3. **Format** : `dotnet format events-manager.slnx --verify-no-changes`
   Si des changements sont signalés, applique `dotnet format events-manager.slnx` puis relis le diff. À ce stade le build est déjà vert, donc `EnforceCodeStyleInBuild` a déjà éliminé l'essentiel des soucis de style : ce qui reste ici est en principe de la pure mise en forme (indentation, espaces, retours à la ligne). Si le diff touche autre chose qu'un layout (réordonnancement de `using`, changement d'expression), vérifie que le comportement est inchangé avant de continuer.

### Boucle web

Les commandes ciblent `web/` via le drapeau `-C` de pnpm, et se lancent donc **depuis la racine
du repo** — pas de `cd` préalable. `-C` est un drapeau global : il se place avant le nom du
script, jamais après.

0. **Pré-requis** : si `web/node_modules` est absent, `pnpm -C web install --frozen-lockfile`
   (l'équivalent pnpm de `npm ci` : échoue si `pnpm-lock.yaml` ne colle pas au `package.json`).
   Un échec ici est un problème d'environnement : critère d'arrêt immédiat, pas 3 passes.
1. **Build** : `pnpm -C web run build` (configuration production par défaut). Vert = exit 0 **et
   aucun warning** dans la sortie (TypeScript, budgets, dépréciations). Un warning est un rouge :
   corrige la cause réelle, ne le masque jamais par configuration.
2. **Tests** : `pnpm -C web test --watch=false` (Vitest du scaffold, exécution unique).
   Deux points de forme, tous deux vérifiés, dont dépend la validité de l'étape :

   - Le `--watch=false` est **obligatoire** : `@angular/build` met `watch` à `true` en terminal
     interactif, l'omettre suspend la boucle.
   - **N'écris jamais le `--` séparateur** hérité de npm. `pnpm test -- --watch=false` ne se
     contente pas d'être superflu, il **échoue** : pnpm colle les arguments à la fin du script,
     `ng` reçoit un `--` nu et rejette la commande (`Option '--' has been specified multiple
     times`, puis `Schema validation failed`). Sous pnpm, tout ce qui suit le nom du script part
     déjà au script.

   Si un test échoue, corrige la cause réelle : dans l'immense majorité des cas le code de
   production, le test seulement s'il est réellement erroné (à signaler explicitement).

   **Ne restreins jamais l'exécution** (`--filter`, `--include`) pour obtenir le vert de la
   boucle : `--filter` matche les noms de tests et **sort en code 0 quand il ne matche rien**
   (« N skipped », exit 0). Un sous-ensemble filtré, ou un filtre erroné, produit un vert qui
   n'a rien exécuté. La boucle exige la suite complète ; lis le décompte de tests, jamais le
   seul code de sortie. Ces drapeaux servent à investiguer, pas à conclure.

Pas de lint ni de format côté web avant la semaine 13 : rien d'autre à lancer.

---

## Interdits absolus (anti-triche)

Aucune exception, même à la 3e passe, même si c'est la seule façon apparente d'obtenir le vert. Si le seul chemin vers le vert passe par une de ces actions, tu **t'arrêtes** et tu appliques le critère d'arrêt : tu ne l'appliques pas.

- **Ne jamais supprimer, commenter ou exclure un test** : pas de suppression de fichier/classe/méthode de test, pas de `[Fact(Skip = "...")]` ni `[Theory(Skip = "...")]`, pas de `#if false`, pas de retrait du test du `.csproj`.
- **Ne jamais désactiver une règle d'analyse** : pas de `#pragma warning disable`, pas de `[SuppressMessage]`, pas de `<NoWarn>` dans un `.csproj`, pas de changement de `dotnet_diagnostic.XXXX.severity` dans `.editorconfig`, pas de retrait de `SonarAnalyzer.CSharp`, `TreatWarningsAsErrors` ou `EnforceCodeStyleInBuild` dans `Directory.Build.props` / `Directory.Packages.props`.
- **Ne jamais affaiblir une assertion** : pas de remplacement d'une assertion précise (`Assert.Equal(...)`, `.Should().Be(...)`) par une assertion permissive (`Assert.True(true)`, `.Should().NotBeNull()` à la place d'une vérification de valeur), pas d'élargissement de tolérance sans justification métier explicite, pas de réduction des cas d'un `[InlineData]` pour esquiver un cas qui échoue.
- **Ne jamais modifier une donnée ou une fixture de test pour la faire coller à un résultat buggé** : si un test échoue, la cause à corriger est le code de production, pas la valeur attendue (`[InlineData]`, propriété d'un objet de fixture, valeur de retour d'un mock) qui a été ajustée pour que le test passe avec le comportement actuel.
- **Ne jamais toucher aux tests d'architecture** (`ArchitectureTests.cs`, `ProjectReferenceTests.cs`) pour les rendre plus permissifs. Ce sont les garde-fous des couches Clean Architecture, ils ne se négocient pas dans une boucle de vérification.
- **Côté web, mêmes principes** : ne jamais supprimer ou désactiver une spec (`.skip`, `.only`,
  `xit`, `xdescribe`, suppression de fichier) — ni son équivalent en ligne de commande, `--filter`
  ou `--include`, qui restreint l'exécution sans laisser de trace dans le code et rend un exit 0
  même sans aucun test exécuté ; ne jamais affaiblir `tsconfig*.json` (strict et
  options associées) ni faire taire le compilateur (`any` de complaisance, `@ts-ignore`,
  `@ts-expect-error`) ; ne jamais augmenter un budget ni masquer un warning par configuration
  dans `angular.json` pour obtenir le vert ; ne jamais ajuster une valeur attendue d'un test
  au comportement buggé.
- Si un test échoue parce qu'il est lui-même réellement erroné (faux positif avéré, pas juste gênant) : la seule correction autorisée est de le rendre correct, jamais plus permissif. Signale-le explicitement dans le rapport final, ne le corrige jamais en silence.

---

## Critère d'arrêt (toujours au rouge après 3 passes)

Arrête la boucle. N'écris aucun code de plus. Affiche ce rapport :

```
❌ /check bloqué après 3 passes

Étape qui bloque : [zone dotnet ou web : build / tests / format]
Dernière erreur exacte :
[extrait brut de la sortie dotnet/pnpm, pas une paraphrase]

Ce qui a été tenté :
- Passe 1 : [correction appliquée, résultat]
- Passe 2 : [correction appliquée, résultat]
- Passe 3 : [correction appliquée, résultat]

Hypothèse sur la cause racine :
[si tu en as une, sinon dis-le clairement]

Ce dont j'ai besoin pour débloquer :
[décision à prendre, information manquante, ou confirmation que la seule voie restante violerait une règle anti-triche listée plus haut]
```

Ne propose jamais, même en option ou "si tu es pressé", une solution qui viole les interdits ci-dessus.

---

## Rapport final (si vert)

```
✅ /check vert après [N] passe(s)

Zones vérifiées : [dotnet / web / les deux] (fichiers déclencheurs : [résumé])

- Passe 1 : [fichier(s) touché(s), nature du problème corrigé]
- Passe 2 : [...]  (si applicable)
- Passe 3 : [...]  (si applicable)

Étapes exécutées : toutes vertes.
Aucun test supprimé ou désactivé, aucune règle d'analyse désactivée, aucune assertion affaiblie, aucun warning masqué.
```

Reste factuel et court. Pas de "tout est parfait !".

---

## Notes

- Solution : `events-manager.slnx` à la racine du repo. Projets de tests : `tests/UnitTests/EventsManager.UnitTests.csproj` (unitaires) et `tests/IntegrationTests/EventsManager.IntegrationTests.csproj` (intégration, Testcontainers SQL Server → Docker actif requis).
- Front : workspace Angular `events-manager-web` sous `web/` (runner de tests Vitest du scaffold, builder `@angular/build:unit-test`). Les commandes de la boucle web ciblent `web/` via `pnpm -C web`, depuis la racine du repo.
- Pas de commit à la fin de `/check`. C'est une boucle de vérification locale, pas un workflow de publication.
- `/check` part du principe que le seul but légitime est de faire dire la vérité au code, jamais de faire taire les outils qui la disent.

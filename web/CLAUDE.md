# Front Angular 22

> Mémoire imbriquée : ce fichier complète le CLAUDE.md racine quand le travail se passe sous web/.
> Les règles globales restent d'application : gouvernance des commits, sous-agents, boucle de vérification.

## Defaults v22 à respecter (ne génère PAS de legacy)

- Composants standalone, signals partout, OnPush implicite, Zoneless (pas de zone.js, jamais).
- Réactivité : `signal()`, `computed()`, `linkedSignal()` ; `effect()` avec parcimonie. Entrées/sorties : `input()`, `output()`, `model()`. PAS de décorateurs `@Input()`/`@Output()`, pas de `BehaviorSubject` pour de l'état local.
- Formulaires : Signal Forms (PAS de ReactiveForms RxJS legacy, pas de `FormBuilder`).
- Lecture de données : Resource API (`resource` / `httpResource`). Mutations : `HttpClient` injecté dans un service.
- Routing : `provideRouter` + lazy loading via `loadComponent`/`loadChildren` (PAS de `provideRoutes`, pas de `NgModule`).
- Templates : control flow natif `@if` / `@for` (avec `track` obligatoire) / `@switch`. PAS de `*ngIf` / `*ngFor`.
- Injection : `inject()` en initialisation de champ, pas d'injection par paramètres de constructeur.
- Bindings hôte : propriété `host` du décorateur, pas de `@HostBinding`/`@HostListener`.
- HttpClient : FetchBackend par défaut (limite connue : pas d'événements de progression d'upload).

## Structure sous web/

- `src/app/layout/` : shell back-office (toolbar, navigation, page 404).
- `src/app/features/<domaine>/` : une feature par domaine d'API (events, orders...), routes lazy par feature.
- `src/app/core/` : configuration d'app (provideHttpClient, locale d'affichage, intercepteurs à venir). **Aucune base URL d'API, et c'est une décision** (2026-10-26) : nginx sert l'application *et* relaie `/api/`, donc le front et l'API partagent l'origine par construction et les appels sont **relatifs** (`/api/events/current`). Pas d'`environment.ts`, pas de variable d'environnement, rien à configurer par cible. Au `ng serve` cette origine commune est reconstituée par `web/proxy.conf.json`, qui vise `http://localhost:8080` parce que le service `api` du compose ne publie aucun port — nginx est le seul chemin vers l'API. Le jour où le front est servi par autre chose que nginx, la question se rouvre.
- Modèles TS écrits à la main, alignés champ à champ sur les DTOs de l'API (contrats en primitifs). Pas de génération de client.

## Outillage

- **Gestionnaire de paquets : pnpm**, déclaré par le champ `packageManager` de `package.json` (lu par Corepack et par pnpm lui-même) et par `cli.packageManager` d'`angular.json` (ce qu'invoquent `ng add` / `ng update`). Ce sont des déclarations, pas des verrous : rien ne fait échouer un `npm install` lancé par erreur — d'où la règle ci-dessous. `pnpm-lock.yaml` est versionné ; il n'y a plus de `package-lock.json`. Ne jamais lancer `npm` ni `npx` ici — `pnpm install`, `pnpm <script>`, et `pnpm dlx` à la place de `npx`. Installation reproductible : `pnpm install --frozen-lockfile`.
- pnpm **bloque par défaut les scripts d'install des dépendances** (surface d'attaque supply-chain). Les quatre paquets concernés ici (`@parcel/watcher`, `esbuild`, `lmdb`, `msgpackr-extract`) sont des accélérateurs natifs optionnels : build et tests sont verts sans eux, ils sont donc déclarés à `false` dans le bloc `allowBuilds` de `web/pnpm-workspace.yaml`. **Ce fichier est la seule déclaration de cette politique** depuis que le bloc `pnpm` de `package.json` a été retiré, et il est versionné pour qu'un clone frais en hérite. Piège de lecture : `allowBuilds: false` **interdit** le build. C'est la même décision que l'ancien `pnpm.ignoredBuiltDependencies`, écrite autrement, pas son inverse. Ne jamais la déduire du nom du champ : `pnpm -C web ignored-builds` la dit sur pièce. Conséquence utile : l'encadré d'avertissement à l'install reste vide, donc un nouveau paquet natif s'y verra immédiatement. Si un jour l'un d'eux devient nécessaire, passer sa valeur à `true`, jamais un blanc-seing global.
- Angular Material comme design system, thème M3 dérivé d'une couleur source (voir « Design tokens et theming »).
- Serveur MCP Angular (`angular-cli`) disponible : le consulter pour le contexte workspace et les bonnes pratiques avant de générer.
- TypeScript strict tel que scaffoldé : ne jamais l'affaiblir, aucun `any` non justifié.
- Polices Roboto et Material Icons chargées depuis les CDN Google (`index.html`). Le self-hosting fait l'objet d'une unité dédiée : ne pas l'introduire au passage.

## Design tokens et theming

Deux sources de vérité qui ne se recouvrent pas. **Ne jamais redéfinir en `--app-*` un token que Material fournit déjà** : un doublon, c'est deux vérités qui divergent au premier changement.

- **Material 3** couvre les couleurs de rôle (`--mat-sys-primary`, `surface`, `error`, `outline`...), l'échelle typographique complète, les rayons (`--mat-sys-corner-*`) et les élévations. Les tokens typographiques sont des **shorthands `font` complets** (graisse, taille, interligne, famille) : ne jamais les surcharger avec une simple taille.
- **Les `--app-*`** comblent ce que Material n'a pas : les espacements (`--app-spacing-xs` → `xxl`) et les couleurs sémantiques succès / avertissement (`--app-color-success`, `--app-color-on-success`, `--app-color-warning`, `--app-color-on-warning`).

Les deux fichiers de `src/styles/` n'ont pas le même statut, ne pas les confondre :

- `src/styles/_tokens.scss` est une **source écrite à la main**, elle s'édite directement. Dix `--app-*`, pas de génération, pas d'outil en amont.
- `src/styles/_theme-colors.scss` est un **fichier généré, à ne jamais éditer** : palette M3 dérivée de `#B0413E` par `ng generate @angular/material:theme-color --primary-color='#B0413E' --directory=src/styles/ --interactive=false`. Régénéré à la main uniquement, pas au build : la reproductibilité du schematic dépend de sa non-modification. Seul `primary` est fixé, M3 dérive le reste.

**Ne pas réintroduire de chaîne de génération pour ces tokens** : ni dossier `tokens/`, ni fichier source intermédiaire, ni dépendance de build chargée de transformer des valeurs en CSS, ni import depuis un outil de design externe. Une telle chaîne a existé et a été retirée délibérément : dix valeurs ne justifient pas une transformation qui ne protège que contre les erreurs qu'elle rend elle-même possibles.

Mode sombre : `mat.theme()` tourne avec son `theme-type` par défaut (`color-scheme`), donc Material émet déjà chaque `--mat-sys-*` en `light-dark(clair, sombre)`. Les `--app-color-*` reprennent la même convention, et `body { color-scheme: light dark; }` fait basculer les deux ensemble sur le réglage système. Ne pas introduire de sélecteur `.dark` ni de `@media (prefers-color-scheme)` en parallèle.

`light-dark()` n'est valide qu'en contexte couleur. Ne jamais l'employer sur un espacement : une custom property l'accepterait sans broncher, le build resterait vert, et la valeur ne casserait qu'au point d'utilisation.

## Conventions

- Un service par domaine d'API. Pas de logique métier dans les composants ni les services front : elle vit dans l'API, le front orchestre l'affichage et la saisie.
- Nommage : conventions du générateur v22, pas de retour aux anciens suffixes si le style guide ne les génère plus.
- Tests : specs ciblées sur le comportement (runner du scaffold v22) ; E2E Playwright gérés au niveau racine (`tests/e2e`), fin de roadmap.

## Vérification

- La boucle `/check` du repo couvre web/ : elle lance `pnpm -C web run build` puis `pnpm -C web test --watch=false` quand web/ est touché (commandes faisant foi dans `.claude/commands/check.md` ; le `--watch=false` est obligatoire, `@angular/build` met `watch` à `true` en terminal interactif). La lancer en fin de modification comme partout ailleurs, zéro erreur et zéro warning avant de rendre la main. Pas de lint configuré avant la semaine 13.

---
name: design-ui
description: Règles de design UI du front Angular d'events-manager. À charger avant toute tâche de développement front, c'est-à-dire toute création ou modification de fichiers sous web/ (composants, templates HTML, styles SCSS, pages, layout, navigation, formulaires) et toute tâche portant sur l'affichage, l'apparence, le visuel, l'UI, le thème Angular Material ou les design tokens. Ne concerne pas le backend .NET sous src/.
---

# Design UI

## Deux sources de vérité, jamais de recouvrement

**Ne jamais redéfinir en `--app-*` un token que Material fournit déjà.** Un doublon, c'est deux vérités qui divergent au premier changement.

- **Material 3** couvre les couleurs de rôle (`--mat-sys-primary`, `surface`, `error`, `outline`...), l'échelle typographique complète, les rayons et les élévations. Les tokens typographiques sont des shorthands `font` complets (graisse, taille, interligne, famille) : ne jamais les surcharger avec une simple taille.
- **Les `--app-*`** comblent uniquement ce que Material n'a pas : les espacements (`--app-spacing-xs` → `xxl`) et les couleurs sémantiques succès / avertissement. Ils sont écrits à la main dans `web/src/styles/_tokens.scss`.

**Ne pas réintroduire de chaîne de génération pour ces tokens** : ni dossier `tokens/`, ni fichier source intermédiaire, ni dépendance de build chargée de transformer des valeurs en CSS, ni import depuis un outil de design externe. Une telle chaîne a existé et a été retirée délibérément.

`web/src/styles/_theme-colors.scss` est un fichier **généré** par le schematic Material à partir de la graine `#B0413E` : ne jamais l'éditer, la reproductibilité du schematic en dépend. Aucun theming Material custom au-delà de cette graine (arbitrage du 2026-07-19).

## Mode clair et sombre

Les couleurs sont composées en `light-dark(clair, sombre)`, même convention que les `--mat-sys-*` émis par `mat.theme()`. **Ne jamais introduire de sélecteur `.dark` ni de `@media (prefers-color-scheme)`** en parallèle : `body { color-scheme: light dark; }` fait déjà basculer les deux ensemble.

`light-dark()` n'est valide qu'en contexte couleur. Ne jamais l'employer sur un espacement : la custom property l'accepterait, le build resterait vert, et la valeur ne casserait qu'au point d'utilisation.

## Accessibilité, règles actées

**Le primary rouge et le success vert ont des luminances voisines (0,13 et 0,15), indistinguables pour un daltonien deutéranope ou protanope. La couleur ne doit jamais être le seul porteur d'information : toujours la doubler d'une icône ou d'un mot.**

C'est la règle la plus facile à violer sans s'en rendre compte. Aucun outil du dépôt ne l'attrape aujourd'hui ; un contrôle a11y automatisé est prévu en semaine 12 et passera en CI en semaine 13. D'ici là, elle repose entièrement sur cette skill et sur la revue.

Conséquence du primary rouge : l'erreur M3 est rouge elle aussi, donc une parenté visuelle est attendue entre bouton primaire et message d'erreur. À observer sur les premiers écrans. Sortie éventuelle : `mat.theme-overrides` sur l'erreur, **jamais** changer le primary.

## Point encore ouvert

Roboto et Material Icons sont chargées depuis les CDN Google (`web/src/index.html`). Le self-hosting est une décision à prendre consciemment, pas à subir, et n'a pas encore été tranchée.

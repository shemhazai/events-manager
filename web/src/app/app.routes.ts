import { Routes } from '@angular/router';

import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      // La racine est la page publique de l'évènement courant, et non une page d'accueil de
      // back-office. Choix daté et réversible du 2026-10-26 : le but était qu'un déploiement
      // montre quelque chose, la séparation public / back-office est une unité à part.
      {
        path: '',
        loadComponent: () =>
          import('./features/events/current-event-page').then((m) => m.CurrentEventPage),
      },
      {
        path: '**',
        loadComponent: () => import('./layout/not-found/not-found').then((m) => m.NotFound),
      },
    ],
  },
];

import { httpResource } from '@angular/common/http';
import { Service } from '@angular/core';

import { CurrentEvent } from './current-event';

/**
 * Lecture de l'évènement courant. Transport seul : aucune règle métier ici, elle vit dans
 * l'API. La dérivation de l'état d'affichage appartient à la page, pas à ce service.
 *
 * `@Service()` et non `@Injectable({ providedIn: 'root' })` : forme recommandée pour un
 * singleton depuis Angular v22, auto-fournie.
 *
 * `httpResource` et non `HttpClient` + `subscribe` : c'est une lecture, et la Resource API
 * rend la requête sous forme de signaux — valeur, chargement, statut — directement
 * consommables par le template sans souscription à gérer.
 *
 * URL relative, sans base URL : voir le motif dans core/core.providers.ts.
 */
@Service()
export class CurrentEventService {
  readonly currentEvent = httpResource<CurrentEvent>(() => '/api/events/current');
}

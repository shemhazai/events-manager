import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { CurrentEvent } from './current-event';
import { CurrentEventService } from './current-event-service';

/**
 * Ce que la page a à rendre, en une seule valeur. Trois issues de la lecture, plus l'état
 * transitoire qui les précède.
 *
 * L'évènement est PORTÉ PAR la variante `found` et non lu à côté : la décision « trouvé »
 * et la donnée qui la justifie sont alors indissociables, et le template n'a aucun second
 * prédicat à appliquer pour retrouver l'une à partir de l'autre.
 */
type PageView =
  | { readonly kind: 'loading' }
  | { readonly kind: 'found'; readonly event: CurrentEvent }
  | { readonly kind: 'none' }
  | { readonly kind: 'error' };

/**
 * Page publique de l'évènement courant, racine de l'application.
 *
 * MONTAGE, CHOIX DATÉ ET RÉVERSIBLE (2026-10-26)
 * Cette page est l'entrée du front public, et elle s'affiche pourtant dans le shell du
 * back-office. Assumé : le but de l'unité est qu'un déploiement montre quelque chose, et la
 * séparation public / back-office est une restructuration à part. Ce n'est pas une
 * architecture, c'est un montage provisoire.
 *
 * LE 404 N'EST PAS UNE ERREUR, ET C'EST TOUT L'ENJEU DE CETTE PAGE
 * `GET /api/events/current` rend un 404 dès qu'aucune fenêtre de commande n'est ouverte,
 * soit onze mois sur douze. EV-016 : cet état est normal et attendu, il pilote la page
 * « aucun évènement à venir » et ne doit déclencher ni toast ni message d'erreur. Une page
 * qui afficherait « une erreur est survenue » en novembre serait conforme au code et fausse
 * au métier — et le défaut resterait invisible pendant la fenêtre du seed.
 */
@Component({
  selector: 'app-current-event-page',
  imports: [DatePipe, MatCardModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './current-event-page.html',
  styleUrl: './current-event-page.scss',
})
export class CurrentEventPage {
  private readonly currentEvent = inject(CurrentEventService).currentEvent;

  /**
   * LE SEUL ENDROIT OÙ L'ÉTAT DE LA PAGE SE DÉCIDE. Le template ne fait que lire cette
   * valeur : aucune branche ne réapplique un prédicat de son côté.
   *
   * La décision se prend sur le CODE DE STATUT et non sur la présence d'une erreur :
   * `statusCode()` est posé par httpResource avant la résolution de la ressource, y compris
   * sur une réponse d'erreur, alors que `error()` est typé `Error` et demanderait un
   * `instanceof` pour livrer la même information.
   *
   * L'ordre des tests porte trois décisions :
   *  - `isLoading()` d'abord, parce que `statusCode()` vaut `undefined` tant que la requête
   *    est en vol : sans ce premier test, la page tomberait dans l'état d'erreur PENDANT
   *    le chargement ;
   *  - le 404 ensuite, avant toute considération de corps : son corps Problem JSON ne nous
   *    apprend rien, l'état est normal (EV-016) ;
   *  - la présence d'un corps exploitable en dernier, pour que « 200 au corps vide » —
   *    interdit par EV-015, donc un défaut de l'API s'il survenait — rende l'état d'erreur
   *    et non un faux « aucun évènement à venir ».
   */
  protected readonly view = computed<PageView>(() => {
    if (this.currentEvent.isLoading()) {
      return { kind: 'loading' };
    }

    if (this.currentEvent.statusCode() === 404) {
      return { kind: 'none' };
    }

    // `hasValue()` avant `value()` : lire la valeur d'une ressource en erreur lève à
    // l'exécution.
    const event = this.currentEvent.hasValue() ? this.currentEvent.value() : undefined;

    return event ? { kind: 'found', event } : { kind: 'error' };
  });
}

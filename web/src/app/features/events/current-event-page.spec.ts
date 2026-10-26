import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { provideCore } from '../../core/core.providers';
import { CurrentEvent } from './current-event';
import { CurrentEventPage } from './current-event-page';

/**
 * Corps de référence : l'évènement du script de démonstration db/seed/saint-nicolas.sql.
 * Les quatre champs, et pas un de plus — en particulier pas de début de fenêtre (EV-011).
 */
const SAINT_NICOLAS: CurrentEvent = {
  id: '01a13361-398e-7fcb-a44f-2774c3a1fd36',
  name: 'Saint-Nicolas',
  date: '2026-12-06',
  order_window_end_date: '2026-11-22',
};

const NOT_FOUND_PROBLEM = {
  type: '/problems/not-found',
  title: 'Ressource introuvable',
  status: 404,
};

const INTERNAL_PROBLEM = {
  type: '/problems/internal-error',
  title: 'Erreur interne',
  status: 500,
};

describe('CurrentEventPage', () => {
  let fixture: ComponentFixture<CurrentEventPage>;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      // provideCore() et pas seulement le backend de test : la locale fr-BE fait partie de ce
      // qui est sous test, puisque c'est elle qui décide du format des dates rendues.
      providers: [provideCore(), provideHttpClientTesting()],
    });

    fixture = TestBed.createComponent(CurrentEventPage);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
  });

  /**
   * `TestBed.tick()` AVANT la réponse, `whenStable()` APRÈS, et l'ordre n'est pas un détail
   * de style : une requête HttpClient en vol est une tâche en attente, donc l'application
   * n'est jamais stable tant que la ressource attend sa réponse. Attendre la stabilité pour
   * pouvoir répondre serait un interblocage — la première écriture de ces specs l'a été, et
   * s'est soldée par six timeouts à 5 s.
   *
   * `tick()` fait tourner les effets, ce qui émet la requête, sans rien attendre. Une fois
   * la réponse remise, la stabilité redevient atteignable, et c'est elle qui garantit que
   * les signaux de la ressource sont propagés jusqu'au DOM.
   */
  async function respondWith(body: object, status?: number): Promise<string> {
    TestBed.tick();
    const request = backend.expectOne('/api/events/current');
    expect(request.request.method).toBe('GET');

    if (status === undefined) {
      request.flush(body);
    } else {
      request.flush(body, { status, statusText: `HTTP ${status}` });
    }

    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it("rend le nom et les deux dates de l'évènement sur un 200", async () => {
    const text = await respondWith(SAINT_NICOLAS);

    expect(text).toContain('Saint-Nicolas');
    // Les dates en français SONT l'assertion de la locale : sans fr-BE enregistrée, Angular
    // formaterait en en-US et rendrait « December 6, 2026 » sans casser le build.
    expect(text).toContain('6 décembre 2026');
    expect(text).toContain('22 novembre 2026');
  });

  it('rend « aucun évènement à venir » sur un 404', async () => {
    const text = await respondWith(NOT_FOUND_PROBLEM, 404);

    expect(text).toContain('Aucun évènement à venir');
  });

  /**
   * LA SPEC QUI JUSTIFIE L'UNITÉ.
   * EV-016 : le 404 est l'état normal onze mois sur douze. Le traiter comme une panne est le
   * mode d'échec de cette page, et il est invisible au build, invisible à l'œil pendant la
   * fenêtre de commande du seed, et conforme à toute lecture qui range « non-2xx » du côté
   * de l'erreur. D'où une assertion négative, séparée et explicite.
   */
  it("ne rend PAS l'état d'erreur sur un 404", async () => {
    const text = await respondWith(NOT_FOUND_PROBLEM, 404);

    expect(text).not.toContain('pas pu être chargé');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it("rend l'état d'erreur sur un 500", async () => {
    const text = await respondWith(INTERNAL_PROBLEM, 500);

    expect(text).toContain('pas pu être chargé');
    expect(text).not.toContain('Aucun évènement à venir');
  });

  /**
   * Prouve que la dernière branche attrape « tout le reste » et pas seulement un 500 : ici
   * aucune réponse HTTP n'arrive, donc aucun statut, et `statusCode()` reste indéfini.
   */
  it("rend l'état d'erreur sur un échec réseau, sans statut", async () => {
    TestBed.tick();
    backend.expectOne('/api/events/current').error(new ProgressEvent('error'));
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('pas pu être chargé');
    expect(text).not.toContain('Aucun évènement à venir');
  });

  it("rend le chargement avant la réponse, et aucun des trois états d'issue", async () => {
    TestBed.tick();
    const request = backend.expectOne('/api/events/current');

    expect(fixture.nativeElement.querySelector('mat-progress-spinner')).not.toBeNull();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Saint-Nicolas');
    expect(text).not.toContain('Aucun évènement à venir');
    expect(text).not.toContain('pas pu être chargé');

    // La requête est consommée ici, sinon backend.verify() la signalerait en attente.
    request.flush(SAINT_NICOLAS);
    await fixture.whenStable();
  });
});

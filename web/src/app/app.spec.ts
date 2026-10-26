import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { App } from './app';
import { routes } from './app.routes';
import { provideCore } from './core/core.providers';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      // La route racine est désormais une page qui lit l'API : sans backend de test, sa
      // requête s'échapperait du test. Ce qui est vérifié ici reste le routage, pas la page.
      providers: [provideRouter(routes), provideCore(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('renders the current event page on the default route', async () => {
    const harness = await RouterTestingHarness.create('/');
    const element = harness.routeNativeElement as HTMLElement;
    // Le titre est le seul élément constant des quatre états de la page : l'assertion tient
    // sans dépendre de la réponse de l'API, qui n'est pas le sujet de cette spec.
    expect(element.querySelector('h1')?.textContent).toContain('Prochain évènement');
  });

  it('renders the 404 page for an unknown route', async () => {
    const harness = await RouterTestingHarness.create('/route-inconnue');
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent).toContain('Page introuvable');
  });
});

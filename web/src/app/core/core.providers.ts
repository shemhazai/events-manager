import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { EnvironmentProviders, LOCALE_ID, makeEnvironmentProviders } from '@angular/core';
import localeFrBe from '@angular/common/locales/fr-BE';

/**
 * Configuration d'application transverse : client HTTP et locale d'affichage.
 *
 * Aucune base URL d'API ici, et ce n'est pas un oubli. nginx sert l'application ET relaie
 * /api/ : le front et l'API partagent l'origine par construction, donc les appels sont
 * relatifs (`/api/events/current`). Pas d'environment.ts, pas de variable d'environnement,
 * rien à configurer par cible. Le jour où le front est servi par autre chose que nginx, la
 * question se rouvre — et elle sera visible, parce qu'elle est écrite ici.
 */
export function provideCore(): EnvironmentProviders {
  // Angular ne livre que les données de la locale en-US. LOCALE_ID seul donnerait un
  // « fr-BE » sans données et casserait au premier DatePipe, à l'exécution et non au build.
  // L'appel vit dans le corps de la fonction plutôt qu'au niveau du module : l'ordre
  // d'exécution est alors explicite, et toute spec qui appelle provideCore() hérite de la
  // locale sans dépendre d'un effet de bord d'import.
  registerLocaleData(localeFrBe);

  return makeEnvironmentProviders([
    // Inopérant en l'état : HttpClient est injectable par défaut depuis Angular v21, et
    // provideHttpClient() ne sert qu'à configurer des features (intercepteurs, XSRF, XHR).
    // Conservé comme point d'accrochage déclaré des intercepteurs à venir, et pour que la
    // configuration HTTP de l'application ait un endroit, plutôt qu'aucun.
    provideHttpClient(),
    { provide: LOCALE_ID, useValue: 'fr-BE' },
  ]);
}

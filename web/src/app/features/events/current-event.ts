/**
 * Contrat de `GET /api/events/current`, aligné champ à champ sur `CurrentEventDto`
 * (docs/openapi.json). Écrit à la main, pas de génération de client.
 *
 * Propriétés en snake_case comme sur le fil : aucun mapper, aucune conversion de casse.
 * Un renommage en camelCase introduirait une couche à tester pour ne rien apporter, et
 * ferait diverger ce fichier du contrat qu'il est censé reproduire.
 *
 * EXACTEMENT QUATRE CHAMPS, et surtout pas de début de fenêtre de commande. EV-011 : un
 * évènement rendu a sa fenêtre ouverte par construction, le début n'a plus aucune utilité
 * pour le front, et un corps qui le porterait serait un défaut de l'API — pas un champ en
 * trop sans conséquence. Ne pas l'inventer ici non plus.
 *
 * Les deux dates sont des chaînes ISO 8601 date (« 2026-12-06 »), format `date` et non
 * `date-time` : pas d'heure, pas de fuseau. Elles restent des chaînes jusqu'au DatePipe.
 */
export interface CurrentEvent {
  readonly id: string;
  readonly name: string;
  readonly date: string;
  readonly order_window_end_date: string;
}

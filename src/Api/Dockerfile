# syntax=docker/dockerfile:1

# Image de l'API.
#
# Contexte de build : LA RACINE DU DÉPÔT, et non src/Api. Le projet a besoin de global.json,
# Directory.Build.props, Directory.Packages.props, .editorconfig et du manifeste d'outils
# .config/dotnet-tools.json, qui vivent tous à la racine.
#
# Deux cibles sont consommées par docker-compose.yml, qui les désigne explicitement :
# « runtime » pour le service api, « migrate » pour le service outil du même nom.

# ─── Étape 1 : restore ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Fichiers de configuration et .csproj d'abord, sources ensuite : la couche de restore ne
# s'invalide ainsi que si une dépendance ou une règle de build change, pas à chaque ligne de C#.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/Domain/EventsManager.Domain.csproj                 src/Domain/
COPY src/Application/EventsManager.Application.csproj       src/Application/
COPY src/Infrastructure/EventsManager.Infrastructure.csproj src/Infrastructure/
COPY src/Api/EventsManager.Api.csproj                       src/Api/

# Le projet, et non la solution : les trois projets de tests n'ont rien à faire dans l'image.
RUN dotnet restore src/Api/EventsManager.Api.csproj

COPY src/ src/

# ─── Étape 2 : publication de l'API ──────────────────────────────────────────
FROM build AS publish

# OpenApiGenerateDocumentsOnBuild=false n'est pas cosmétique : sans lui, la génération lancerait
# l'hôte factice GetDocument.Insider et écrirait un document dans docs/ À L'INTÉRIEUR de l'image,
# sans aucune utilité. Le docs/openapi.json versionné reste produit et vérifié sur le poste, par
# l'étape 1bis de /check.
RUN dotnet publish src/Api/EventsManager.Api.csproj \
      --configuration Release \
      --no-restore \
      -p:OpenApiGenerateDocumentsOnBuild=false \
      --output /app/publish

# ─── Étape 3 : construction du bundle de migrations ──────────────────────────
FROM build AS migrate-build

# dotnet-ef est un outil local, déclaré par .config/dotnet-tools.json.
RUN dotnet tool restore

# Pas de --startup-project : la factory design-time EventsManagerDbContextFactory suffit, et
# l'option --connection du bundle remplacera au lancement la chaîne de dev qu'elle code en dur.
# Ni --self-contained ni --target-runtime : le RID de cette image SDK est déjà linux-x64, et un
# bundle framework-dependent s'exécute sur l'image aspnet de l'étape suivante, qui porte le runtime.
RUN mkdir -p /app \
 && dotnet ef migrations bundle \
      --project src/Infrastructure/EventsManager.Infrastructure.csproj \
      --configuration Release \
      --output /app/efbundle

# ─── Étape 4 : exécution des migrations, sans SDK ─────────────────────────────
# Même image de base que l'étape d'exécution : aucune image supplémentaire à tirer, couches
# partagées. Appliquer les migrations reste un geste explicite — ce conteneur se lance par
# « docker compose run --rm migrate », il n'est chaîné à aucun « up ».
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrate

WORKDIR /app

COPY --from=migrate-build /app/efbundle ./

USER $APP_UID

ENTRYPOINT ["/app/efbundle"]

# ─── Étape 5 : exécution de l'API ────────────────────────────────────────────
# Image COMPLÈTE, et non une variante chiseled : celles-ci n'embarquent ni ICU ni tzdata, et
# TimeProviderExtensions.TodayLocal() rend alors un 500 sur toute route qui lit la date du jour,
# alors même que le conteneur est « Up » (mesuré le 2026-10-18). Les 160 Mo économisés ne valent
# pas une route cassée. Voir le commentaire de src/Application/TimeProviderExtensions.cs.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=publish /app/publish ./

# Utilisateur non root fourni par les images .NET depuis la 8. APP_UID porte son UID numérique,
# ce que réclament les contrôles de type runAsNonRoot.
USER $APP_UID

# Documentaire : l'image écoute en HTTP seul sur 8080, défaut des images ASP.NET depuis .NET 8.
# Aucun port n'est publié vers l'hôte dans le compose, nginx est le seul point d'entrée.
EXPOSE 8080

ENTRYPOINT ["dotnet", "EventsManager.Api.dll"]

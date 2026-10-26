# syntax=docker/dockerfile:1

# Image du front : build Angular, puis service des fichiers statiques par nginx.
# Contexte de build : web/. Modèle repris du POC d'Halloween, aux différences près qui sont
# commentées ci-dessous.

# ─── Étape 1 : build de l'application ────────────────────────────────────────
# Même majeure que le poste (v24.20.0 mesuré). Corepack est livré avec Node 24 mais annoncé comme
# retiré des distributions à partir de la 25 : ne pas monter de majeure sans en tenir compte.
FROM node:24-bookworm-slim AS build

WORKDIR /app

# pnpm est activé depuis le champ packageManager de package.json, empreinte comprise : la version
# du conteneur est donc celle du dépôt, indépendamment du pnpm installé sur le poste. Elle n'est
# pas répétée ici : recopiée, elle redeviendrait fausse au premier `corepack use`, et l'a déjà
# été. package.json fait foi, seul.
RUN corepack enable

# pnpm-workspace.yaml fait partie du trio : c'est lui qui porte la politique allowBuilds, qui
# refuse les scripts d'install des quatre paquets natifs. L'oublier changerait cette politique
# dans l'image, en silence.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm run build

# ─── Étape 2 : service des fichiers statiques ────────────────────────────────
FROM nginx:stable-alpine AS runtime

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Chemin de sortie par défaut de @angular/build pour le projet events-manager-web : angular.json
# ne définit aucun outputPath.
COPY --from=build /app/dist/events-manager-web/browser /usr/share/nginx/html

EXPOSE 80

# 127.0.0.1 et non localhost : nginx n'écoute qu'en IPv4, wget tenterait ::1.
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
  CMD wget --spider -q http://127.0.0.1/ || exit 1

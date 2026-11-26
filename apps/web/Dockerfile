# Imagen del cliente web de ProjectVTT (build estático + nginx con proxy
# hacia la API). Se construye desde la raíz del repositorio:
#   docker build -f apps/web/Dockerfile -t projectvtt-web .
FROM node:26-alpine AS build
WORKDIR /src
COPY apps/web/package*.json apps/web/
RUN cd apps/web && npm ci
COPY apps/web/ apps/web/
RUN cd apps/web && npm run build

FROM nginx:alpine
# Al verificar la imagen (Docker disponible), fijar aquí la versión exacta.
COPY infra/docker/nginx.selfhosted.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/apps/web/dist /usr/share/nginx/html
EXPOSE 80

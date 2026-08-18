# SPEC-004 — frontend image for the local container stack.
# nginx serves the built Angular bundle and reverse-proxies /api and /auth to the API, so
# the browser sees a single origin. That is what keeps the SPEC-003 cookie session and the
# X-XSRF-TOKEN double-submit contract working without any CORS configuration (AC-07).

FROM docker.io/library/node:24-alpine AS build
WORKDIR /src

# npm ci from the lockfile only; copying package files first keeps this layer cached
# across source edits.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM docker.io/library/nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
# @angular/build:application emits browser assets under dist/<project>/browser.
COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html

# 8080, not 80: unprivileged so the image also runs under rootless podman (AC-08).
EXPOSE 8080

# 127.0.0.1, not localhost: nginx listens on IPv4 here, while `localhost` inside the
# container resolves to ::1 first — which fails with "connection refused" (SPEC-004 EC-11).
HEALTHCHECK --interval=5s --timeout=3s --start-period=5s --retries=6 \
    CMD wget --quiet --spider http://127.0.0.1:8080/ || exit 1

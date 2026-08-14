# Build context = repo root (npm-workspaces monorepo). @kurx/admin depends on the @kurx/ui
# workspace package (packages/ui), so the whole workspace is installed together — a per-app
# build context can't see packages/ui and npm would try (and fail) to fetch it from the registry.
FROM node:22-bookworm-slim AS build
WORKDIR /repo
ENV NEXT_TELEMETRY_DISABLED=1
# NEXT_PUBLIC_* are inlined into the client bundle at build time, so they must be set before
# `npm run build` — a runtime value never reaches the browser (lib/site.ts).
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
# All workspace manifests first so `npm ci` can build the tree and this layer caches on lock changes only.
COPY package.json package-lock.json ./
COPY packages/ui/package.json ./packages/ui/
COPY web/package.json ./web/
COPY admin/package.json ./admin/
RUN npm ci
COPY packages ./packages
COPY admin ./admin
RUN npm run build -w @kurx/admin

FROM node:22-bookworm-slim AS runtime
WORKDIR /repo
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
# Keep the workspace layout so module resolution (hoisted node_modules + @kurx/ui symlink) matches build.
# Copied as `node` (D-256): the runtime user must own .next, because `next start` writes into
# .next/cache for ISR and image optimisation — root-owned files would make those writes fail at runtime
# rather than at build, which is the worst place to find out. `node` (uid 1000) ships with the image.
COPY --from=build --chown=node:node /repo/node_modules ./node_modules
COPY --from=build --chown=node:node /repo/package.json ./package.json
COPY --from=build --chown=node:node /repo/packages ./packages
COPY --from=build --chown=node:node /repo/admin ./admin
USER node
WORKDIR /repo/admin
# 3001, not 3000: the package's own start script is `next start -p 3001`.
EXPOSE 3001
CMD ["npm", "run", "start"]

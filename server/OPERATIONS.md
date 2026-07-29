# Kitchen Designer — Server Operations Guide

**Host:** `domovoy@192.168.0.189`
**Domain:** https://kitchendesigner.duckdns.org/
**Initial setup:** 2026-07-13

The deployed copy lives at `/opt/kitchen-designer/OPERATIONS.md`; this file is the source of
truth — edit here, then `scp` it over.

---

## Directory layout

```
/opt/kitchen-designer/
├── docker-compose.yml        ← main compose (db + web + nginx)
├── docker-compose.prod.yml   ← release override: image kitchen-server:release
├── docker-compose.debug.yml  ← debug override:   image kitchen-server:debug
├── Dockerfile                ← reference copy only; the image is built on the dev machine
├── server/
│   └── nginx.conf            ← nginx config (HTTPS + reverse proxy), bind-mounted
├── webgl/                    ← Unity WebGL build, bind-mounted read-only into web
│   └── _trash/               ← superseded builds parked by deploy.cmd
├── seed/
│   └── example.save.json     ← demo project, bind-mounted read-only as /app/seed-live
├── data/
│   └── pgdata/               ← PostgreSQL data (bind mount)
└── OPERATIONS.md             ← copy of this file
```

There is **no source tree on the server**. Both compose overrides run a pre-built image, so
`docker compose up` must never be given `--build` here: `docker-compose.yml` declares
`build.context: ..`, which resolves to `/opt` and fails with
`lstat /opt/server: no such file or directory`.

## Docker services

| Container | Image | Host ports | Purpose |
|---|---|---|---|
| kitchen-designer-db-1 | postgres:17.4-alpine | (none) | PostgreSQL |
| kitchen-designer-web-1 | kitchen-server:release / :debug | 8081 (MCP) | ASP.NET Blazor app |
| kitchen-designer-nginx-1 | nginx:1.27-alpine | 23080→80, 23443→443 | SSL termination + reverse proxy |

## Ports / Routing

```
Internet → 217.114.238.97:443 → router → 192.168.0.189:23443 → nginx:443 (HTTPS)
Internet → 217.114.238.97:80  → router → 192.168.0.189:23080 → nginx:80  (301 → HTTPS)
LAN      → 192.168.0.189:8081                → web:8081        (MCP hub, no auth)
```

The WebGL client is served by the ASP.NET app under `/unity` (`WebGL:RootPath=/app/webgl`,
see `Services/UnityWebGLStaticFiles.cs`), not by nginx — nginx proxies everything to
`web:8080`.

## Where the data lives

| Data | Location | Survives a deploy? |
|---|---|---|
| Accounts (ASP.NET Identity) | Postgres, bind mount `./data/pgdata` | Yes — the `db` service is untouched |
| Saved projects | named volume `kitchen-designer_appdata` → `/app/data/projects` | Yes — named volumes outlive containers |
| Data-protection keys (auth cookies) | same volume → `/app/data/keys` | Yes — sessions stay valid |
| SSL certs | `/etc/letsencrypt` on the host | Yes — never touched by deploy |
| WebGL client | `./webgl`, bind-mounted read-only | Replaced by every deploy (old build → `webgl/_trash`) |
| Demo project | `./seed/example.save.json`, mounted as `/app/seed-live` | Replaced by every deploy |

### Demo project

`docs/example.save.json` reaches the server twice over: baked into the image at `/app/seed`
and bind-mounted at `/app/seed-live`, which wins (`ExampleProjectService.DefaultSearchPaths`).
The mount is what lets `deploy.cmd` refresh the demo without shipping a new image; the baked
copy is the fallback if the mount is ever missing.

The service reads the file **once at startup**, so a new demo needs `restart web` —
`deploy.cmd` does that automatically, and only when the file actually changed. Users who
already imported the example get a new version of their project on their next visit to
`/demo` or the project list; their own edits are not deleted but move into version history.

Schema handling on startup (`Program.cs`): `EnsureCreated()` is a no-op on an existing
database, followed by additive `ALTER TABLE … ADD COLUMN IF NOT EXISTS`. Nothing drops or
rewrites existing rows, so a release rollout does not touch accounts or projects.

## Deploy (from the dev machine)

### Release deploy

```
cd F:\repos\KitchenDesigner2\kd-repose
build.cmd -WebGL
deploy.cmd
```

`deploy.cmd` does: build `kitchen-server:release` locally → prepare remote dirs and park stale
builds → scp `Builds/WebGL` → scp compose files + `nginx.conf` → `docker save | ssh docker load`
→ `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d` → `nginx -t` + reload
→ `/health` check.

Only the client or the demo changed? Skip the image entirely — both are bind mounts. The
WebGL build goes live with no restart; the demo triggers a `restart web` only if its content
actually changed:

```
deploy.cmd -WebGLOnly
```

### Debug deploy

```
cd F:\repos\KitchenDesigner2\kd-repose
build-deploy-debug.cmd
```

Same mechanism with `Builds/WebGL_Debug` and the `kitchen-server:debug` image
(`docker-compose.debug.yml`).

### What gets copied (and what does NOT)

**Copied by `deploy.cmd`:**
- `server/docker-compose.yml`, `server/docker-compose.prod.yml` → `/opt/kitchen-designer/`
- `server/nginx.conf` → `/opt/kitchen-designer/server/nginx.conf` (this is the path nginx
  bind-mounts; a copy in the root directory is ignored)
- `Builds/WebGL/*` → `/opt/kitchen-designer/webgl/`
- `docs/example.save.json` → `/opt/kitchen-designer/seed/` (also baked into the image)
- the `kitchen-server:release` image, as a `docker save` stream — the ASP.NET publish happens
  inside the image build, nothing is published to the server as loose DLLs

**NOT copied (host-only state):**
- `/etc/letsencrypt/` — SSL certs
- `/opt/kitchen-designer/data/` — PostgreSQL data
- `kitchen-designer_appdata` volume — projects and data-protection keys
- `certbot-www` volume — ACME challenge files

## SSL / Let's Encrypt

**Certificate:** `/etc/letsencrypt/live/kitchendesigner.duckdns.org/fullchain.pem`
**Key:** `/etc/letsencrypt/live/kitchendesigner.duckdns.org/privkey.pem`

### Auto-renewal

- `certbot.timer` (systemd) runs twice daily
- On successful renewal: `/etc/letsencrypt/renewal-hooks/deploy/reload-nginx.sh` executes
- The hook runs: `docker exec kitchen-designer-nginx-1 nginx -s reload`
- nginx mounts certs read-only from host, so reload picks up new certs instantly

### Manual renewal

```bash
sudo certbot renew --http-01-port 23080
```

If nginx is running, stop it first:

```bash
cd /opt/kitchen-designer && docker compose stop nginx
sudo certbot renew --http-01-port 23080
docker compose up -d nginx
```

### Reissue from scratch (if expired/revoked)

```bash
cd /opt/kitchen-designer && docker compose stop nginx
sudo certbot certonly --standalone --http-01-port 23080 \
  -d kitchendesigner.duckdns.org --non-interactive --agree-tos \
  --email admin@kitchendesigner.duckdns.org
docker compose up -d nginx
```

## Manual operations

Every `docker compose` call on the server needs both files — plain `docker compose …` would
try to build from a context that does not exist:

```bash
cd /opt/kitchen-designer
alias kdc='docker compose -f docker-compose.yml -f docker-compose.prod.yml'
```

### Check nginx config validity

```bash
docker exec kitchen-designer-nginx-1 nginx -t
```

### Reload nginx without restart

```bash
docker exec kitchen-designer-nginx-1 nginx -s reload
```

### View logs

```bash
docker logs kitchen-designer-nginx-1 --tail 50
docker logs kitchen-designer-web-1 --tail 50
```

### Restart entire stack

```bash
cd /opt/kitchen-designer
docker compose -f docker-compose.yml -f docker-compose.prod.yml down
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

### Check cert expiry

```bash
echo | openssl s_client -connect kitchendesigner.duckdns.org:443 2>/dev/null | \
  openssl x509 -noout -dates
```

### PostgreSQL backup

```bash
docker exec kitchen-designer-db-1 pg_dump -U kitchen kitchendb > backup_$(date +%Y%m%d).sql
```

## Troubleshooting

| Problem | Fix |
|---|---|
| 502 Bad Gateway | `docker logs kitchen-designer-web-1` — web container is down |
| `lstat /opt/server: no such file or directory` | A `docker compose` call was made without the override, or with `--build`. Use both `-f` files and no `--build`. |
| SSL cert error | Check expiry with the openssl one-liner above. If expired, renew manually. |
| nginx won't start | `docker exec kitchen-designer-nginx-1 nginx -t` — config syntax error |
| Deploy breaks HTTPS | The deploy copies `nginx.conf` with HTTPS baked in. Just re-run deploy. Certs on the host are safe. |
| ACME challenge fails | Stop nginx first: `docker compose … stop nginx`, then run certbot standalone on port 23080 |

## DuckDNS

If the DuckDNS token/IP needs updating:

```bash
curl "https://www.duckdns.org/update?domains=kitchendesigner&token=YOUR_TOKEN&ip="
```

# Déployer TradX

TradX se déploie en **trois conteneurs** décrits dans `docker-compose.prod.yml` :

```text
Internet ──HTTPS──▶ caddy (certificat automatique) ──▶ app (API + site React) ──▶ db (PostgreSQL)
                                                         └── volume « uploads » (photos)
```

- **app** : une seule image (`Dockerfile`) ; l'API ASP.NET Core sert aussi le site. Site et API partagent donc
  le même domaine, ce qu'exige le cookie de session (`SameSite=Strict`).
- **caddy** : obtient et renouvelle seul le certificat HTTPS (Let's Encrypt) et transmet l'IP réelle des visiteurs.
- **db** : PostgreSQL 17, jamais exposé sur Internet.
- **Mode démonstration** : aucun e-mail n'est envoyé, un compte est utilisable dès l'inscription
  (voir `README.md`, section configuration).

## 1. Ce qu'il faut

- Un **serveur Linux** (VPS) avec Docker et Docker Compose, 1 Go de RAM minimum (2 Go conseillés).
  Processeur x64 ou ARM (l'image se construit pour les deux).
- Un **nom de domaine** dont l'enregistrement DNS `A` (et `AAAA` en IPv6) pointe vers l'adresse du serveur.
- Les ports **80** et **443** ouverts dans le pare-feu du serveur (Caddy en a besoin pour le certificat).

## 2. Première installation

```bash
git clone https://github.com/zikipedo/Tradex.git && cd Tradex
cp deploy/.env.production.example .env.production
```

Remplir `.env.production` (ce fichier n'est **jamais** commité) :

| Variable | Valeur |
|---|---|
| `DOMAIN` | le domaine, par exemple `tradx.exemple.ml` |
| `POSTGRES_PASSWORD` | long et aléatoire : `openssl rand -base64 32` |
| `JWT_SIGNING_KEY` | au moins 32 octets aléatoires : `openssl rand -base64 48` |
| `ADMIN_EMAIL` | facultatif : ce compte, une fois inscrit, devient administrateur au démarrage suivant |

Puis lancer :

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
docker compose -f docker-compose.prod.yml --env-file .env.production logs -f app
```

Au premier démarrage, l'application crée les tables (migrations) puis écoute. Le site est en ligne sur
`https://<DOMAIN>` dès que Caddy a obtenu le certificat (quelques secondes, si le DNS est correct).

Vérification rapide : `curl https://<DOMAIN>/health` doit répondre `Healthy`.

## 3. Mettre à jour

```bash
git pull
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

Les migrations éventuelles s'appliquent au redémarrage. Les données (base, photos, certificats) sont dans des
volumes Docker : elles survivent aux mises à jour.

## 4. Sauvegarder et restaurer

**Sauvegarde** de la base (à planifier chaque jour, et à copier hors du serveur) :

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production exec -T db \
  pg_dump -U tradx -d tradx --format=custom > tradx-$(date +%F).dump
```

**Photos** : sauvegarder le volume `tradx_uploads` (par exemple avec `docker run --rm -v tradx_uploads:/d -v "$PWD":/b alpine tar czf /b/photos.tgz -C /d .`).

**Restauration** (à tester régulièrement sur une copie) :

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production exec -T db \
  pg_restore -U tradx -d tradx --clean --if-exists < tradx-AAAA-MM-JJ.dump
```

## 5. Sécurité en place

- HTTPS partout (redirection automatique), HSTS, en-têtes OWASP, CSP distincte pour l'API et pour le site.
- L'application tourne avec un utilisateur **non root** ; la base n'est pas joignable depuis Internet.
- Aucun secret dans l'image ni dans Git : tout vient de `.env.production`.
- Les en-têtes `X-Forwarded-*` ne sont acceptés que depuis le réseau Docker (`172.16.0.0/12`), donc seulement de Caddy.
  Si Docker utilise un autre réseau sur le serveur, adapter `ForwardedHeaders__KnownNetworks__0`.
- Le site compilé est servi avec un cache d'un an pour les fichiers versionnés et sans cache pour `index.html`.

## 6. Limites connues

- **Une seule instance** de l'application : les migrations au démarrage et le stockage des photos sur disque
  supposent un seul conteneur `app`. Pour plusieurs instances : migrations par une étape dédiée et photos sur un
  stockage compatible S3 (l'interface `IFileStorage` est prête).
- **Mode démonstration** : pas d'e-mail, donc pas de « mot de passe oublié ». Pour un vrai lancement :
  `Email__Mode=Smtp` (avec les variables SMTP) et `Auth__RequireEmailConfirmation=true`.
- **Hébergeur sans Docker Compose** (plateforme qui exécute une seule image) : l'image `Dockerfile` suffit, avec
  une base PostgreSQL fournie par l'hébergeur (`ConnectionStrings__TradX`) ; déclarer alors le réseau du proxy de
  l'hébergeur dans `ForwardedHeaders__KnownNetworks__0` et vérifier que le disque des photos est persistant.

## 7. Essai sur sa propre machine

Avec `DOMAIN=localhost`, `HTTP_PORT=8080` et `HTTPS_PORT=8443` dans un fichier d'environnement de test, la même
commande lance toute la pile en local : `https://localhost:8443`. Le navigateur affichera un avertissement de
certificat (Caddy utilise un certificat local pour `localhost`) ; sur un vrai domaine, le certificat est reconnu.

# TradX : API d'échange de biens et de services sans argent

TradX permet d'échanger des **biens** et des **services** entre deux personnes, **sans argent**, de façon **équitable**.
Ce dépôt contient le **backend** : une API REST en **ASP.NET Core (.NET 10)**, qui servira l'application web (le front sera conçu ensuite).

> Ce backend a été développé avec l'assistance d'une IA (Claude). Chaque étape a été testée (`dotnet test`) avant d'être committée.

---

## 1. Démarrer en 3 commandes

Prérequis : **.NET SDK 10**, **Docker Desktop** (démarré).

```powershell
docker compose up -d                      # base PostgreSQL 17 (port local 5433)
dotnet test                               # 275 tests : unitaires + intégration (Docker requis)
dotnet run --project src/TradX.Api        # API sur http://localhost:5191
```

- Documentation interactive de l'API : **http://localhost:5191/scalar/v1** (développement uniquement).
- Les migrations de la base sont appliquées automatiquement au démarrage en développement.
- Les e-mails (confirmation, mot de passe oublié) sont écrits dans `src/TradX.Api/.mail-outbox/` en développement : ouvrez le fichier pour récupérer le lien.
- Premier administrateur : définir `Bootstrap__AdminEmail=votre@email` (variable d'environnement) puis redémarrer. Ce compte doit exister et avoir confirmé son e-mail.

---

## 2. .NET et ASP.NET Core, en bref

| Élément | Rôle | Équivalent Java |
|---|---|---|
| **C#** | le langage | Java |
| **.NET 10** | la plateforme (runtime + SDK + bibliothèques) | JVM + JDK |
| **ASP.NET Core** | le framework web (contrôleurs, sécurité, middlewares) | Spring Boot |
| **EF Core** | l'accès à la base de données (ORM) | JPA / Hibernate |
| **NuGet** (`Directory.Packages.props`) | les dépendances | Maven |
| **xUnit** | les tests | JUnit |

---

## 3. Architecture (Clean Architecture)

```text
src/
  TradX.Domain/          Le cœur métier : entités et règles, sans aucune dépendance technique.
  TradX.Application/     Les cas d'utilisation (services), les DTO et leur validation.
  TradX.Infrastructure/  La technique : PostgreSQL, JWT, hachage, e-mails, images, fichiers.
  TradX.Api/             La porte d'entrée HTTP : contrôleurs, sécurité, erreurs.
tests/
  TradX.UnitTests/         Tests rapides du domaine et des services.
  TradX.IntegrationTests/  L'API complète contre un vrai PostgreSQL (Testcontainers).
```

Les dépendances vont vers l'intérieur : `Api → Application → Domain`, et `Infrastructure → Application`.
Le domaine ne sait rien de la base ni du web, ce qui le rend facile à tester.

**Trajet d'une requête** (exemple : accepter un échange) :

1. `ExchangesController.Accept` reçoit `POST /api/v1/exchanges/{id}/accept`. Le contrôleur ne contient aucune logique.
2. `ExchangeService.AcceptAsync` charge l'échange **seulement si l'utilisateur en fait partie** (sinon 404), puis applique la règle.
3. `Exchange.Accept()` (domaine) vérifie que c'est bien l'auteur de l'annonce et que la transition est autorisée.
4. `SaveChangesAsync()` enregistre tout (échange, annonces réservées, audit) dans **une seule transaction**.
5. Si une règle est violée, une exception métier devient une réponse **ProblemDetails** (`GlobalExceptionHandler`).

---

## 4. Règles métier

### Trois façons d'échanger

| Type | Pour quoi | Contrepartie |
|---|---|---|
| **Troc** (`Barter`) | bien ↔ bien, bien ↔ service… | une de mes annonces publiées, aucun crédit |
| **Banque de temps** (`TimeBank`) | un service | des heures : **1 h de service = 1 h de crédit**, quel que soit le service |
| **Don** (`Donation`) | une annonce marquée « don » | aucune |

### Banque de temps : ce n'est pas de l'argent
- Les crédits **ne s'achètent pas, ne se vendent pas, ne se retirent pas**. Aucun endpoint ne permet de modifier un solde.
- Durée : multiple de **30 min**, de 30 min à 8 h. Solde de départ **0**. Découvert **−5 h** (pour démarrer). Plafond **+100 h**.
- Les heures ne bougent qu'après la confirmation des **deux** parties. Chaque mouvement est écrit dans un **grand livre non modifiable**.
- La somme de tous les soldes vaut toujours **0** (vérifié par un test).

### Cycle de vie d'un échange
```text
Proposé ──accepter──▶ Accepté ──les 2 confirment──▶ Réalisé
   │                     │
   ├─ refuser ─▶ Refusé  ├─ annuler ─▶ Annulé (impossible si une partie a déjà confirmé)
   ├─ annuler ─▶ Annulé  └─ litige ──▶ Litige ──modérateur──▶ Réalisé ou Annulé
   └─ 7 jours sans réponse ─▶ Expiré (tâche de fond)
```
À l'acceptation, les **biens** sont réservés. À la fin, ils passent à « échangé » et les autres propositions sur ces biens sont refusées automatiquement.
Les **services** restent disponibles : on peut les rendre plusieurs fois.

### Annonces
`Brouillon → Publiée → Réservée → Échangée`, plus `Archivée` et `Suspendue` (modération).
Il n'y a **aucun champ prix** : envoyer un champ `price` est refusé (400). Il faut un e-mail confirmé pour publier ou proposer un échange.

### Confiance
- **Avis** (1 à 5) uniquement après un échange terminé, un seul par partie : impossible de laisser un faux avis.
- **Signalements** : **3 signalements** de comptes **vérifiés et âgés d'au moins 7 jours** suspendent l'annonce à titre préventif. Les comptes créés pour l'occasion ne comptent pas. L'auteur peut contester, un modérateur tranche.
- **Messagerie** privée par échange. Un modérateur ne peut la lire qu'en cas de **litige** (preuves).

---

## 5. Endpoints principaux (`/api/v1`)

| Domaine | Méthode et route | Accès |
|---|---|---|
| Auth | `POST auth/register`, `confirm-email`, `resend-confirmation`, `login`, `refresh`, `logout`, `forgot-password`, `reset-password` | public, limité par IP |
| Profil | `GET/PUT users/me` · `GET users/{id}` · `GET users/{id}/reviews` | membre · public |
| Admin | `PUT admin/users/{id}/role` · `GET/POST/PUT admin/categories` | Admin |
| Catégories | `GET categories` | public |
| Annonces | `GET listings` (recherche) · `GET listings/mine` · `GET/POST/PUT/DELETE listings/{id}` · `POST listings/{id}/publish`, `archive`, `appeal` | public en lecture, auteur en écriture |
| Photos | `POST listings/{id}/photos` (multipart) · `GET/DELETE listings/{id}/photos/{photoId}` | auteur · selon visibilité |
| Échanges | `POST exchanges` · `GET exchanges` · `GET exchanges/{id}` · `POST exchanges/{id}/accept`, `decline`, `cancel`, `confirm`, `dispute` | parties uniquement |
| Temps | `GET time-account` | membre |
| Avis | `POST exchanges/{id}/review` | parties d'un échange terminé |
| Messages | `GET/POST exchanges/{id}/messages` · `POST exchanges/{id}/messages/read` | parties |
| Signalement | `POST listings/{id}/reports` | membre vérifié |
| Modération | `GET moderation/reports` · `GET moderation/listings/suspended` · `POST moderation/listings/{id}/decision` · `GET moderation/disputes` · `POST moderation/disputes/{id}/resolve` | Moderator, Admin |
| Santé | `GET /health` | public |

Toutes les listes sont **paginées** (`page`, `pageSize` ≤ 50). Les messages utilisent un **curseur** (`before`).
Toutes les erreurs suivent le format **ProblemDetails** avec un `code` stable (ex. `timebank.insufficient_balance`) que le front peut traduire.

---

## 6. Sécurité (OWASP Top 10)

| Risque OWASP | Protection dans TradX |
|---|---|
| A01 Contrôle d'accès | *Deny by default* (tout endpoint exige une connexion sauf `[AllowAnonymous]`), rôles, **contrôle de propriété dans chaque service** : la ressource d'autrui renvoie 404 (anti-IDOR), testé pour chaque action |
| A02 Cryptographie | Mots de passe hachés PBKDF2 (ASP.NET Identity) ; jetons e-mail et refresh **stockés hachés** (SHA-256) ; JWT signé HS256 avec une clé ≥ 256 bits venant de l'environnement |
| A03 Injection | Uniquement LINQ / EF Core (requêtes paramétrées), testé avec `' OR 1=1; DROP TABLE` ; messages stockés en texte brut et servis en JSON (pas de XSS côté API) |
| A04 Conception | Machines à états centralisées, contraintes en base (soldes bornés, index uniques, grand livre et audit non modifiables par déclencheur) |
| A05 Configuration | En-têtes de sécurité (CSP, X-Frame-Options, nosniff, HSTS hors dev), CORS en liste blanche, documentation Scalar en dev uniquement, aucune stacktrace renvoyée |
| A06 Composants | `dotnet list package --vulnerable` dans la CI (0 vulnérabilité aujourd'hui), versions centralisées |
| A07 Authentification | Verrouillage après 5 échecs, messages génériques (pas d'énumération des comptes, même durée de réponse), refresh token rotatif avec **détection de réutilisation**, `alg: none` refusé, limitation de débit |
| A08 Intégrité | Champs JSON inconnus refusés (pas d'affectation de masse), verrouillage optimiste (`xmin`) contre les modifications concurrentes |
| A09 Journalisation | Journal d'audit **append-only** (connexions, rôles, échanges, temps, modération), logs JSON avec `CorrelationId`, aucun secret ni jeton dans les logs |
| A10 SSRF | Aucune URL fournie par l'utilisateur n'est appelée par le serveur |

**Photos** : vrai type vérifié par les octets magiques, réencodage en WebP qui **supprime les métadonnées EXIF et GPS**, 5 Mo et 5 photos maximum, protection contre le *path traversal* et les images géantes.

---

## 7. Tests (275)

```powershell
dotnet test                                                     # tout
dotnet test tests/TradX.UnitTests                               # rapides, sans Docker
dotnet test --collect:"XPlat Code Coverage;Format=opencover"    # avec couverture
```

- **213 tests unitaires** : règles du domaine (transitions, banque de temps, avis…), services (base en mémoire), validateurs, cryptographie, traitement d'image.
- **62 tests d'intégration** : l'API complète contre un **vrai PostgreSQL 17** lancé par Testcontainers. On y teste les parcours complets, les rôles, l'IDOR, la falsification de JWT, la limitation de débit, la concurrence (deux acceptations simultanées) et les garde-fous de la base.
- **Couverture** : environ **93 %** des lignes hors code généré (domaine 99 %, application 96 %).

---

## 8. Qualité : SonarQube et CI

**SonarQube en local** :
```powershell
docker compose --profile quality up -d     # http://localhost:9000 (admin/admin au premier lancement)
$env:SONAR_TOKEN = "<jeton créé dans SonarQube>"
./scripts/sonar.ps1
```

**GitHub Actions** (`.github/workflows/ci.yml`) : compilation (avertissements = erreurs), tous les tests, analyse des dépendances vulnérables, puis SonarCloud si le secret `SONAR_TOKEN` et les variables `SONAR_PROJECT_KEY` / `SONAR_ORGANIZATION` sont configurés.

---

## 9. Configuration (variables d'environnement)

Voir `.env.example`. En production, ces valeurs sont **obligatoires** et ne sont **jamais** dans le code :

| Variable | Rôle |
|---|---|
| `ConnectionStrings__TradX` | connexion PostgreSQL |
| `Jwt__SigningKey` | clé de signature JWT (≥ 32 octets aléatoires) |
| `Cors__AllowedOrigins__0` | adresse du front (le joker `*` est refusé en production) |
| `Email__Mode=Smtp`, `Email__SmtpHost`, `Email__SmtpUser`, `Email__SmtpPassword`, `Email__From` | envoi des e-mails |
| `Auth__FrontendBaseUrl` | adresse du front pour les liens envoyés par e-mail |
| `ForwardedHeaders__KnownProxies__0` | IP du reverse proxy, s'il y en a un |

---

## 10. Pistes pour la suite

- **Front web** : consommer cette API (le contrat est décrit par OpenAPI sur `/openapi/v1.json`).
- Messagerie en **temps réel** avec SignalR (aujourd'hui en REST).
- Stockage des photos sur un service compatible **S3** (l'interface `IFileStorage` est prête).
- Notifications (nouvelle proposition, nouveau message).
- Avant le lancement : vérifier la loi malienne sur les données personnelles et rédiger les conditions d'utilisation (liste des objets interdits).

# CLAUDE.md : TradX, plateforme d'échange de biens et de services sans argent

## 0. Résumé

**TradX** est une application **web** où les gens échangent des **biens** et des **services** **sans argent**, de façon **équitable**.
Lancement : **Mali (Bamako)**, en **français**. Pas d'application mobile : web uniquement (le front sera généré ensuite et consommera l'API).

**Principes non négociables**
1. **Aucun argent** dans l'application : rien ne s'achète, rien ne se vend, rien ne se retire. Aucun champ prix, aucun paiement.
2. **Équité** : un échange n'a lieu que si les deux parties l'acceptent et confirment l'avoir reçu.
3. **Les crédits de temps ne se créent ni ne se perdent** : chaque mouvement est tracé dans un grand livre append-only.
4. **Sécurité d'abord** (OWASP) : on ne désactive jamais une protection pour « faire marcher ».
5. **Beaucoup de tests**, code commenté, un commit propre à chaque étape testée.

## 1. Règles métier

### 1.1 Trois façons d'échanger (`ExchangeKind`)
| Type | Pour quoi | Contrepartie |
|---|---|---|
| `Barter` (troc) | bien ↔ bien, bien ↔ service, service ↔ service | l'annonce proposée en retour ; **aucun crédit ne bouge** |
| `TimeBank` (banque de temps) | un **service** | des **crédits de temps** : 1 heure de service = 1 heure de crédit, quel que soit le service |
| `Donation` (don) | une annonce marquée « don » | aucune |

- Un échange se fait **uniquement entre deux personnes** (pas de chaîne A→B→C au départ).
- On ne peut pas échanger avec soi-même.

### 1.2 Banque de temps (décisions)
- Les crédits sont du **temps**, pas de l'argent : ils **ne s'achètent pas, ne se vendent pas, ne se retirent pas**, ne se transfèrent pas hors d'un échange terminé.
- Unité stockée : **minutes entières** (`int`). Durée d'un service : multiple de **30 min**, entre 30 min et 8 h.
- Solde de départ : **0**. Découvert autorisé : **−5 h** (−300 min), pour que les nouveaux puissent démarrer. Plafond : **+100 h** (6 000 min), pour éviter la thésaurisation.
- Les crédits bougent **seulement** quand les **deux** parties ont confirmé « échange réalisé ».
- Grand livre `time_transactions` **append-only** ; la somme de tous les soldes est toujours **0**.
- Le solde est protégé par un **jeton de concurrence** (pas de double débit en parallèle).

### 1.3 Annonces (`Listing`)
- Sens : `Offer` (« Je propose ») ou `Request` (« Je cherche »).
- Type : `Good` (bien) ou `Service`.
- Champs : titre, description, catégorie, état (`New`, `Good`, `Used`, pour un bien), ville + quartier, disponibilité (texte), « ce que je cherche en échange », option **don**, jusqu'à **5 photos** (5 Mo max, JPEG/PNG/WebP, EXIF supprimé, réencodées).
- Statuts : `Draft → Published → Reserved → Exchanged`, plus `Archived` et `Suspended` (modération).
- Recherche paginée : mot-clé, catégorie, type, sens, ville, quartier, don, tri par date.
- Catégories : liste gérée par l'admin.
- Pour publier ou proposer un échange, l'e-mail doit être **confirmé**.

### 1.4 Cycle de vie d'un échange (machine à états, un seul endroit)
```text
Proposed → Accepted → Completed
         ↘ Declined
         ↘ Cancelled (par le demandeur, avant acceptation)
Accepted → Cancelled (par l'une ou l'autre partie, avant confirmation finale)
Accepted → Disputed (litige, traité par un modérateur)
Proposed → Expired (sans réponse après 7 jours)
```
- `Completed` seulement quand **les deux** parties ont cliqué « échange réalisé ».
- À l'acceptation, les annonces concernées passent `Reserved` ; à la fin, `Exchanged` ; si annulé, elles redeviennent `Published`.
- Chaque transition est auditée.

### 1.5 Avis, messagerie, signalements
- **Avis** : note 1 à 5 + commentaire, **uniquement après un échange `Completed`**, un avis par partie et par échange.
- **Messagerie** : messages liés à un échange, visibles **uniquement par les deux participants** (REST d'abord, SignalR plus tard).
- **Signalement** d'une annonce : 3 signalements de comptes **différents**, **e-mail confirmé** et **âgés d'au moins 7 jours** → suspension préventive (`Suspended`) en attendant un modérateur. Le propriétaire peut contester.
- Objets interdits (armes, médicaments, animaux, contenus illégaux…) : interdits par les conditions d'utilisation, modérés via les signalements.

## 2. Rôles
| Rôle | Droits |
|---|---|
| `User` | publier, rechercher, échanger, noter, signaler |
| `Moderator` | traiter signalements et litiges, suspendre/rétablir une annonce |
| `Admin` | gérer les catégories, les rôles, tout ce que fait un modérateur |

## 3. Stack technique
- **.NET 10 (LTS)**, **C#**, **ASP.NET Core Web API** (contrôleurs), `Nullable` activé, avertissements traités comme erreurs.
- **PostgreSQL 17** (Docker) + **EF Core 10** (Npgsql) + **migrations EF Core**.
- Authentification : **JWT** (accès 15 min) + **refresh token rotatif** (7 jours, haché en base, réutilisation = révocation de la famille). Mots de passe : `PasswordHasher<T>` d'ASP.NET Core Identity (PBKDF2, jamais de hash maison).
- Validation : **FluentValidation**. Erreurs : **ProblemDetails (RFC 9457)** via un seul `IExceptionHandler`.
- Limitation de débit : `Microsoft.AspNetCore.RateLimiting` (intégré).
- Images : **SkiaSharp** (MIT) pour vérifier, réencoder en WebP et supprimer les métadonnées.
- Documentation : OpenAPI intégré (`Microsoft.AspNetCore.OpenApi`) + Scalar UI **en Development uniquement**.
- Tests : **xUnit**, **NSubstitute**, **Shouldly**, **Testcontainers (PostgreSQL)** + `WebApplicationFactory`.
- Qualité : **SonarQube** (Docker, Community) + `dotnet-sonarscanner`, **coverlet** pour la couverture, `dotnet list package --vulnerable` dans la CI.
- Gestion centralisée des versions : `Directory.Packages.props`.

## 4. Architecture (Clean Architecture)
```text
src/
  TradX.Domain/          entités, énumérations, règles pures, exceptions métier (aucune dépendance)
  TradX.Application/     cas d'utilisation (services), DTO (records), validateurs, interfaces
  TradX.Infrastructure/  EF Core, PostgreSQL, JWT, hachage, e-mail, stockage des photos
  TradX.Api/             contrôleurs, middleware, configuration, sécurité HTTP
tests/
  TradX.UnitTests/         domaine + services (NSubstitute, sans base)
  TradX.IntegrationTests/  API complète contre un vrai PostgreSQL (Testcontainers)
```
**Règles**
- Les contrôleurs ne contiennent **aucune logique métier**.
- **Aucune entité** n'entre ni ne sort de l'API : uniquement des DTO validés.
- Injection **par constructeur** uniquement.
- Contrôle de **propriété** dans les services (risque n°1 : **IDOR**) en plus de `[Authorize]`.
- Identifiants exposés : **GUID v7**, jamais d'entiers séquentiels.
- Dates en **UTC** (`DateTimeOffset`), horloge injectée (`TimeProvider`) pour les tests.
- API versionnée : `/api/v1/...`.
- Code (classes, méthodes) en **anglais** ; **commentaires XML `///` en français** sur tout membre public.

## 5. Sécurité (OWASP Top 10 / ASVS)
- **Deny by default** : politique d'autorisation globale ; seuls les endpoints listés sont `[AllowAnonymous]`.
- Validation stricte côté serveur, tailles maximales, rejet des champs JSON inconnus.
- Requêtes paramétrées uniquement (LINQ / EF Core). Zéro concaténation SQL.
- En-têtes : HSTS (hors dev), `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, CSP stricte pour l'API.
- CORS : liste blanche des origines du front (jamais `*` en production).
- Limitation de débit : connexion, inscription, refresh, création d'échanges, messages, signalements, upload.
- Verrouillage du compte après 5 échecs de connexion (15 min). Message d'erreur identique que l'e-mail existe ou non.
- Aucun secret en dur : variables d'environnement / user-secrets ; `.env.example` sans valeurs réelles.
- **Journal d'audit** `audit_logs` append-only : connexions et échecs, changements de rôle, transitions d'échange, mouvements de crédits, décisions de modération.
- **Logs** : JSON en production, `CorrelationId` par requête ; jamais de mot de passe, token ou e-mail complet dans les logs.

## 6. Workflow
1. Plan court → 2. petites étapes → 3. `dotnet build` + `dotnet test` verts → 4. audit sécurité → 5. commit.
- Branches `feature/<nom>` fusionnées dans `main` quand tout est vert.
- **Conventional Commits** en français : `feat(listings): ajoute la recherche paginée`.
- Jamais de test supprimé, désactivé ou affaibli pour faire passer le build.
- Jamais de secret, `.env`, dump ou donnée réelle dans Git.
- Ne pas ajouter de ligne `Co-Authored-By` ni « Generated with Claude Code » dans les commits.
- Commandes destructives (`DROP`, `git reset --hard`, `push --force`, suppression de fichiers) : demander d'abord.

### Définition de « terminé »
Build sans avertissement, tests unitaires sur chaque service, tests d'intégration sur chaque endpoint,
**tests de sécurité** (sans authentification, mauvais rôle, ressource d'un autre utilisateur, entrées invalides),
validation en place, audit des actions sensibles, migration fournie si le schéma change, OpenAPI à jour, commit fait.

## 7. Commandes
```powershell
docker compose up -d                 # PostgreSQL (+ SonarQube avec --profile quality)
dotnet build
dotnet test
dotnet run --project src/TradX.Api
```

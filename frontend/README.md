# TradX · front web

Interface web de TradX (troc, banque de temps et dons à Bamako), branchée sur l'API ASP.NET Core du dépôt.

## Démarrer

```powershell
docker compose up -d                              # PostgreSQL (depuis la racine du dépôt)
dotnet run --project src/TradX.Api --launch-profile http   # API sur http://localhost:5191
cd frontend
npm install
npm run dev                                       # http://localhost:5173
```

En développement, Vite relaie `/api` vers l'API : le front et l'API partagent la même origine (pas de CORS à ouvrir).
Les e-mails de confirmation sont écrits dans `src/TradX.Api/.mail-outbox` : ouvre le lien qu'ils contiennent.

| Commande | Rôle |
|---|---|
| `npm run dev` | serveur de développement |
| `npm run typecheck` | vérification TypeScript (app + tests) |
| `npm run lint` | analyse statique (oxlint) |
| `npm test` | tests Vitest + Testing Library |
| `npm run build` | build de production dans `dist/` (avec la CSP) |
| `npm run api:types` | régénère `src/api/schema.d.ts` depuis l'OpenAPI de l'API lancée |

## Direction artistique « Bogolan »

Inspirée du tissu malien peint à la boue : chaque échange est un **fil** qui relie deux personnes.

- **Palette** (`src/styles/tokens.css`) : encre `#2A1F17`, coton `#F3ECDF`, kaolin `#E6DAC4`, ocre `#C4842F` (actions uniquement), rouille `#8E3F23`, karité `#5F6A46`. Contrastes WCAG calculés et notés dans le fichier.
- **Typographies** auto-hébergées (aucune requête vers Google) : *Fraunces* (axe SOFT) pour les titres, *Schibsted Grotesk* pour l'interface.
- **Le fil signature** (`pages/home/SignatureThread.tsx`) : part du nœud du héros, se dessine au défilement (DrawSVG) et passe par chaque étape du récit ; un nœud le suit (MotionPath). Le même fil indique l'état d'un échange (`ExchangeKnot`) : séparés, croisés, noués, ou détendus.
- **Les mailles** (`components/TimeStrip.tsx`) : le solde de temps dessiné comme une bande tissée, une maille = 30 min.
- **Les tissus** (`components/Motif.tsx`) : 12 motifs géométriques, un par catégorie ; une annonce sans photo devient un échantillon de tissu (clair pour une offre, sombre pour une demande).
- **Mouvement** : entrées de 700 ms en `expo.out`, décalage de 90 à 110 ms, uniquement transform et opacité ; défilement amorti par Lenis synchronisé avec ScrollTrigger. Tout est désactivé si `prefers-reduced-motion`.

## Architecture

```text
src/
  api/          client HTTP, types (générés depuis l'OpenAPI + énumérations en texte), appels par domaine
  auth/         session (contexte React) et protection des pages réservées aux membres
  components/   éléments partagés (en-tête, cartes, champs, motifs, mailles)
  lib/          logique pure et testée (format, règles d'échange, redirection sûre, motifs)
  motion/       GSAP (plugins enregistrés une fois), Lenis, révélations au défilement
  pages/        une page par route, chargée à la demande (sauf l'accueil)
  styles/       jetons de design, base, styles partagés
```

Les règles métier affichées (actions possibles sur un échange, façons d'échanger, limites de la banque de temps) reprennent
exactement celles du domaine C# ; l'API reste l'autorité et refuse toute action interdite.

## Sécurité

- **Jetons** : le JWT d'accès reste en mémoire ; le refresh token est dans `sessionStorage` (survit au rechargement, pas à
  la fermeture de l'onglet, jamais partagé entre onglets). Évolution recommandée : cookie `HttpOnly` posé par l'API.
- **Renouvellement unique** : plusieurs 401 simultanés ne déclenchent qu'un seul refresh (l'API révoque toute la famille
  de jetons si un refresh token est réutilisé). Couvert par un test.
- **XSS** : aucun HTML injecté (`dangerouslySetInnerHTML` absent), le texte des membres est toujours échappé par React.
- **CSP** stricte ajoutée au build (`default-src 'self'`, pas de script externe) ; à doubler par un en-tête HTTP en production.
- **Redirection ouverte** : la page de retour après connexion n'accepte que des chemins internes (`lib/safeRedirect.ts`).
- **Images protégées** : les photos d'un brouillon sont chargées avec le jeton, uniquement depuis `/api/v1/`.
- `npm audit` dans la CI.

## Accessibilité

Navigation au clavier, focus visibles (indigo sur fond clair, ocre sur fond sombre), lien d'évitement, champs liés à leurs
étiquettes et erreurs (`aria-describedby`), boîtes de dialogue natives, cibles tactiles de 44 px minimum, animations réduites
respectées. Les informations portées par une couleur ou un dessin sont toujours répétées en texte.

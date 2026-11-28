# Diagramme de classes de TradX

Ce diagramme montre les classes du **domaine** (projet `TradX.Domain`) : les objets métier et leurs règles.
Chaque classe devient une table dans PostgreSQL (voir `AppDbContext.cs`).

Les liens entre classes sont des **identifiants** (`OwnerId`, `CategoryId`…), pas des objets imbriqués.
Chaque module reste ainsi indépendant, et l'on charge seulement ce dont on a besoin.

```mermaid
classDiagram
    direction LR

    class Entity {
        <<abstract>>
        +Guid Id
    }

    class User {
        +string Email
        +string DisplayName
        +string PasswordHash
        +UserRole Role
        +bool EmailConfirmed
        +string City
        +string? Neighborhood
        +int FailedLoginCount
        +DateTimeOffset? LockoutEndsAt
        +int TimeBalanceMinutes
        +Register()$ User
        +RegisterFailedLogin(now)
        +UpdateProfile(...)
        +ChangeRole(role)
    }

    class RefreshToken {
        +Guid UserId
        +string TokenHash
        +Guid FamilyId
        +DateTimeOffset ExpiresAt
        +DateTimeOffset? RevokedAt
        +Issue()$ RefreshToken
        +IsActive(now) bool
        +Revoke(now)
    }

    class Category {
        +string Name
        +bool IsActive
        +Create(name)$ Category
    }

    class Listing {
        +Guid OwnerId
        +Guid CategoryId
        +ListingDirection Direction
        +ListingType Type
        +string Title
        +string Description
        +string City
        +bool IsDonation
        +ListingStatus Status
        +Create()$ Listing
        +Publish(now)
        +Reserve(now)
        +Release(now)
        +MarkExchanged(now)
        +Suspend(now)
        +AddPhoto(...) ListingPhoto
    }

    class ListingPhoto {
        +Guid ListingId
        +string StorageKey
        +int Width
        +int Height
        +int Position
    }

    class Exchange {
        +ExchangeKind Kind
        +ExchangeStatus Status
        +Guid RequesterId
        +Guid OwnerId
        +Guid TargetListingId
        +Guid? OfferedListingId
        +int? DurationMinutes
        +DateTimeOffset ExpiresAt
        +Propose()$ Exchange
        +Accept(actorId, now)
        +Decline(actorId, now)
        +Cancel(actorId, now)
        +ConfirmDone(actorId, now) bool
        +OpenDispute(actorId, reason, now)
        -MoveTo(target)
    }

    class TimeTransaction {
        +Guid ExchangeId
        +Guid FromUserId
        +Guid ToUserId
        +int Minutes
    }

    class TimeBank {
        <<static>>
        +OverdraftLimitMinutes = -300
        +MaxBalanceMinutes = 6000
        +SlotMinutes = 30
        +EnsureValidDuration(minutes)$
        +EnsureTransferAllowed(...)$
    }

    class Message {
        +Guid ExchangeId
        +Guid SenderId
        +Guid RecipientId
        +string Content
        +DateTimeOffset? ReadAt
        +Send()$ Message
        +MarkRead(readerId, now)
    }

    class Review {
        +Guid ExchangeId
        +Guid AuthorId
        +Guid TargetUserId
        +int Rating
        +string? Comment
        +Create()$ Review
    }

    class Report {
        +Guid ListingId
        +Guid ReporterId
        +ReportReason Reason
        +ReportStatus Status
        +Create()$ Report
        +Resolve(moderatorId, upheld, now)
        +ShouldSuspend(count)$ bool
    }

    class AuditLog {
        +DateTimeOffset OccurredAt
        +Guid? ActorId
        +string Action
        +string ResourceType
        +string? IpAddress
    }

    class UserRole {
        <<enumeration>>
        User
        Moderator
        Admin
    }

    class ListingStatus {
        <<enumeration>>
        Draft
        Published
        Reserved
        Exchanged
        Archived
        Suspended
    }

    class ExchangeKind {
        <<enumeration>>
        Barter
        TimeBank
        Donation
    }

    class ExchangeStatus {
        <<enumeration>>
        Proposed
        Accepted
        Completed
        Declined
        Cancelled
        Expired
        Disputed
    }

    Entity <|-- User
    Entity <|-- RefreshToken
    Entity <|-- Category
    Entity <|-- Listing
    Entity <|-- ListingPhoto
    Entity <|-- Exchange
    Entity <|-- TimeTransaction
    Entity <|-- Message
    Entity <|-- Review
    Entity <|-- Report
    Entity <|-- AuditLog

    User "1" --> "*" RefreshToken : sessions
    User "1" --> "*" Listing : publie
    Category "1" --> "*" Listing : classe
    Listing "1" *-- "0..5" ListingPhoto : photos
    User "1" --> "*" Exchange : propose / reçoit
    Exchange "*" --> "1" Listing : annonce visée
    Exchange "*" --> "0..1" Listing : annonce offerte (troc)
    Exchange "1" --> "0..1" TimeTransaction : paie en heures
    Exchange "1" --> "*" Message : discussion
    Exchange "1" --> "0..2" Review : avis
    Listing "1" --> "*" Report : signalements
    TimeBank ..> User : vérifie le solde
    User --> UserRole
    Listing --> ListingStatus
    Exchange --> ExchangeKind
    Exchange --> ExchangeStatus
```

## Lecture rapide

| Classe | Rôle |
|---|---|
| `Entity` | Classe mère : donne à chaque objet un identifiant unique (`Guid`). |
| `User` | Un membre : e-mail, mot de passe **haché**, rôle, solde d'heures. |
| `RefreshToken` | Une session de connexion, stockée hachée et révocable. |
| `Category` / `Listing` / `ListingPhoto` | Une annonce (« je propose » ou « je cherche »), sa catégorie et ses photos. |
| `Exchange` | Un échange entre deux membres : troc, banque de temps ou don. Il suit une **machine à états**. |
| `TimeTransaction` / `TimeBank` | Les mouvements d'heures (1 h de service = 1 h de crédit) et leurs limites. |
| `Message` / `Review` / `Report` | La discussion d'un échange, les avis et les signalements. |
| `AuditLog` | Le journal des actions sensibles (qui, quoi, quand, depuis quelle IP). |

## Cycle de vie d'un échange

```mermaid
stateDiagram-v2
    [*] --> Proposed
    Proposed --> Accepted : le propriétaire accepte
    Proposed --> Declined : le propriétaire refuse
    Proposed --> Cancelled : le demandeur retire
    Proposed --> Expired : 7 jours sans réponse
    Accepted --> Completed : les deux confirment
    Accepted --> Cancelled
    Accepted --> Disputed : litige
    Disputed --> Completed : décision du modérateur
    Disputed --> Cancelled : décision du modérateur
    Completed --> [*]
    Declined --> [*]
    Cancelled --> [*]
    Expired --> [*]
```

Ces transitions sont définies à un seul endroit : `AllowedTransitions` dans
`src/TradX.Domain/Exchanges/Exchange.cs` (ligne 64). Toute autre transition est refusée par `MoveTo` (ligne 322).

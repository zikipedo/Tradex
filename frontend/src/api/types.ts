import type { components } from './schema'

/**
 * Types de l'API TradX.
 * Les formes viennent du document OpenAPI généré (`schema.d.ts`, via `npm run api:types`).
 * Les énumérations sont redéfinies ici en unions de chaînes : l'API les sérialise en texte
 * (JsonStringEnumConverter) alors que le document OpenAPI les annonce comme des nombres.
 */
type Schemas = components['schemas']

export type ListingDirection = 'Offer' | 'Request'
export type ListingType = 'Good' | 'Service'
export type ItemCondition = 'New' | 'Good' | 'Used'
export type ListingStatus = 'Draft' | 'Published' | 'Reserved' | 'Exchanged' | 'Archived' | 'Suspended'
export type ExchangeKind = 'Barter' | 'TimeBank' | 'Donation'
export type ExchangeStatus = 'Proposed' | 'Accepted' | 'Completed' | 'Declined' | 'Cancelled' | 'Expired' | 'Disputed'
export type ExchangeRoleFilter = 'All' | 'Sent' | 'Received'
export type ReportReason = 'NoLongerAvailable' | 'Scam' | 'Inappropriate' | 'Prohibited' | 'Other'
export type UserRole = 'User' | 'Moderator' | 'Admin'

/** Remplace les champs d'énumération et d'entier du schéma généré par leurs vrais types. */
type Fix<T, Overrides> = Omit<T, keyof Overrides> & Overrides

/** Session renvoyée par la connexion et le renouvellement : le refresh token est dans un cookie HttpOnly. */
export type Session = Schemas['SessionResponse']
export type Category = Schemas['CategoryResponse']
export type ListingOwner = Schemas['ListingOwner']
export type ListingPhoto = Fix<Schemas['ListingPhotoResponse'], { width: number; height: number }>

export type ListingSummary = Fix<
  Schemas['ListingSummary'],
  { direction: ListingDirection; type: ListingType; status: ListingStatus }
>

export type Listing = Fix<
  Schemas['ListingResponse'],
  {
    direction: ListingDirection
    type: ListingType
    status: ListingStatus
    condition: ItemCondition | null
    photos: ListingPhoto[]
  }
>

export type UpsertListing = Fix<
  Schemas['UpsertListingRequest'],
  { direction: ListingDirection; type: ListingType; condition: ItemCondition | null }
>

export type ExchangeListing = Fix<Schemas['ExchangeListing'], { type: ListingType; status: ListingStatus }>
export type ExchangeParty = Schemas['ExchangeParty']

export type Exchange = Fix<
  Schemas['ExchangeResponse'],
  {
    kind: ExchangeKind
    status: ExchangeStatus
    targetListing: ExchangeListing
    offeredListing: ExchangeListing | null
    durationMinutes: number | null
  }
>

export type ProposeExchange = Fix<
  Schemas['ProposeExchangeRequest'],
  { kind: ExchangeKind; durationMinutes: number | null }
>

export type MyProfile = Fix<Schemas['MyProfileResponse'], { role: UserRole; timeBalanceMinutes: number }>
export type PublicProfile = Fix<
  Schemas['PublicProfileResponse'],
  { averageRating: number | null; reviewCount: number; completedExchanges: number }
>
export type UpdateProfile = Schemas['UpdateProfileRequest']
export type RegisterRequest = Schemas['RegisterRequest']

export type Message = Schemas['MessageResponse']
export type MessagePage = Fix<Schemas['MessagePage'], { items: Message[] }>
export type Review = Fix<Schemas['ReviewResponse'], { rating: number }>

export type TimeTransaction = Fix<Schemas['TimeTransactionResponse'], { minutes: number }>
export type TimeAccount = Fix<
  Schemas['TimeAccountResponse'],
  {
    balanceMinutes: number
    overdraftLimitMinutes: number
    maxBalanceMinutes: number
    transactions: Paged<TimeTransaction>
  }
>

/** Page de résultats : toutes les listes de l'API sont paginées. */
export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Filtres de recherche des annonces (`GET /api/v1/listings`). */
export interface ListingSearch {
  q?: string
  categoryId?: string
  type?: ListingType
  direction?: ListingDirection
  city?: string
  neighborhood?: string
  donationOnly?: boolean
  page?: number
  pageSize?: number
}

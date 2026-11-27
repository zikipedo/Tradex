import { api } from './client'
import type {
  Category,
  Exchange,
  ExchangeRoleFilter,
  ExchangeStatus,
  Listing,
  ListingPhoto,
  ListingSearch,
  ListingSummary,
  Message,
  MessagePage,
  MyProfile,
  Paged,
  ProposeExchange,
  PublicProfile,
  RegisterRequest,
  ReportReason,
  Review,
  TimeAccount,
  UpdateProfile,
  Session,
  UpsertListing,
} from './types'

/** Appels de l'API TradX, regroupés par domaine. Un appel = une route documentée dans le README de l'API. */

export const authApi = {
  register: (body: RegisterRequest) => api<{ message: string }>('/auth/register', { method: 'POST', body }),
  confirmEmail: (token: string) => api<void>('/auth/confirm-email', { method: 'POST', body: { token } }),
  resendConfirmation: (email: string) => api<{ message: string }>('/auth/resend-confirmation', { method: 'POST', body: { email } }),
  login: (email: string, password: string) => api<Session>('/auth/login', { method: 'POST', body: { email, password } }),
  forgotPassword: (email: string) => api<{ message: string }>('/auth/forgot-password', { method: 'POST', body: { email } }),
  resetPassword: (token: string, newPassword: string) =>
    api<void>('/auth/reset-password', { method: 'POST', body: { token, newPassword } }),
}

export const usersApi = {
  me: () => api<MyProfile>('/users/me'),
  updateMe: (body: UpdateProfile) => api<MyProfile>('/users/me', { method: 'PUT', body }),
  profile: (id: string) => api<PublicProfile>(`/users/${id}`),
  reviews: (id: string, page = 1) => api<Paged<Review>>(`/users/${id}/reviews`, { query: { page, pageSize: 10 } }),
}

export const listingsApi = {
  categories: () => api<Category[]>('/categories'),
  search: (filters: ListingSearch, signal?: AbortSignal) =>
    api<Paged<ListingSummary>>('/listings', { query: { ...filters }, signal }),
  mine: (page = 1) => api<Paged<ListingSummary>>('/listings/mine', { query: { page, pageSize: 24 } }),
  get: (id: string) => api<Listing>(`/listings/${id}`),
  create: (body: UpsertListing) => api<Listing>('/listings', { method: 'POST', body }),
  update: (id: string, body: UpsertListing) => api<Listing>(`/listings/${id}`, { method: 'PUT', body }),
  remove: (id: string) => api<void>(`/listings/${id}`, { method: 'DELETE' }),
  publish: (id: string) => api<Listing>(`/listings/${id}/publish`, { method: 'POST' }),
  archive: (id: string) => api<Listing>(`/listings/${id}/archive`, { method: 'POST' }),
  uploadPhoto: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api<ListingPhoto>(`/listings/${id}/photos`, { method: 'POST', body: form })
  },
  deletePhoto: (id: string, photoId: string) => api<void>(`/listings/${id}/photos/${photoId}`, { method: 'DELETE' }),
  report: (id: string, reason: ReportReason, details: string | null) =>
    api<{ id: string; message: string }>(`/listings/${id}/reports`, { method: 'POST', body: { reason, details } }),
}

export const exchangesApi = {
  propose: (body: ProposeExchange) => api<Exchange>('/exchanges', { method: 'POST', body }),
  list: (role: ExchangeRoleFilter = 'All', status?: ExchangeStatus, page = 1) =>
    api<Paged<Exchange>>('/exchanges', { query: { role, status, page, pageSize: 20 } }),
  get: (id: string) => api<Exchange>(`/exchanges/${id}`),
  accept: (id: string) => api<Exchange>(`/exchanges/${id}/accept`, { method: 'POST' }),
  decline: (id: string) => api<Exchange>(`/exchanges/${id}/decline`, { method: 'POST' }),
  cancel: (id: string) => api<Exchange>(`/exchanges/${id}/cancel`, { method: 'POST' }),
  confirm: (id: string) => api<Exchange>(`/exchanges/${id}/confirm`, { method: 'POST' }),
  dispute: (id: string, reason: string) => api<Exchange>(`/exchanges/${id}/dispute`, { method: 'POST', body: { reason } }),
  review: (id: string, rating: number, comment: string | null) =>
    api<Review>(`/exchanges/${id}/review`, { method: 'POST', body: { rating, comment } }),
  timeAccount: (page = 1) => api<TimeAccount>('/time-account', { query: { page, pageSize: 20 } }),
}

export const messagesApi = {
  list: (exchangeId: string, before?: string) =>
    api<MessagePage>(`/exchanges/${exchangeId}/messages`, { query: { before, limit: 30 } }),
  send: (exchangeId: string, content: string) =>
    api<Message>(`/exchanges/${exchangeId}/messages`, { method: 'POST', body: { content } }),
  markRead: (exchangeId: string) => api<number>(`/exchanges/${exchangeId}/messages/read`, { method: 'POST' }),
}

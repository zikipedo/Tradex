import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { messagesApi } from '../../api/endpoints'
import type { Message } from '../../api/types'
import { formatRelative, formatTime } from '../../lib/format'
import { errorMessage } from '../../lib/useAsync'

const POLL_MS = 15_000
const MAX_LENGTH = 2000

/**
 * Conversation privée d'un échange (visible par les deux parties seulement, contrôlé par l'API).
 * Les messages sont affichés comme du texte brut : React échappe tout, aucun HTML n'est interprété.
 * Nouveaux messages récupérés toutes les 15 s tant que l'onglet est visible (en attendant le temps réel).
 */
export function Conversation({ exchangeId, myId, open }: { exchangeId: string; myId: string; open: boolean }) {
  const [messages, setMessages] = useState<Message[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const list = useRef<HTMLOListElement>(null)

  /** Récupère la page la plus récente et fusionne sans doublon (ordre chronologique à l'écran). */
  const refresh = useCallback(async () => {
    const page = await messagesApi.list(exchangeId)
    setMessages((current) => {
      const byId = new Map(current.map((m) => [m.id, m]))
      for (const m of page.items) byId.set(m.id, m)
      return [...byId.values()].sort((a, b) => a.sentAt.localeCompare(b.sentAt) || a.id.localeCompare(b.id))
    })
    setCursor((c) => c ?? page.nextCursor)
    if (page.items.some((m) => m.senderId !== myId && !m.readAt)) void messagesApi.markRead(exchangeId)
  }, [exchangeId, myId])

  useEffect(() => {
    refresh().catch((err: unknown) => setError(errorMessage(err)))
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') refresh().catch(() => undefined)
    }, POLL_MS)
    return () => clearInterval(timer)
  }, [refresh])

  // Garde le dernier message visible quand la liste grandit.
  useEffect(() => {
    list.current?.lastElementChild?.scrollIntoView({ block: 'nearest' })
  }, [messages.length])

  const loadOlder = async () => {
    if (!cursor) return
    const page = await messagesApi.list(exchangeId, cursor)
    setMessages((current) => [...[...page.items].reverse(), ...current])
    setCursor(page.nextCursor)
  }

  const onSend = async (event: FormEvent) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content) return
    setSending(true)
    setError(null)
    try {
      const sent = await messagesApi.send(exchangeId, content)
      setMessages((current) => [...current, sent])
      setDraft('')
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <section className="chat panel" aria-labelledby="chat-title">
      <h2 id="chat-title" className="h3">
        Conversation
      </h2>
      <p className="field-hint">Visible par vous deux seulement. Pour se retrouver, préférez un lieu public et passant.</p>
      {cursor && (
        <button type="button" className="btn btn--ghost btn--small" onClick={loadOlder}>
          Messages plus anciens
        </button>
      )}
      {messages.length === 0 ? (
        <p className="muted chat__empty">Aucun message. Dis bonjour et proposez un lieu et une heure.</p>
      ) : (
        <ol ref={list} className="chat__list" aria-live="polite">
          {messages.map((m) => {
            const mine = m.senderId === myId
            return (
              <li key={m.id} className={`bubble${mine ? ' bubble--mine' : ''}`}>
                <p>{m.content}</p>
                <span className="bubble__meta">
                  <time dateTime={m.sentAt} title={formatRelative(m.sentAt)}>
                    {formatTime(m.sentAt)}
                  </time>
                  {mine && <span>{m.readAt ? ' · lu' : ' · envoyé'}</span>}
                </span>
              </li>
            )
          })}
        </ol>
      )}
      {error && <p className="alert">{error}</p>}
      {open ? (
        <form className="chat__form" onSubmit={onSend}>
          <label htmlFor="message" className="sr-only">
            Ton message
          </label>
          <textarea
            id="message"
            rows={2}
            value={draft}
            maxLength={MAX_LENGTH}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                e.currentTarget.form?.requestSubmit()
              }
            }}
            placeholder="Écris ton message…"
          />
          <button type="submit" className="btn btn--action" disabled={sending || !draft.trim()}>
            Envoyer
          </button>
        </form>
      ) : (
        <p className="field-hint">Cette conversation est fermée.</p>
      )}
    </section>
  )
}

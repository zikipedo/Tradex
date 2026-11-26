import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/client'

/** Message lisible pour n'importe quelle erreur (API, réseau, inattendue). */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) return 'Connexion impossible. Vérifie ton réseau et réessaie.'
  return 'Une erreur inattendue est survenue.'
}

/**
 * Charge une ressource au montage (et quand `deps` change), avec annulation si le composant disparaît.
 * Renvoie les données, l'erreur éventuelle et une fonction pour recharger.
 */
export function useResource<T>(load: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let active = true
    setLoading(true)
    setError(null)
    load()
      .then((value) => active && setData(value))
      .catch((err: unknown) => active && setError(errorMessage(err)))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])
  return { data, setData, error, loading, reload }
}

/**
 * Exécute une action (envoi de formulaire, bouton) en gérant l'état « en cours » et les erreurs de l'API,
 * y compris les erreurs par champ renvoyées par la validation du serveur.
 */
export function useAction<Args extends unknown[], R>(action: (...args: Args) => Promise<R>) {
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  const run = useCallback(
    async (...args: Args): Promise<R | undefined> => {
      setPending(true)
      setError(null)
      setFieldErrors({})
      try {
        return await action(...args)
      } catch (err) {
        if (!mounted.current) return undefined
        if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
          const flat: Record<string, string> = {}
          for (const [key, messages] of Object.entries(err.fieldErrors)) flat[key.toLowerCase()] = messages[0] ?? ''
          setFieldErrors(flat)
          setError('Certains champs sont à corriger.')
        } else {
          setError(errorMessage(err))
        }
        return undefined
      } finally {
        if (mounted.current) setPending(false)
      }
    },
    [action],
  )

  return { run, pending, error, fieldErrors, setError }
}

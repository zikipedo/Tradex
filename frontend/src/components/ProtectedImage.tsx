import { useEffect, useState, type ImgHTMLAttributes } from 'react'
import { fetchProtectedImage } from '../api/client'

/**
 * Image chargée avec le jeton de session (photos d'un brouillon), puis affichée via une URL locale `blob:`.
 * L'URL locale est libérée quand l'image change ou disparaît (pas de fuite mémoire).
 */
export function ProtectedImage({ src, alt, ...img }: ImgHTMLAttributes<HTMLImageElement> & { src: string; alt: string }) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    let created: string | null = null
    fetchProtectedImage(src, controller.signal)
      .then((blob) => {
        created = URL.createObjectURL(blob)
        setObjectUrl(created)
      })
      .catch(() => setObjectUrl(null))
    return () => {
      controller.abort()
      if (created) URL.revokeObjectURL(created)
    }
  }, [src])

  return objectUrl ? <img {...img} src={objectUrl} alt={alt} /> : <span className="protected-image__placeholder" aria-label={alt} role="img" />
}

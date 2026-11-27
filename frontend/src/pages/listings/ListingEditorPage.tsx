import { useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { listingsApi } from '../../api/endpoints'
import type { ItemCondition, Listing, ListingDirection, ListingType, UpsertListing } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { Segmented, SelectField, TextArea, TextField } from '../../components/Field'
import { ErrorNote, Loader } from '../../components/Loader'
import { ProtectedImage } from '../../components/ProtectedImage'
import { conditionLabel } from '../../lib/labels'
import { selectionNotice, uploadFailure } from '../../lib/photoMessages'
import { errorMessage, useAction, useResource } from '../../lib/useAsync'
import './ListingEditorPage.css'

const MAX_PHOTOS = 5
const MAX_PHOTO_BYTES = 5 * 1024 * 1024
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']

const EMPTY: UpsertListing = {
  direction: 'Offer',
  type: 'Good',
  title: '',
  description: '',
  categoryId: '',
  condition: 'Good',
  city: 'Bamako',
  neighborhood: null,
  availability: null,
  wantedInReturn: null,
  isDonation: false,
}

function toForm(listing: Listing): UpsertListing {
  return {
    direction: listing.direction,
    type: listing.type,
    title: listing.title,
    description: listing.description,
    categoryId: listing.category.id,
    condition: listing.condition,
    city: listing.city,
    neighborhood: listing.neighborhood,
    availability: listing.availability,
    wantedInReturn: listing.wantedInReturn,
    isDonation: listing.isDonation,
  }
}

/**
 * Création et modification d'une annonce. Aucun champ prix : c'est voulu.
 * Les photos s'ajoutent une fois l'annonce enregistrée (elle a alors un identifiant).
 */
export default function ListingEditorPage() {
  const { id } = useParams()
  const existing = useResource(() => (id ? listingsApi.get(id) : Promise.resolve(null)), [id])

  if (id && existing.loading) return <Loader label="Chargement de l’annonce" />
  if (id && (existing.error || !existing.data))
    return (
      <div className="wrap page">
        <ErrorNote message={existing.error ?? 'Annonce introuvable.'} onRetry={existing.reload} />
      </div>
    )
  // La clé recrée le formulaire si l'on passe d'une annonce à une autre.
  return <Editor key={id ?? 'nouvelle'} initial={existing.data} />
}

function Editor({ initial }: { initial: Listing | null }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const { me } = useAuth()
  const [form, setForm] = useState<UpsertListing>(() =>
    initial ? toForm(initial) : { ...EMPTY, city: me?.city ?? 'Bamako', neighborhood: me?.neighborhood ?? null },
  )
  const [listing, setListing] = useState<Listing | null>(initial)
  const categories = useResource(() => listingsApi.categories(), [])

  const save = useAction(async (body: UpsertListing) => (listing ? listingsApi.update(listing.id, body) : listingsApi.create(body)))
  const publish = useAction(listingsApi.publish)
  const set = <K extends keyof UpsertListing>(key: K, value: UpsertListing[K]) => setForm((f) => ({ ...f, [key]: value }))
  const text = (key: 'title' | 'description' | 'city') => (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => set(key, e.target.value)
  const optional = (key: 'neighborhood' | 'availability' | 'wantedInReturn') => (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    set(key, e.target.value === '' ? null : e.target.value)

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const body: UpsertListing = {
      ...form,
      title: form.title.trim(),
      description: form.description.trim(),
      city: form.city.trim(),
      // Un service n'a pas d'état ; un don est forcément une offre.
      condition: form.type === 'Good' ? form.condition : null,
      isDonation: form.direction === 'Offer' && form.isDonation,
      wantedInReturn: form.isDonation ? null : form.wantedInReturn,
    }
    const saved = await save.run(body)
    if (saved) {
      setListing(saved)
      if (!id) navigate(`/mes-annonces/${saved.id}/modifier`, { replace: true })
    }
  }

  const fe = save.fieldErrors
  const titleLength = form.title.trim().length
  const descriptionLength = form.description.trim().length

  return (
    <div className="editor wrap page">
      <header className="page-head">
        <p className="label">{listing ? 'Modifier l’annonce' : 'Nouvelle annonce'}</p>
        <h1 className="h1">{listing ? listing.title : <>Qu’as-tu <em>à échanger ?</em></>}</h1>
      </header>

      <div className="editor__grid">
        <form className="editor__form" onSubmit={onSubmit} noValidate>
          <section className="editor__section">
            <h2 className="h3">
              <span className="editor__step">1</span> L’essentiel
            </h2>
            <Segmented<ListingDirection>
              legend="Tu…"
              name="direction"
              value={form.direction}
              onChange={(v) => set('direction', v)}
              options={[
                { value: 'Offer', label: 'Je propose', hint: 'J’ai quelque chose à offrir' },
                { value: 'Request', label: 'Je cherche', hint: 'J’ai besoin de quelque chose' },
              ]}
            />
            <Segmented<ListingType>
              legend="C’est…"
              name="type"
              value={form.type}
              onChange={(v) => set('type', v)}
              options={[
                { value: 'Good', label: 'Un objet' },
                { value: 'Service', label: 'Un service', hint: 'Peut s’échanger en heures' },
              ]}
            />
            <TextField
              label="Titre"
              value={form.title}
              onChange={text('title')}
              maxLength={100}
              required
              placeholder={form.type === 'Good' ? 'Machine à coudre Singer, bon état' : 'Cours de français, niveau collège'}
              hint={`${titleLength} / 100 · au moins 5 caractères`}
              error={fe.title}
            />
            <SelectField label="Catégorie" value={form.categoryId} onChange={(e) => set('categoryId', e.target.value)} required error={fe.categoryid}>
              <option value="">Choisir</option>
              {(categories.data ?? [])
                .filter((c) => c.isActive)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </SelectField>
          </section>

          <section className="editor__section">
            <h2 className="h3">
              <span className="editor__step">2</span> Les détails
            </h2>
            <TextArea
              label="Description"
              value={form.description}
              onChange={text('description')}
              maxLength={3000}
              rows={6}
              required
              placeholder="Décris honnêtement : ce que c’est, son état, ce qu’il faut savoir."
              hint={`${descriptionLength} / 3000 · au moins 20 caractères`}
              error={fe.description}
            />
            {form.type === 'Good' && (
              <Segmented<ItemCondition>
                legend="État"
                name="condition"
                value={form.condition ?? 'Good'}
                onChange={(v) => set('condition', v)}
                options={(Object.keys(conditionLabel) as ItemCondition[]).map((c) => ({ value: c, label: conditionLabel[c] }))}
              />
            )}
            <TextField label="Disponibilités" value={form.availability ?? ''} onChange={optional('availability')} placeholder="Le samedi matin, après 18 h en semaine" error={fe.availability} />
          </section>

          <section className="editor__section">
            <h2 className="h3">
              <span className="editor__step">3</span> Où et en échange de quoi
            </h2>
            <div className="form-grid">
              <TextField label="Ville" value={form.city} onChange={text('city')} required error={fe.city} />
              <TextField label="Quartier" value={form.neighborhood ?? ''} onChange={optional('neighborhood')} placeholder="Magnambougou" error={fe.neighborhood} />
            </div>
            {form.direction === 'Offer' && (
              <label className="check">
                <input type="checkbox" checked={form.isDonation} onChange={(e) => set('isDonation', e.target.checked)} />
                <span>
                  <strong>C’est un don.</strong> Je ne demande rien en retour.
                </span>
              </label>
            )}
            {!form.isDonation && (
              <TextArea
                label="Ce qui t’intéresserait en retour"
                value={form.wantedInReturn ?? ''}
                onChange={optional('wantedInReturn')}
                rows={3}
                placeholder="Des cours d’anglais pour mon fils, un ventilateur, ou des heures de la banque de temps."
                error={fe.wantedinreturn}
              />
            )}
          </section>

          {save.error && (
            <p className="alert" role="alert">
              {save.error}
            </p>
          )}
          <div className="form-actions editor__actions">
            <button type="submit" className="btn btn--action" disabled={save.pending || titleLength < 5 || descriptionLength < 20 || !form.categoryId}>
              {save.pending ? 'Enregistrement…' : listing ? 'Enregistrer les modifications' : 'Enregistrer le brouillon'}
            </button>
            {listing && (
              <Link to={`/annonces/${listing.id}`} className="btn btn--ghost">
                Voir l’annonce
              </Link>
            )}
          </div>
        </form>

        <aside className="editor__side">
          {listing ? (
            <>
              <PhotoManager listing={listing} onChange={setListing} />
              {listing.status === 'Draft' && (
                <div className="panel panel--kaolin">
                  <h2 className="h3">Prête ?</h2>
                  <p className="muted">Une fois publiée, ton annonce apparaît dans les recherches. Ton e-mail doit être confirmé.</p>
                  {publish.error && <p className="alert">{publish.error}</p>}
                  <button
                    type="button"
                    className="btn btn--action"
                    disabled={publish.pending}
                    onClick={async () => {
                      const published = await publish.run(listing.id)
                      if (published) navigate(`/annonces/${published.id}`)
                    }}
                  >
                    Publier l’annonce
                  </button>
                </div>
              )}
            </>
          ) : (
            <div className="panel panel--kaolin">
              <h2 className="h3">Et les photos ?</h2>
              <p className="muted">Enregistre d’abord le brouillon, puis ajoute jusqu’à {MAX_PHOTOS} photos. Elles sont réduites et nettoyées (la position GPS est supprimée).</p>
              <p className="muted">Rappel : pas de prix. Une annonce qui demande de l’argent sera signalée.</p>
            </div>
          )}
        </aside>
      </div>
    </div>
  )
}

/** Ajout et suppression des photos d'une annonce enregistrée. */
function PhotoManager({ listing, onChange }: { listing: Listing; onChange: (l: Listing) => void }) {
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const remaining = MAX_PHOTOS - listing.photos.length

  const onFiles = async (event: ChangeEvent<HTMLInputElement>) => {
    const chosen = Array.from(event.target.files ?? [])
    const files = chosen.slice(0, remaining)
    event.target.value = ''
    setError(null)
    // On dit clairement quand des photos sont laissées de côté (jamais d'abandon silencieux).
    setNotice(selectionNotice(chosen.length, remaining, MAX_PHOTOS))
    // Contrôles côté navigateur pour éviter un envoi inutile ; l'API revérifie le vrai format du fichier.
    const invalid = files.find((f) => !ACCEPTED_TYPES.includes(f.type) || f.size > MAX_PHOTO_BYTES)
    if (invalid) {
      setError(`« ${invalid.name} » n’est pas accepté : JPEG, PNG ou WebP de 5 Mo maximum.`)
      return
    }
    setBusy(true)
    let current: File | undefined
    try {
      for (const file of files) {
        current = file
        await listingsApi.uploadPhoto(listing.id, file)
      }
    } catch (err) {
      setError(uploadFailure(current!.name, errorMessage(err), files.length - files.indexOf(current!) - 1))
    } finally {
      // Même si un envoi échoue en cours de route, les précédents sont enregistrés : on recharge la liste.
      try {
        onChange(await listingsApi.get(listing.id))
      } catch {
        // L'erreur d'envoi déjà affichée suffit.
      }
      setBusy(false)
    }
  }

  const onDelete = async (photoId: string) => {
    setBusy(true)
    setError(null)
    try {
      await listingsApi.deletePhoto(listing.id, photoId)
      onChange({ ...listing, photos: listing.photos.filter((p) => p.id !== photoId) })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="panel photos">
      <h2 className="h3">Photos</h2>
      <p className="field-hint">
        {listing.photos.length} / {MAX_PHOTOS} · la première sert de couverture
      </p>
      <ul className="photos__grid">
        {listing.photos.map((photo, i) => (
          <li key={photo.id}>
            <ProtectedImage src={photo.url} alt={`Photo ${i + 1}`} />
            <button type="button" className="photos__remove" onClick={() => onDelete(photo.id)} disabled={busy} aria-label={`Supprimer la photo ${i + 1}`}>
              ×
            </button>
          </li>
        ))}
      </ul>
      {notice && (
        <p className="alert alert--ok" role="status">
          {notice}
        </p>
      )}
      {error && <p className="alert">{error}</p>}
      {remaining > 0 && (
        <>
          <input ref={input} type="file" accept={ACCEPTED_TYPES.join(',')} multiple hidden onChange={onFiles} />
          <button type="button" className="btn" onClick={() => input.current?.click()} disabled={busy}>
            {busy ? 'Envoi en cours…' : 'Ajouter des photos'}
          </button>
        </>
      )}
    </div>
  )
}

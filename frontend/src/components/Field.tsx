import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'

interface FieldShellProps {
  label: string
  hint?: ReactNode
  error?: string
  children: (props: { id: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }) => ReactNode
}

/** Champ de formulaire accessible : étiquette liée, aide et erreur annoncées par `aria-describedby`. */
function FieldShell({ label, hint, error, children }: FieldShellProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children({ id, 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy })}
      {hint && (
        <span id={hintId} className="field-hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={errorId} className="field-error">
          {error}
        </span>
      )}
    </div>
  )
}

type Common = { label: string; hint?: ReactNode; error?: string }

export function TextField({ label, hint, error, ...input }: Common & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(a11y) => <input {...input} {...a11y} />}
    </FieldShell>
  )
}

export function TextArea({ label, hint, error, ...area }: Common & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(a11y) => <textarea {...area} {...a11y} />}
    </FieldShell>
  )
}

export function SelectField({ label, hint, error, children, ...select }: Common & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(a11y) => (
        <select {...select} {...a11y}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}

/** Choix exclusif présenté en pastilles (radio natifs, donc navigables au clavier). */
export function Segmented<T extends string>({
  legend,
  name,
  value,
  options,
  onChange,
}: {
  legend: string
  name: string
  value: T
  options: { value: T; label: string; hint?: string }[]
  onChange: (value: T) => void
}) {
  return (
    <fieldset className="segmented">
      <legend>{legend}</legend>
      <div className="segmented__options">
        {options.map((option) => (
          <label key={option.value} className={`segmented__option${option.value === value ? ' is-active' : ''}`}>
            <input
              type="radio"
              name={name}
              value={option.value}
              checked={option.value === value}
              onChange={() => onChange(option.value)}
            />
            <span>{option.label}</span>
            {option.hint && <small>{option.hint}</small>}
          </label>
        ))}
      </div>
    </fieldset>
  )
}

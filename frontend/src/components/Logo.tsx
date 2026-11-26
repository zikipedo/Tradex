/** Logo TradX : deux fils qui se croisent et se nouent (l'échange), suivis du nom. */
export function Logo({ className }: { className?: string }) {
  return (
    <span className={className} style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5em' }}>
      <svg width="30" height="30" viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <path d="M5 10c7 0 13 12 22 12M5 22c7 0 13-12 22-12" fill="none" stroke="var(--ochre)" strokeWidth="2.8" strokeLinecap="round" />
        <circle cx="16" cy="16" r="3" fill="currentColor" />
      </svg>
      <span
        style={{
          fontFamily: 'var(--font-display)',
          fontVariationSettings: "'SOFT' 100",
          fontWeight: 700,
          fontSize: '1.45rem',
          letterSpacing: '-0.04em',
          lineHeight: 1,
        }}
      >
        TradX
      </span>
    </span>
  )
}

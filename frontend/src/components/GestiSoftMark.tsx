import { useId } from 'react'

/** Marchio GestiSoft (badge esagonale con accento arancione) — stessa forma usata nel design canvas approvato. */
export function GestiSoftMark({ size = 32 }: { size?: number }) {
  const gradientId = `gsMarkGradient-${useId()}`

  return (
    <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true">
      <circle cx="24" cy="24" r="22" fill="#0F141B" />
      <path d="M24 8 L38 16 V32 L24 40 L10 32 V16 Z" fill="none" stroke={`url(#${gradientId})`} strokeWidth={3.6} />
      <path d="M24 24 V33" stroke="#DD7A2C" strokeWidth={3.6} strokeLinecap="round" />
      <defs>
        <linearGradient id={gradientId} x1="10" y1="8" x2="38" y2="40">
          <stop offset="0" stopColor="#EAF6FC" />
          <stop offset="1" stopColor="#1C7EA8" />
        </linearGradient>
      </defs>
    </svg>
  )
}

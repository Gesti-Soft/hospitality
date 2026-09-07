import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement>

const base = { width: 17, height: 17, viewBox: '0 0 18 18', fill: 'none' } as const

export function IconCruscotto(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <rect x="2" y="2" width="6" height="6" rx="1.3" stroke="currentColor" strokeWidth={1.6} />
      <rect x="10" y="2" width="6" height="6" rx="1.3" stroke="currentColor" strokeWidth={1.6} />
      <rect x="2" y="10" width="6" height="6" rx="1.3" stroke="currentColor" strokeWidth={1.6} />
      <rect x="10" y="10" width="6" height="6" rx="1.3" stroke="currentColor" strokeWidth={1.6} />
    </svg>
  )
}

export function IconNotifiche(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M4.5 13V8.3C4.5 5.6 6.5 3.4 9 3.4C11.5 3.4 13.5 5.6 13.5 8.3V13" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
      <path d="M3.5 13H14.5" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
      <path d="M7.2 15.2C7.6 15.7 8.3 16 9 16C9.7 16 10.4 15.7 10.8 15.2" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconSuperAdmin(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M9 2L15 4V8.5C15 12 12.5 14.8 9 16C5.5 14.8 3 12 3 8.5V4L9 2Z" stroke="currentColor" strokeWidth={1.5} strokeLinejoin="round" />
      <path d="M6.3 8.8L8 10.5L11.5 7" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

export function IconCalendario(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <rect x="2" y="3.5" width="14" height="12" rx="1.6" stroke="currentColor" strokeWidth={1.5} />
      <path d="M2 7H16M6 2V4.5M12 2V4.5" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconCamere(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M2 10V13.5H16V10" stroke="currentColor" strokeWidth={1.5} />
      <path d="M2.5 10V7.6C2.5 6.7 3.2 6 4.1 6H13.9C14.8 6 15.5 6.7 15.5 7.6V10" stroke="currentColor" strokeWidth={1.5} />
      <circle cx="5.3" cy="8.2" r="1" stroke="currentColor" strokeWidth={1.3} />
      <path d="M2 13.5V15M16 13.5V15" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconPulizie(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M7 15.5L11.5 4L13 8L16 9.5L12.5 11L11 15L9.5 11L6.5 9.5Z" stroke="currentColor" strokeWidth={1.3} strokeLinejoin="round" />
      <path d="M2.5 6.5L3.4 4L4.3 6.5L6.5 7.5L4.3 8.5L3.4 11L2.5 8.5L0.5 7.5Z" stroke="currentColor" strokeWidth={1.1} strokeLinejoin="round" />
    </svg>
  )
}

export function IconTipologie(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M9.5 2.5H14.5C15.05 2.5 15.5 2.95 15.5 3.5V8.5C15.5 8.9 15.34 9.28 15.06 9.56L9.56 15.06C8.98 15.64 8.02 15.64 7.44 15.06L2.94 10.56C2.36 9.98 2.36 9.02 2.94 8.44L8.44 2.94C8.72 2.66 9.1 2.5 9.5 2.5Z" stroke="currentColor" strokeWidth={1.5} strokeLinejoin="round" />
      <circle cx="12" cy="6" r="1.1" stroke="currentColor" strokeWidth={1.3} />
    </svg>
  )
}

export function IconOspiti(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="6" r="3" stroke="currentColor" strokeWidth={1.5} />
      <path d="M3 16C3 12.7 5.7 11 9 11C12.3 11 15 12.7 15 16" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconFinanze(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <rect x="2" y="4.5" width="14" height="9.5" rx="1.6" stroke="currentColor" strokeWidth={1.5} />
      <path d="M2 8H16" stroke="currentColor" strokeWidth={1.5} />
      <path d="M12 8V11" stroke="currentColor" strokeWidth={1.5} />
    </svg>
  )
}

export function IconRiepilogo(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M4 2.5H14V15.5H4V2.5Z" stroke="currentColor" strokeWidth={1.5} strokeLinejoin="round" />
      <path d="M6.3 6H11.7M6.3 9H11.7" stroke="currentColor" strokeWidth={1.3} strokeLinecap="round" />
      <path d="M6.3 12.3H11.7" stroke="currentColor" strokeWidth={1.8} strokeLinecap="round" />
    </svg>
  )
}

export function IconSpese(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth={1.5} />
      <path d="M6 9H12" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" />
    </svg>
  )
}

export function IconEntrate(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth={1.5} />
      <path d="M9 6V12M6 9H12" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" />
    </svg>
  )
}

export function IconCauzioni(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M9 2.2L14.5 4.3V8.7C14.5 12.1 12.2 14.7 9 15.8C5.8 14.7 3.5 12.1 3.5 8.7V4.3L9 2.2Z" stroke="currentColor" strokeWidth={1.4} strokeLinejoin="round" />
      <circle cx="9" cy="8" r="1.3" stroke="currentColor" strokeWidth={1.2} />
      <path d="M9 9.3V11" stroke="currentColor" strokeWidth={1.2} strokeLinecap="round" />
    </svg>
  )
}

export function IconFatturazione(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M4.5 2H12L14.5 4.5V16H4.5V2Z" stroke="currentColor" strokeWidth={1.5} strokeLinejoin="round" />
      <path d="M6.8 8H12.2M6.8 11H12.2" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
    </svg>
  )
}

export function IconPolizia(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M9 2L15 4.5V9C15 12.6 12.4 15 9 16C5.6 15 3 12.6 3 9V4.5L9 2Z" stroke="currentColor" strokeWidth={1.5} strokeLinejoin="round" />
    </svg>
  )
}

export function IconOsservatorio(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="1.6" stroke="currentColor" strokeWidth={1.4} />
      <path d="M9 3.5C11.5 5 13 7 13 9C13 11 11.5 13 9 14.5" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
      <path d="M9 3.5C6.5 5 5 7 5 9C5 11 6.5 13 9 14.5" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
    </svg>
  )
}

export function IconPayTourist(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth={1.5} />
      <path d="M9 6V9L11 11" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconWubook(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth={1.5} />
      <path d="M2.5 9H15.5M9 2.5C10.8 4.4 11.8 6.6 11.8 9C11.8 11.4 10.8 13.6 9 15.5C7.2 13.6 6.2 11.4 6.2 9C6.2 6.6 7.2 4.4 9 2.5Z" stroke="currentColor" strokeWidth={1.3} />
    </svg>
  )
}

export function IconUtenti(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="6.5" cy="6.5" r="2.3" stroke="currentColor" strokeWidth={1.4} />
      <circle cx="12.5" cy="8" r="1.9" stroke="currentColor" strokeWidth={1.4} />
      <path d="M2 15C2 12.2 4 10.7 6.5 10.7C9 10.7 11 12.2 11 15" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
      <path d="M11.5 11C13.7 11 15.5 12.3 15.5 15" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
    </svg>
  )
}

export function IconImpostazioni(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <circle cx="9" cy="9" r="2.4" stroke="currentColor" strokeWidth={1.5} />
      <path
        d="M9 2.7V4.4M9 13.6V15.3M15.3 9H13.6M4.4 9H2.7M13.4 4.6L12.2 5.8M5.8 12.2L4.6 13.4M13.4 13.4L12.2 12.2M5.8 5.8L4.6 4.6"
        stroke="currentColor"
        strokeWidth={1.5}
        strokeLinecap="round"
      />
    </svg>
  )
}

export function IconStatistiche(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M2.5 15.5V2.5" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
      <path d="M2.5 15.5H15.5" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
      <rect x="4.7" y="10" width="2.3" height="5.5" rx="0.6" stroke="currentColor" strokeWidth={1.4} />
      <rect x="8.85" y="6.5" width="2.3" height="9" rx="0.6" stroke="currentColor" strokeWidth={1.4} />
      <rect x="13" y="3.5" width="2.3" height="12" rx="0.6" stroke="currentColor" strokeWidth={1.4} />
    </svg>
  )
}

export function IconLog(props: IconProps) {
  return (
    <svg {...base} {...props}>
      <path d="M4 2.5H14V15.5H4V2.5Z" stroke="currentColor" strokeWidth={1.5} />
      <path d="M6.5 6H11.5M6.5 9H11.5M6.5 12H9.5" stroke="currentColor" strokeWidth={1.4} strokeLinecap="round" />
    </svg>
  )
}

export function IconEsci(props: IconProps) {
  return (
    <svg width={16} height={16} viewBox="0 0 16 16" fill="none" {...props}>
      <path d="M6 2H3.5C2.7 2 2 2.7 2 3.5V12.5C2 13.3 2.7 14 3.5 14H6" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
      <path d="M10.5 5L14 8L10.5 11" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" />
      <path d="M14 8H6" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" />
    </svg>
  )
}

export function IconChevronDown(props: IconProps) {
  return (
    <svg width={14} height={14} viewBox="0 0 16 16" fill="none" {...props}>
      <path d="M4 6L8 10L12 6" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

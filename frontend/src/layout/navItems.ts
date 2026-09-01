import type { ComponentType, SVGProps } from 'react'
import {
  IconCalendario,
  IconCamere,
  IconCruscotto,
  IconFatturazione,
  IconFinanze,
  IconImpostazioni,
  IconLog,
  IconOspiti,
  IconOsservatorio,
  IconPayTourist,
  IconPolizia,
  IconSuperAdmin,
  IconTipologie,
  IconUtenti,
  IconWubook,
} from './navIcons'

export type ServizioConcedibile = 'wubookAbilitato' | 'alloggiatiWebAbilitato' | 'osservatorioAbilitato' | 'payTouristAbilitato'

export interface NavItem {
  label: string
  path: string
  icon: ComponentType<SVGProps<SVGSVGElement>>
  /**
   * Se il Super Admin non ha concesso questo servizio al Cliente della struttura corrente, la voce
   * non deve comparire affatto (non solo bloccata/in errore) — vedi StrutturaDto sulla struttura
   * selezionata.
   */
  richiedeServizio?: ServizioConcedibile
}

export interface NavSection {
  title: string
  /** Se true, la sezione compare solo per il Super Admin (staff GestiSoft), mai per un Cliente. */
  soloSuperAdmin?: boolean
  items: NavItem[]
}

export const navSections: NavSection[] = [
  {
    title: 'Super Admin',
    soloSuperAdmin: true,
    items: [{ label: 'Dashboard Super Admin', path: '/super-admin', icon: IconSuperAdmin }],
  },
  {
    title: 'Operativo',
    items: [
      { label: 'Cruscotto', path: '/', icon: IconCruscotto },
      { label: 'Calendario', path: '/calendario', icon: IconCalendario },
      { label: 'Ospiti', path: '/ospiti', icon: IconOspiti },
      { label: 'Tipologie', path: '/tipologie', icon: IconTipologie },
      { label: 'Camere', path: '/camere', icon: IconCamere },
      { label: 'Finanze', path: '/finanze', icon: IconFinanze },
      { label: 'Fatture', path: '/fatturazione', icon: IconFatturazione },
    ],
  },
  {
    title: 'Invii automatici',
    items: [
      { label: 'Polizia di Stato', path: '/polizia-di-stato', icon: IconPolizia, richiedeServizio: 'alloggiatiWebAbilitato' },
      { label: 'Osservatorio', path: '/osservatorio', icon: IconOsservatorio, richiedeServizio: 'osservatorioAbilitato' },
      { label: 'PayTourist', path: '/paytourist', icon: IconPayTourist, richiedeServizio: 'payTouristAbilitato' },
      { label: 'OTA · Wubook', path: '/wubook', icon: IconWubook, richiedeServizio: 'wubookAbilitato' },
    ],
  },
  {
    title: 'Amministrazione',
    items: [
      { label: 'Utenti', path: '/utenti', icon: IconUtenti },
      { label: 'Impostazioni', path: '/impostazioni', icon: IconImpostazioni },
      { label: 'Log', path: '/log', icon: IconLog },
    ],
  },
]

export const navItemsFlat: NavItem[] = navSections.flatMap((s) => s.items)

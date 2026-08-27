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
  IconUtenti,
  IconWubook,
} from './navIcons'

export interface NavItem {
  label: string
  path: string
  icon: ComponentType<SVGProps<SVGSVGElement>>
}

export interface NavSection {
  title: string
  items: NavItem[]
}

export const navSections: NavSection[] = [
  {
    title: 'Operativo',
    items: [
      { label: 'Cruscotto', path: '/', icon: IconCruscotto },
      { label: 'Calendario', path: '/calendario', icon: IconCalendario },
      { label: 'Camere', path: '/camere', icon: IconCamere },
      { label: 'Ospiti', path: '/ospiti', icon: IconOspiti },
      { label: 'Finanze', path: '/finanze', icon: IconFinanze },
      { label: 'Fatturazione', path: '/fatturazione', icon: IconFatturazione },
    ],
  },
  {
    title: 'Invii automatici',
    items: [
      { label: 'Polizia di Stato', path: '/polizia-di-stato', icon: IconPolizia },
      { label: 'Osservatorio', path: '/osservatorio', icon: IconOsservatorio },
      { label: 'PayTourist', path: '/paytourist', icon: IconPayTourist },
      { label: 'OTA · Wubook', path: '/wubook', icon: IconWubook },
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

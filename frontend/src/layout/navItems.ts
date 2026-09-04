import type { ComponentType, SVGProps } from 'react'
import {
  IconCalendario,
  IconCamere,
  IconCauzioni,
  IconCruscotto,
  IconEntrate,
  IconFatturazione,
  IconFinanze,
  IconImpostazioni,
  IconLog,
  IconOspiti,
  IconOsservatorio,
  IconPayTourist,
  IconPolizia,
  IconRiepilogo,
  IconSpese,
  IconStatistiche,
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
  /** Icona della sezione, usata come tab nella barra di navigazione in alto (vedi AppShell). */
  icon: ComponentType<SVGProps<SVGSVGElement>>
  /** Se true, la sezione compare solo per il Super Admin (staff GestiSoft), mai per un Cliente. */
  soloSuperAdmin?: boolean
  /**
   * Riservata a chi gestisce gli utenti della struttura corrente (permesso SettingUser) — un
   * lavoratore normale (Cliente non amministratore) non deve vedere la sezione. Il Super Admin la
   * vede sempre.
   */
  richiedeGestioneUtenti?: boolean
  items: NavItem[]
}

export const navSections: NavSection[] = [
  {
    title: 'Super Admin',
    icon: IconSuperAdmin,
    soloSuperAdmin: true,
    items: [
      { label: 'Dashboard', path: '/super-admin', icon: IconSuperAdmin },
      { label: 'Clienti', path: '/super-admin/clienti', icon: IconUtenti },
      { label: 'Impostazioni', path: '/super-admin/impostazioni', icon: IconImpostazioni },
    ],
  },
  {
    title: 'Operativo',
    icon: IconCruscotto,
    items: [
      { label: 'Cruscotto', path: '/', icon: IconCruscotto },
      { label: 'Calendario', path: '/calendario', icon: IconCalendario },
      { label: 'Ospiti', path: '/ospiti', icon: IconOspiti },
      { label: 'Tipologie', path: '/tipologie', icon: IconTipologie },
      { label: 'Camere', path: '/camere', icon: IconCamere },
    ],
  },
  {
    title: 'Finanze',
    icon: IconFinanze,
    items: [
      { label: 'Statistiche', path: '/statistiche', icon: IconStatistiche },
      { label: 'Riepilogo', path: '/finanze/riepilogo', icon: IconRiepilogo },
      { label: 'Spese', path: '/finanze/spese', icon: IconSpese },
      { label: 'Entrate', path: '/finanze/entrate', icon: IconEntrate },
      { label: 'Cauzioni', path: '/finanze/cauzioni', icon: IconCauzioni },
      { label: 'Fatture', path: '/fatturazione', icon: IconFatturazione },
    ],
  },
  {
    title: 'Invii automatici',
    icon: IconWubook,
    items: [
      { label: 'Polizia di Stato', path: '/polizia-di-stato', icon: IconPolizia, richiedeServizio: 'alloggiatiWebAbilitato' },
      { label: 'Osservatorio', path: '/osservatorio', icon: IconOsservatorio, richiedeServizio: 'osservatorioAbilitato' },
      { label: 'PayTourist', path: '/paytourist', icon: IconPayTourist, richiedeServizio: 'payTouristAbilitato' },
      { label: 'OTA · Wubook', path: '/wubook', icon: IconWubook, richiedeServizio: 'wubookAbilitato' },
    ],
  },
  {
    title: 'Amministrazione',
    icon: IconImpostazioni,
    richiedeGestioneUtenti: true,
    items: [
      { label: 'Utenti', path: '/utenti', icon: IconUtenti },
      { label: 'Impostazioni', path: '/impostazioni', icon: IconImpostazioni },
      { label: 'Log', path: '/log', icon: IconLog },
    ],
  },
]

export const navItemsFlat: NavItem[] = navSections.flatMap((s) => s.items)

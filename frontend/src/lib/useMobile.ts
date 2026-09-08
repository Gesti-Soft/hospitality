import useMediaQuery from '@mui/material/useMediaQuery'

/** Sotto i 900px la UI passa dal layout desktop (tabelle, dialog affiancati) a un layout a card/schermo intero pensato per il touch. Stessa soglia usata da `AppShell` per il menu a comparsa. */
export function useMobile(): boolean {
  return useMediaQuery('(max-width:899.95px)')
}

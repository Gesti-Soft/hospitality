export const ANNO_CORRENTE = new Date().getFullYear()

/**
 * Anni da proporre in un selettore "Anno": quelli con dati reali più l'anno corrente, sempre —
 * su richiesta esplicita, anche una struttura senza ancora nulla per l'anno in corso deve poterlo
 * selezionare (es. per registrare la prima spesa/entrata) — ordinati dal più recente.
 */
export function anniConAnnoCorrente(anniConDati: number[] | undefined): number[] {
  const insieme = new Set(anniConDati ?? [])
  insieme.add(ANNO_CORRENTE)
  return Array.from(insieme).sort((a, b) => b - a)
}

/**
 * Anni da proporre in un selettore "Anno" quando non è disponibile un elenco di "anni con dati reali"
 * (es. le schermate Alloggiati Web/Osservatorio/PayTourist, che non hanno un endpoint dedicato per
 * questo — a differenza di Statistiche/Finanze): semplicemente l'anno corrente e i `quantita - 1`
 * precedenti, dal più recente.
 */
export function ultimiAnni(quantita = 4): number[] {
  return Array.from({ length: quantita }, (_, i) => ANNO_CORRENTE - i)
}

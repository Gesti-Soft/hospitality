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

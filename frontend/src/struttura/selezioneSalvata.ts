/**
 * Cliente e Struttura di lavoro ricordati nel browser.
 *
 * Il Cliente/Operatore ha una sola struttura propria: sta in `localStorage` e dura tra un accesso e
 * l'altro, come è sempre stato.
 *
 * Il Super Admin no. La sua scelta sta in `sessionStorage`, che è esattamente la durata voluta:
 * sopravvive al ricaricamento della pagina — prima bastava un F5 o un indirizzo digitato a mano per
 * perdere la struttura e farsi rimbalzare sulla dashboard Super Admin — ma muore con la chiusura
 * del browser, e non passa a una scheda nuova. Chi riapre o riaccede riparte dalla dashboard, che è
 * il comportamento chiesto: quello del Super Admin è il contesto di lavoro di una sessione, non una
 * preferenza da ricordare.
 *
 * Servono **entrambi** i valori: la lista delle Strutture si carica per Cliente, quindi senza il
 * Cliente la Struttura salvata non si potrebbe nemmeno risolvere.
 */
const CHIAVE_STRUTTURA_OPERATORE = 'gestisoft.strutturaId'
const CHIAVE_CLIENTE_SUPER_ADMIN = 'gestisoft.superAdmin.clienteId'
const CHIAVE_STRUTTURA_SUPER_ADMIN = 'gestisoft.superAdmin.strutturaId'

function scrivi(archivio: Storage, chiave: string, valore: string | null): void {
  if (valore === null) {
    archivio.removeItem(chiave)
    return
  }
  archivio.setItem(chiave, valore)
}

export const leggiStrutturaOperatore = (): string | null => localStorage.getItem(CHIAVE_STRUTTURA_OPERATORE)

export const salvaStrutturaOperatore = (strutturaId: string | null): void =>
  scrivi(localStorage, CHIAVE_STRUTTURA_OPERATORE, strutturaId)

export const leggiSelezioneSuperAdmin = (): { clienteId: string | null; strutturaId: string | null } => ({
  clienteId: sessionStorage.getItem(CHIAVE_CLIENTE_SUPER_ADMIN),
  strutturaId: sessionStorage.getItem(CHIAVE_STRUTTURA_SUPER_ADMIN),
})

export function salvaSelezioneSuperAdmin(clienteId: string | null, strutturaId: string | null): void {
  scrivi(sessionStorage, CHIAVE_CLIENTE_SUPER_ADMIN, clienteId)
  scrivi(sessionStorage, CHIAVE_STRUTTURA_SUPER_ADMIN, strutturaId)
}

export const dimenticaSelezioneSuperAdmin = (): void => salvaSelezioneSuperAdmin(null, null)

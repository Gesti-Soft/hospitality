import { useCallback, useEffect, useRef, useState } from 'react'

// Righe caricate/mostrate alla volta: si parte con un'unica pagina (quante ce ne stanno a video),
// poi se ne aggiunge un'altra ogni volta che si scorre fino in fondo alla tabella — stesso valore
// usato in OspitiPage, la prima pagina ad adottare questo pattern.
const RIGHE_PER_PAGINA = 25

/**
 * Paginazione a scroll infinito per una tabella (o per un elenco di card su mobile): mostra le
 * prime `RIGHE_PER_PAGINA` righe di un elenco già filtrato/ordinato, caricandone altre quando la
 * sentinella entra nel viewport. `sentinellaRef` è una ref callback (non un `RefObject`) proprio per
 * poter essere assegnata sia a un `<TableRow ref={sentinellaRef}>` su desktop sia a una card/`Box`
 * su mobile — solo una delle due è montata alla volta a seconda del breakpoint, e una ref callback
 * (a differenza di un `RefObject` tipizzato su un elemento specifico) è assegnabile a `ref` di
 * qualunque elemento DOM. `resetDeps` deve elencare i valori che, se cambiano, devono far ripartire
 * la paginazione dalla prima pagina (es. testo di ricerca, filtri data) — altrimenti, cambiando
 * filtro mentre si è scrollato in fondo, si vedrebbe un elenco vuoto finché non si rifà lo scroll.
 */
export function usePaginazioneScroll(totaleRighe: number, resetDeps: readonly unknown[]) {
  const [righeVisibili, setRigheVisibili] = useState(RIGHE_PER_PAGINA)
  const altreDaCaricare = righeVisibili < totaleRighe

  // resetDeps è per definizione l'elenco completo delle dipendenze, deciso da chi chiama l'hook —
  // non un array letterale, quindi il linter non può verificarlo staticamente da qui in nessuno dei
  // due modi (dipendenze mancanti / array non letterale): entrambi gli avvisi vanno silenziati.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => {
    setRigheVisibili(RIGHE_PER_PAGINA)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, resetDeps)

  const elementoSentinella = useRef<HTMLElement | null>(null)
  const sentinellaRef = useCallback((el: HTMLElement | null) => {
    elementoSentinella.current = el
  }, [])

  useEffect(() => {
    if (!altreDaCaricare) return
    const el = elementoSentinella.current
    if (!el) return

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          setRigheVisibili((n) => n + RIGHE_PER_PAGINA)
        }
      },
      { rootMargin: '200px' },
    )
    observer.observe(el)
    return () => observer.disconnect()
  }, [altreDaCaricare])

  return { righeVisibili, altreDaCaricare, sentinellaRef }
}

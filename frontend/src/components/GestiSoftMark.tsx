/** Logo aziendale GestiSoft (stesso file usato dal programma legacy). */
export function GestiSoftMark({ size = 32 }: { size?: number }) {
  return <img src="/logo.png" alt="GestiSoft" width={size} height={size} style={{ display: 'block', objectFit: 'contain' }} />
}

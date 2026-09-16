import { createTheme } from '@mui/material/styles'

/**
 * Token di design derivati dal canvas approvato (identità evoluta dal brand GestiSoft esistente:
 * logo navy/blu con accento arancione, stile "Notte Boutique" del desktop WPF). Fonte di verità
 * per colori/tipografia/raggi: usare sempre questi token, mai valori hardcoded nei componenti.
 */
export const tokens = {
  ink900: '#141922',
  ink800: '#1B222C',
  ink700: '#232C38',
  ink600: '#3A4453',
  ink100: '#C9D2DE',
  paper: '#F4F6F9',
  surface: '#FFFFFF',
  surfaceBorder: '#E2E6EC',
  textPrimary: '#1B222C',
  textSecondary: '#5B6472',
  textTertiary: '#8B93A0',
  blue700: '#125E80',
  blue600: '#1C7EA8',
  blue400: '#4FB4DE',
  blue100: '#DCEEF6',
  orange700: '#B4551C',
  orange600: '#DD7A2C',
  orange400: '#F0A868',
  orange100: '#FBE9D7',
  ok600: '#1F8A70',
  ok100: '#DCF3EC',
  wait600: '#C2921C',
  wait100: '#FBF0D9',
  error600: '#C24444',
  error100: '#FBE3E3',
  navRail: '#171C24',
  navHover: '#20262F',
  navBorder: '#2C333D',
  navText: '#A9B3C0',
} as const

export const fontDisplay = "'Montserrat','Segoe UI',sans-serif"
export const fontBody = "'Public Sans','Segoe UI',sans-serif"
export const fontMono = "'IBM Plex Mono',Consolas,monospace"

/**
 * Stile di ogni numero che si legge come quantità — importi, totali, date. Non usa il mono: il
 * monospazio serviva solo ad allineare le cifre in colonna, e per quello bastano le cifre tabulari,
 * che lo fanno restando nel font del testo. Il mono resta ai codici veri (numero prenotazione,
 * codice fiscale, id nei log), dove il carattere-per-carattere è il punto.
 */
export const stileImporto = { fontFamily: fontBody, fontVariantNumeric: 'tabular-nums' } as const

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: tokens.blue600, dark: tokens.blue700, light: tokens.blue400, contrastText: '#fff' },
    secondary: { main: tokens.orange600, dark: tokens.orange700, light: tokens.orange400, contrastText: '#fff' },
    background: { default: tokens.paper, paper: tokens.surface },
    text: { primary: tokens.textPrimary, secondary: tokens.textSecondary },
    success: { main: tokens.ok600 },
    warning: { main: tokens.wait600 },
    error: { main: tokens.error600 },
    divider: tokens.surfaceBorder,
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: fontBody,
    h1: { fontFamily: fontDisplay, fontWeight: 800 },
    h2: { fontFamily: fontDisplay, fontWeight: 800 },
    h3: { fontFamily: fontDisplay, fontWeight: 700 },
    h4: { fontFamily: fontDisplay, fontWeight: 700 },
    h5: { fontFamily: fontDisplay, fontWeight: 700 },
    h6: { fontFamily: fontDisplay, fontWeight: 700 },
    button: { fontFamily: fontBody, fontWeight: 700, textTransform: 'none' },
  },
  components: {
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 8, padding: '10px 20px' },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: 'none' },
      },
    },
    MuiTextField: {
      defaultProps: { size: 'small' },
    },
    MuiAutocomplete: {
      // Senza questo, un <Autocomplete> senza `size` esplicito rende il suo TextField interno a
      // "medium" (default MUI) mentre il resto dell'app è tutto "small" — il disallineamento nelle
      // metriche fa sì che la label ristretta finisca a cavallo del bordo del notch invece che
      // sopra, tagliata a metà (bug reale riscontrato in PrezzoDialog/PrenotazioneDialog).
      defaultProps: { size: 'small' },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: { borderRadius: 6, backgroundColor: '#fff' },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 700 },
      },
    },
  },
})

/**
 * The app's name and identity, in one place.
 *
 * Change `appName` here to rename the app everywhere: screen headers, the browser tab title,
 * the PWA manifest (install name) and the Capacitor (App Store) app name all read it.
 * This file is plain data with no browser or Vite APIs, so vite.config.ts and
 * capacitor.config.ts can import it too.
 *
 * "K12 Practice" is a working name. A new name needs a trademark, App Store and domain check
 * before it goes here. Never put a state, exam or standards name in it.
 */
export const brand = {
  appName: 'K12 Practice',
  /** Shown under the icon on a home screen; keep it to about 12 characters. */
  shortName: 'K12 Practice',
  /** Browser and OS chrome colour. Matches --brand-blue in shared/theme/tokens.css. */
  themeColor: '#1F5BD6',
  /** Page background behind the PWA splash. Matches --paper in tokens.css. */
  backgroundColor: '#F5F7FB',
} as const

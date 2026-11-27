import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import { brand } from './src/shared/config/brand'

/** Fills the app name and colours from shared/config/brand.ts into index.html and the PWA manifest. */
function brandPlugin(): Plugin {
  const manifest = JSON.stringify(
    {
      name: brand.appName,
      short_name: brand.shortName,
      start_url: '/',
      display: 'standalone',
      orientation: 'any',
      background_color: brand.backgroundColor,
      theme_color: brand.themeColor,
      icons: [],
    },
    null,
    2,
  )
  return {
    name: 'k12-brand',
    transformIndexHtml: (html) =>
      html.replaceAll('%BRAND_APP_NAME%', brand.appName).replaceAll('%BRAND_THEME_COLOR%', brand.themeColor),
    configureServer(server) {
      server.middlewares.use('/manifest.webmanifest', (_req, res) => {
        res.setHeader('Content-Type', 'application/manifest+json')
        res.end(manifest)
      })
    },
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'manifest.webmanifest', source: manifest })
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), brandPlugin()],
})

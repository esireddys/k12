import type { CapacitorConfig } from '@capacitor/cli'

// The same Vite build ships as the PWA and inside the App Store app.
// Add the iOS project with: npx cap add ios (needs a Mac with Xcode).
const config: CapacitorConfig = {
  appId: 'com.k12practice.app',
  appName: 'K12 Practice',
  webDir: 'dist',
}

export default config

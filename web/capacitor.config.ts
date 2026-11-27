import type { CapacitorConfig } from '@capacitor/cli'
import { brand } from './src/shared/config/brand'

// The same Vite build ships as the PWA and inside the App Store app.
// Add the iOS project with: npx cap add ios (needs a Mac with Xcode).
const config: CapacitorConfig = {
  // The bundle id can't change once the app is in the App Store; settle the final name first.
  appId: 'com.k12practice.app',
  appName: brand.appName,
  webDir: 'dist',
}

export default config

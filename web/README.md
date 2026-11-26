# web

React + TypeScript + Vite. One build ships as the installable PWA and, through Capacitor, as the App Store app.

```bash
npm install
cp .env.example .env.local   # points at the local API
npm run dev                  # http://localhost:5173
npm run build                # outputs dist/
npx cap add ios              # once, on a Mac with Xcode
npx cap sync                 # after each build
```

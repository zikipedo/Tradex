/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

/**
 * Politique de sécurité du contenu (CSP) ajoutée seulement au build de production :
 * en développement, Vite injecte un script en ligne (rechargement à chaud) que cette CSP bloquerait.
 * En production, le serveur web doit aussi l'envoyer en en-tête HTTP (la balise meta est un filet de sécurité).
 */
const CONTENT_SECURITY_POLICY = [
  "default-src 'self'",
  "img-src 'self' data: blob:",
  "font-src 'self'",
  "style-src 'self' 'unsafe-inline'",
  "script-src 'self'",
  "connect-src 'self'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'",
].join('; ')

function contentSecurityPolicy(): Plugin {
  return {
    name: 'tradx-csp',
    apply: 'build',
    transformIndexHtml: () => [
      { tag: 'meta', attrs: { 'http-equiv': 'Content-Security-Policy', content: CONTENT_SECURITY_POLICY }, injectTo: 'head-prepend' },
    ],
  }
}

/**
 * Configuration Vite de TradX.
 * En développement, `/api` est relayé vers l'API ASP.NET Core : le front et l'API
 * partagent ainsi la même origine (pas de CORS à ouvrir, cookies et photos servis tels quels).
 */
export default defineConfig({
  plugins: [react(), contentSecurityPolicy()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: process.env.TRADX_API_URL ?? 'http://localhost:5191', changeOrigin: false },
    },
  },
  build: {
    // Cible des navigateurs récents mais courants sur Android d'entrée de gamme.
    target: 'es2022',
    cssCodeSplit: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})

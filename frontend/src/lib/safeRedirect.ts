/**
 * Page de retour après connexion, limitée aux chemins internes de l'application.
 * Refuse les adresses absolues (`https://…`), protocole-relatives (`//site.com`), les barres obliques
 * inverses (que certains navigateurs normalisent en `/`) et les caractères de contrôle :
 * protection contre la redirection ouverte (OWASP A01).
 */
export function safeRedirect(target: unknown, fallback = '/annonces'): string {
  if (typeof target !== 'string') return fallback
  // eslint-disable-next-line no-control-regex
  if (!target.startsWith('/') || target.startsWith('//') || target.includes('\\') || /[\u0000-\u001f]/.test(target)) {
    return fallback
  }
  return target
}

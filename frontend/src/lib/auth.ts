import { api } from './api'
import { useAuthStore } from '../stores/authStore'
import type { AuthSession } from '../stores/authStore'

export async function login(email: string, password: string): Promise<AuthSession> {
  const response = await api.post<AuthSession>('/auth/login', { email, password })
  return response.data
}

export async function refreshSession(): Promise<AuthSession> {
  const response = await api.post<AuthSession>('/auth/refresh')
  return response.data
}

export async function logout(): Promise<void> {
  await api.post('/auth/logout')
}

export async function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  await api.post('/auth/change-password', { currentPassword, newPassword })
}

let bootstrapPromise: Promise<void> | null = null

const SESSION_MARKER = 'hanora_session=1'

function hasSessionMarker(): boolean {
  return document.cookie
    .split(';')
    .map((part) => part.trim())
    .some((part) => part === SESSION_MARKER)
}

/**
 * Dipanggil sekali saat aplikasi dimuat. StrictMode dapat memanggil effect
 * dua kali, jadi promise di-cache agar refresh token tidak dirotasi ganda.
 * Penanda cookie non-httpOnly mencegah request /refresh yang pasti 401
 * ketika pengunjung belum pernah login.
 */
export function bootstrapSession(): Promise<void> {
  if (!bootstrapPromise) {
    if (!hasSessionMarker()) {
      useAuthStore.getState().setAnonymous()
      bootstrapPromise = Promise.resolve()
      return bootstrapPromise
    }

    bootstrapPromise = (async () => {
      try {
        const session = await refreshSession()
        useAuthStore.getState().setSession(session)
      } catch {
        useAuthStore.getState().setAnonymous()
      }
    })()
  }

  return bootstrapPromise
}

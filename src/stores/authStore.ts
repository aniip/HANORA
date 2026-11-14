import { create } from 'zustand'

export type AuthUser = {
  id: string
  name: string
  email: string
  role: 'Owner' | 'Cashier'
  mustChangePassword: boolean
}

export type AuthSession = {
  accessToken: string
  expiresIn: number
  user: AuthUser
}

type AuthStatus = 'unknown' | 'authenticated' | 'anonymous'

type AuthState = {
  status: AuthStatus
  accessToken: string | null
  user: AuthUser | null
  setSession: (session: AuthSession) => void
  clearSession: () => void
  setAnonymous: () => void
}

/**
 * Access token hanya disimpan di memori.
 * Refresh token dikelola backend melalui cookie httpOnly.
 */
export const useAuthStore = create<AuthState>((set) => ({
  status: 'unknown',
  accessToken: null,
  user: null,
  setSession: (session) =>
    set({
      status: 'authenticated',
      accessToken: session.accessToken,
      user: session.user,
    }),
  clearSession: () =>
    set({ status: 'anonymous', accessToken: null, user: null }),
  setAnonymous: () =>
    set({ status: 'anonymous', accessToken: null, user: null }),
}))

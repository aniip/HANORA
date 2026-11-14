import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import type { TableInfo } from '../lib/tables'
import type { TableSessionResponse } from '../lib/orders'

type TableState = {
  sessionToken: string | null
  table: TableInfo | null
  expiresAt: number | null
  setSession: (session: TableSessionResponse) => void
  clearSession: () => void
}

export const useTableStore = create<TableState>()(
  persist(
    (set) => ({
      sessionToken: null,
      table: null,
      expiresAt: null,
      setSession: (session) =>
        set({
          sessionToken: session.tableSessionToken,
          table: session.table,
          expiresAt: Date.now() + session.expiresIn * 1000,
        }),
      clearSession: () =>
        set({ sessionToken: null, table: null, expiresAt: null }),
    }),
    {
      name: 'hanora-table-session',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({
        sessionToken: state.sessionToken,
        table: state.table,
        expiresAt: state.expiresAt,
      }),
    },
  ),
)

export function hasActiveTableSession(state: {
  sessionToken: string | null
  table: TableInfo | null
  expiresAt: number | null
}): boolean {
  return Boolean(
    state.sessionToken &&
      state.table &&
      state.expiresAt &&
      state.expiresAt > Date.now(),
  )
}

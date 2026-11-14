import {
  ClipboardList,
  Coffee,
  History,
  LayoutDashboard,
  LogOut,
} from 'lucide-react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { logout } from '../../lib/auth'
import { useAuthStore } from '../../stores/authStore'

const navigation = [
  { to: '/kasir', label: 'Antrean', icon: ClipboardList, end: true },
  { to: '/kasir/riwayat', label: 'Riwayat', icon: History, end: false },
]

export function CashierLayout() {
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)

  const handleLogout = async () => {
    try {
      await logout()
    } catch {
      // Tetap bersihkan sesi lokal.
    } finally {
      clearSession()
      navigate('/masuk')
    }
  }

  return (
    <div className="min-h-screen bg-coffee-50">
      <header className="sticky top-0 z-40 border-b border-coffee-100 bg-[#fffdfa]">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-3 px-4 md:px-6">
          <div className="flex items-center gap-3">
            <img
              src="/hanora-logo.jpeg"
              alt="Logo HANORA CAFE"
              className="h-10 w-10 rounded-full object-cover"
            />
            <div>
              <p className="font-display text-base leading-none text-coffee-950">
                HANORA CAFE
              </p>
              <p className="mt-1 text-[9px] tracking-[0.22em] text-coffee-600">
                DASHBOARD KASIR
              </p>
            </div>
          </div>

          <nav className="hidden items-center gap-1 md:flex">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `inline-flex items-center gap-2 rounded-xl px-3 py-2 text-xs font-semibold transition ${
                    isActive
                      ? 'bg-coffee-900 text-white'
                      : 'text-coffee-700 hover:bg-coffee-100'
                  }`
                }
              >
                <item.icon size={15} />
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="flex items-center gap-2">
            {user?.role === 'Owner' && (
              <NavLink
                to="/owner"
                className="hidden rounded-lg p-2 text-coffee-600 hover:bg-coffee-100 sm:block"
                aria-label="Dashboard owner"
              >
                <LayoutDashboard size={16} />
              </NavLink>
            )}
            <div className="hidden text-right sm:block">
              <p className="text-xs font-bold text-coffee-950">{user?.name}</p>
              <p className="text-[10px] text-stone-400">{user?.role}</p>
            </div>
            <button
              type="button"
              onClick={() => void handleLogout()}
              className="rounded-lg p-2 text-coffee-600 hover:bg-coffee-100"
              aria-label="Keluar"
            >
              <LogOut size={16} />
            </button>
          </div>
        </div>

        <div className="flex gap-1 border-t border-coffee-50 px-4 pb-2 md:hidden">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg px-2 py-2 text-[11px] font-semibold ${
                  isActive
                    ? 'bg-coffee-900 text-white'
                    : 'text-coffee-700 hover:bg-coffee-100'
                }`
              }
            >
              <item.icon size={14} />
              {item.label}
            </NavLink>
          ))}
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 md:px-6 md:py-8">
        <Outlet />
      </main>

      <footer className="border-t border-coffee-100 bg-white">
        <div className="mx-auto flex max-w-7xl flex-col gap-1 px-4 py-4 text-[10px] text-stone-400 sm:flex-row sm:items-center sm:justify-between md:px-6">
          <p>© 2026 HANORA CAFE. Seluruh hak dilindungi.</p>
          <p className="inline-flex items-center gap-1">
            <Coffee size={12} />
            Workspace kasir
          </p>
        </div>
      </footer>
    </div>
  )
}

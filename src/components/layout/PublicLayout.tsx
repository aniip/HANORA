import { LogIn, LogOut } from 'lucide-react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { logout } from '../../lib/auth'
import { useAuthStore } from '../../stores/authStore'
import { buttonClasses } from '../ui/buttonStyles'

const navigation = [
  { to: '/', label: 'Beranda', end: true },
  { to: '/menu', label: 'Menu' },
  { to: '/galeri', label: 'Galeri' },
  { to: '/lokasi', label: 'Lokasi' },
]

export function PublicLayout() {
  const navigate = useNavigate()
  const status = useAuthStore((state) => state.status)
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)

  const dashboardPath = user?.mustChangePassword
    ? '/ganti-password'
    : user?.role === 'Owner'
      ? '/owner'
      : '/kasir'

  const handleLogout = async () => {
    try {
      await logout()
    } catch {
      // Sesi tetap dibersihkan di sisi klien meskipun request logout gagal.
    } finally {
      clearSession()
      navigate('/')
    }
  }

  return (
    <div className="min-h-screen bg-coffee-50">
      <header className="sticky top-0 z-40 border-b border-coffee-100 bg-coffee-50/90 backdrop-blur-xl">
        <div className="mx-auto flex h-20 max-w-6xl items-center justify-between px-6">
          <Link to="/" className="flex items-center gap-3">
            <img
              src="/hanora-logo.jpeg"
              alt="Logo HANORA CAFE"
              className="h-11 w-11 rounded-full object-cover"
            />
            <span>
              <span className="block font-display text-lg leading-none tracking-wide text-coffee-950">
                HANORA
              </span>
              <span className="mt-1 block text-[9px] tracking-[0.34em] text-coffee-600">
                CAFE
              </span>
            </span>
          </Link>

          <nav className="hidden items-center gap-7 text-sm text-coffee-700 md:flex">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  isActive
                    ? 'font-semibold text-coffee-950'
                    : 'transition hover:text-coffee-950'
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          {status === 'authenticated' && user ? (
            <div className="flex items-center gap-2">
              <Link
                to={dashboardPath}
                className={buttonClasses('outline', '!px-4 !py-2 hidden sm:inline-flex')}
              >
                Dashboard
              </Link>
              <button
                type="button"
                onClick={() => void handleLogout()}
                className="inline-flex items-center gap-2 rounded-xl px-3 py-2 text-sm font-semibold text-coffee-700 transition hover:bg-coffee-100"
              >
                <LogOut size={15} />
                <span className="hidden sm:inline">Keluar</span>
              </button>
            </div>
          ) : (
            <Link
              to="/masuk"
              className={buttonClasses('outline', '!px-4 !py-2')}
            >
              <LogIn size={15} />
              Masuk staf
            </Link>
          )}
        </div>
      </header>

      <main>
        <Outlet />
      </main>

      <footer className="border-t border-coffee-100 bg-[#fffdfa]">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-6 py-8 text-xs text-stone-500 sm:flex-row sm:items-center sm:justify-between">
          <p>© 2026 HANORA CAFE. Seluruh hak dilindungi.</p>
          <p>Coffee · Comfort · Connection</p>
        </div>
      </footer>
    </div>
  )
}

import {
  BarChart3,
  Coffee,
  ExternalLink,
  LayoutDashboard,
  LogOut,
  Menu as MenuIcon,
  Package,
  QrCode,
  Settings,
  Tags,
  Users,
  Wallet,
  X,
} from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { logout } from '../../lib/auth'
import { useAuthStore } from '../../stores/authStore'

const navigation = [
  { to: '/owner', label: 'Ringkasan', icon: LayoutDashboard, end: true },
  { to: '/owner/produk', label: 'Produk', icon: Package },
  { to: '/owner/kategori', label: 'Kategori', icon: Tags },
  { to: '/owner/meja', label: 'Meja & QR', icon: QrCode },
  { to: '/owner/pengeluaran', label: 'Pengeluaran', icon: Wallet },
  { to: '/owner/laporan', label: 'Laporan', icon: BarChart3 },
  { to: '/owner/pengguna', label: 'Pengguna', icon: Users },
  { to: '/owner/pengaturan', label: 'Pengaturan', icon: Settings },
]

export function OwnerLayout() {
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)
  const [mobileOpen, setMobileOpen] = useState(false)

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
      <div
        className={`fixed inset-0 z-40 bg-black/30 lg:hidden ${
          mobileOpen ? 'block' : 'hidden'
        }`}
        onClick={() => setMobileOpen(false)}
        role="presentation"
      />

      <aside
        className={`fixed inset-y-0 left-0 z-50 flex w-[270px] flex-col border-r border-coffee-100 bg-[#fffdfa] px-5 py-6 transition-transform lg:translate-x-0 ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex items-center justify-between">
          <Link to="/owner" className="flex items-center gap-3">
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
          <button
            type="button"
            className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-100 lg:hidden"
            onClick={() => setMobileOpen(false)}
            aria-label="Tutup menu"
          >
            <X size={18} />
          </button>
        </div>

        <div className="mt-10">
          <p className="mb-3 px-3 text-[10px] font-bold uppercase tracking-[0.2em] text-stone-400">
            Dashboard owner
          </p>
          <nav className="space-y-1">
            {navigation.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                onClick={() => setMobileOpen(false)}
                className={({ isActive }) =>
                  `flex w-full items-center gap-3 rounded-xl px-3 py-3 text-sm transition ${
                    isActive
                      ? 'bg-coffee-900 text-white shadow-lg shadow-coffee-900/10'
                      : 'text-stone-600 hover:bg-coffee-50 hover:text-coffee-900'
                  }`
                }
              >
                <item.icon size={18} />
                <span className="font-medium">{item.label}</span>
              </NavLink>
            ))}
          </nav>
        </div>

        <div className="mt-6">
          <Link
            to="/"
            className="flex items-center gap-2 rounded-xl px-3 py-2 text-xs font-semibold text-coffee-600 hover:bg-coffee-50"
          >
            <ExternalLink size={14} />
            Lihat website publik
          </Link>
        </div>

        <div className="mt-auto space-y-4">
          <div className="rounded-2xl bg-coffee-100 p-4">
            <div className="flex items-center gap-2 text-coffee-900">
              <Coffee size={16} />
              <span className="text-xs font-bold">HANORA CAFE</span>
            </div>
            <p className="mt-2 text-[11px] leading-5 text-coffee-700">
              Kelola katalog, keuangan, laporan, dan akses staf HANORA CAFE.
            </p>
          </div>

          <div className="flex items-center justify-between gap-2 border-t border-coffee-100 pt-4">
            <div className="min-w-0">
              <p className="truncate text-xs font-bold text-coffee-950">
                {user?.name ?? 'Owner'}
              </p>
              <p className="truncate text-[10px] text-stone-400">
                {user?.email}
              </p>
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
      </aside>

      <div className="lg:pl-[270px]">
        <header className="sticky top-0 z-30 flex h-[72px] items-center justify-between border-b border-coffee-100 bg-coffee-50/90 px-4 backdrop-blur-xl md:px-8">
          <div className="flex items-center gap-3">
            <button
              type="button"
              className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-100 lg:hidden"
              onClick={() => setMobileOpen(true)}
              aria-label="Buka menu"
            >
              <MenuIcon size={20} />
            </button>
            <div>
              <p className="text-[10px] uppercase tracking-[0.18em] text-stone-400">
                HANORA CAFE
              </p>
              <p className="text-sm font-semibold text-coffee-950">
                Workspace owner
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="hidden rounded-full border border-coffee-200 bg-white px-3 py-1.5 text-[10px] font-semibold text-coffee-700 sm:inline-flex">
              Owner
            </span>
            <div className="grid h-9 w-9 place-items-center rounded-full bg-coffee-800 text-xs font-bold text-white">
              {user?.name?.slice(0, 2).toUpperCase() ?? 'OW'}
            </div>
          </div>
        </header>

        <main className="mx-auto max-w-[1400px] px-4 py-7 md:px-8 md:py-9">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

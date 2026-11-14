import { ArrowLeft, ShieldOff } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { useAuthStore } from '../stores/authStore'

export function ForbiddenPage() {
  const user = useAuthStore((state) => state.user)

  const dashboard =
    user?.role === 'Owner' ? '/owner' : user?.role === 'Cashier' ? '/kasir' : '/'

  return (
    <section className="mx-auto max-w-2xl px-6 py-24 text-center">
      <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-rose-50 text-rose-600">
        <ShieldOff size={28} />
      </div>
      <p className="mt-6 font-display text-6xl text-coffee-300">403</p>
      <h1 className="mt-3 font-display text-3xl text-coffee-950">
        Akses ditolak.
      </h1>
      <p className="mt-3 text-sm leading-7 text-stone-500">
        Role akun Anda tidak memiliki izin untuk membuka halaman ini. Jika ini
        tidak sesuai, hubungi owner.
      </p>
      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <Link to={dashboard} className={buttonClasses('primary')}>
          Ke dashboard
        </Link>
        <Link to="/" className={buttonClasses('outline')}>
          <ArrowLeft size={16} />
          Beranda
        </Link>
      </div>
    </section>
  )
}

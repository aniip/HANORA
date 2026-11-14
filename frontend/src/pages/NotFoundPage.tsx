import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'

export function NotFoundPage() {
  return (
    <section className="mx-auto max-w-2xl px-6 py-24 text-center">
      <p className="font-display text-7xl text-coffee-300">404</p>
      <h1 className="mt-4 font-display text-3xl text-coffee-950">
        Halaman tidak ditemukan.
      </h1>
      <p className="mt-3 text-sm leading-7 text-stone-500">
        Alamat yang Anda buka tidak tersedia. Periksa kembali tautan atau
        kembali ke beranda.
      </p>
      <Link to="/" className={buttonClasses('primary', 'mt-6')}>
        <ArrowLeft size={16} />
        Kembali ke beranda
      </Link>
    </section>
  )
}

import { ArrowLeft } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { StatusPill } from '../components/ui/StatusPill'

type PlaceholderPageProps = {
  title: string
  description: string
  stage: string
}

export function PlaceholderPage({
  title,
  description,
  stage,
}: PlaceholderPageProps) {
  return (
    <section className="mx-auto max-w-3xl px-6 py-20">
      <div className="rounded-3xl border border-coffee-100 bg-white p-8 shadow-soft md:p-10">
        <StatusPill tone="amber">{stage}</StatusPill>

        <h1 className="mt-5 font-display text-4xl text-coffee-950">{title}</h1>
        <p className="mt-3 text-sm leading-7 text-stone-600">{description}</p>

        <div className="mt-6 rounded-2xl border border-coffee-100 bg-coffee-50 p-5 text-sm leading-6 text-coffee-800">
          Halaman ini sengaja belum diisi. Fitur bisnis akan diimplementasikan
          pada tahap yang tertera, dengan endpoint backend dan pengujian yang
          benar. Tidak ada data dummy yang ditampilkan sebagai data asli.
        </div>

        <Link to="/" className={buttonClasses('outline', 'mt-6')}>
          <ArrowLeft size={16} />
          Kembali ke beranda
        </Link>
      </div>
    </section>
  )
}

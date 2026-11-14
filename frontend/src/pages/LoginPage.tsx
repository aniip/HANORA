import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Coffee,
  Loader2,
  Lock,
  Mail,
} from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { login } from '../lib/auth'
import { getApiErrorMessage } from '../lib/apiError'
import { useAuthStore } from '../stores/authStore'

type LoginLocationState = {
  from?: string
}

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation();
  const state = location.state as LoginLocationState | null

  const status = useAuthStore((store) => store.status)
  const user = useAuthStore((store) => store.user)
  const setSession = useAuthStore((store) => store.setSession)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [passwordChanged] = useState(() => {
    if (sessionStorage.getItem('hanora_password_changed') === '1') {
      sessionStorage.removeItem('hanora_password_changed')
      return true
    }

    return false
  })

  if (status === 'authenticated' && user) {
    const target = user.mustChangePassword
      ? '/ganti-password'
      : user.role === 'Owner'
        ? '/owner'
        : '/kasir'

    return <Navigate to={target} replace />
  }

  const redirectAfterLogin =
    state?.from && state.from !== '/ganti-password' && state.from !== '/masuk'
      ? state.from
      : undefined

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')
    setLoading(true)

    try {
      const session = await login(email, password)
      setSession(session)

      if (session.user.mustChangePassword) {
        navigate('/ganti-password', { replace: true })
        return
      }

      const fallback =
        session.user.role === 'Owner' ? '/owner' : '/kasir'
      navigate(redirectAfterLogin ?? fallback, { replace: true })
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="mx-auto grid max-w-5xl gap-0 px-6 py-14 lg:grid-cols-[0.9fr_1.1fr] lg:items-stretch">
      <div className="relative hidden overflow-hidden rounded-l-3xl bg-coffee-950 p-8 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="pointer-events-none absolute -right-16 top-16 h-56 w-56 rounded-full bg-caramel/20 blur-3xl" />
        <div className="relative">
          <div className="flex items-center gap-3">
            <img
              src="/hanora-logo.jpeg"
              alt="Logo HANORA CAFE"
              className="h-12 w-12 rounded-full object-cover"
            />
            <div>
              <p className="font-display text-xl leading-none">HANORA</p>
              <p className="mt-1 text-[9px] tracking-[0.34em] text-white/60">
                CAFE
              </p>
            </div>
          </div>

          <p className="mt-14 text-[10px] font-bold uppercase tracking-[0.3em] text-gold">
            Workspace staf
          </p>
          <h1 className="mt-4 font-display text-4xl leading-tight">
            Kelola layanan
            <br />
            dengan tenang.
          </h1>
          <p className="mt-4 max-w-sm text-sm leading-7 text-white/60">
            Masuk untuk mengakses dashboard kasir atau owner. Sesi Anda diamankan
            dengan access token berumur pendek dan refresh token httpOnly.
          </p>
        </div>

        <div className="relative mt-10 flex items-center gap-2 text-xs text-white/40">
          <Coffee size={15} />
          HANORA CAFE · Sistem internal
        </div>
      </div>

      <div className="rounded-3xl border border-coffee-100 bg-white p-7 shadow-soft lg:rounded-l-none lg:border-l-0 md:p-10">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600 hover:text-coffee-900"
        >
          <ArrowLeft size={14} />
          Kembali ke beranda
        </Link>

        <h2 className="mt-6 font-display text-3xl text-coffee-950">Masuk staf</h2>
        <p className="mt-2 text-sm leading-6 text-stone-500">
          Gunakan akun yang diberikan oleh owner.
        </p>

        {passwordChanged && (
          <div className="mt-5 flex gap-2 rounded-xl bg-emerald-50 p-3 text-xs leading-5 text-emerald-800">
            <CheckCircle2 size={16} className="mt-0.5 shrink-0" />
            Password berhasil diubah. Silakan masuk dengan password baru.
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-7 space-y-4">
          <label className="block text-xs font-semibold text-coffee-800">
            Email
            <span className="mt-2 flex items-center gap-2 rounded-xl border border-coffee-200 px-3 py-3 focus-within:border-coffee-600">
              <Mail size={16} className="text-stone-400" />
              <input
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
                autoComplete="email"
                placeholder="nama@hanoracafe.id"
                className="w-full bg-transparent text-sm outline-none placeholder:text-stone-400"
              />
            </span>
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Password
            <span className="mt-2 flex items-center gap-2 rounded-xl border border-coffee-200 px-3 py-3 focus-within:border-coffee-600">
              <Lock size={16} className="text-stone-400" />
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
                minLength={8}
                autoComplete="current-password"
                placeholder="Masukkan password"
                className="w-full bg-transparent text-sm outline-none placeholder:text-stone-400"
              />
            </span>
          </label>

          {error && (
            <div
              role="alert"
              className="flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
            >
              <AlertCircle size={16} className="mt-0.5 shrink-0" />
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={loading}
            className={buttonClasses('primary', 'w-full disabled:opacity-60')}
          >
            {loading ? (
              <Loader2 size={16} className="animate-spin" />
            ) : (
              <ArrowRight size={16} />
            )}
            {loading ? 'Memeriksa...' : 'Masuk'}
          </button>
        </form>

        <p className="mt-6 text-xs leading-5 text-stone-400">
          Belum memiliki akses? Hubungi owner untuk dibuatkan akun. Owner wajib
          mengganti password saat login pertama.
        </p>
      </div>
    </section>
  )
}

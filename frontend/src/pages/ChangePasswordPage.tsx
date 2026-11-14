import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  KeyRound,
  Loader2,
  ShieldCheck,
} from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { changePassword } from '../lib/auth'
import { getApiErrorMessage } from '../lib/apiError'
import { useAuthStore } from '../stores/authStore'

export function ChangePasswordPage() {
  const navigate = useNavigate()
  const user = useAuthStore((state) => state.user)
  const clearSession = useAuthStore((state) => state.clearSession)

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')

    if (newPassword.length < 12) {
      setError('Password baru minimal 12 karakter.')
      return
    }

    if (newPassword !== confirmPassword) {
      setError('Konfirmasi password tidak cocok.')
      return
    }

    if (newPassword === currentPassword) {
      setError('Password baru harus berbeda dari password saat ini.')
      return
    }

    setLoading(true)

    try {
      await changePassword(currentPassword, newPassword)
      sessionStorage.setItem('hanora_password_changed', '1')
      clearSession()
      navigate('/masuk', { replace: true })
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="mx-auto max-w-3xl px-6 py-16">
      <div className="rounded-3xl border border-coffee-100 bg-white p-7 shadow-soft md:p-10">
        {user?.mustChangePassword ? (
          <button
            type="button"
            onClick={() => {
              clearSession()
              navigate('/masuk')
            }}
            className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600 hover:text-coffee-900"
          >
            <ArrowLeft size={14} />
            Kembali ke login
          </button>
        ) : (
          <Link
            to={user?.role === 'Owner' ? '/owner' : '/kasir'}
            className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600 hover:text-coffee-900"
          >
            <ArrowLeft size={14} />
            Kembali ke dashboard
          </Link>
        )}

        <div className="mt-6 flex items-start gap-4">
          <div className="rounded-2xl bg-coffee-100 p-3 text-coffee-800">
            <ShieldCheck size={22} />
          </div>
          <div>
            <h1 className="font-display text-3xl text-coffee-950">
              Ganti password
            </h1>
            <p className="mt-2 text-sm leading-6 text-stone-500">
              {user?.mustChangePassword
                ? 'Akun ini wajib mengganti password sebelum mengakses dashboard.'
                : 'Gunakan password yang panjang dan tidak dipakai di layanan lain.'}
            </p>
          </div>
        </div>

        {user?.mustChangePassword && (
          <div className="mt-6 rounded-2xl border border-amber-100 bg-amber-50 p-4 text-xs leading-5 text-amber-800">
            Setelah password diubah, semua sesi lama dicabut. Anda akan diarahkan
            untuk masuk kembali.
          </div>
        )}

        <form onSubmit={handleSubmit} className="mt-7 space-y-4">
          <label className="block text-xs font-semibold text-coffee-800">
            Password saat ini
            <span className="mt-2 flex items-center gap-2 rounded-xl border border-coffee-200 px-3 py-3 focus-within:border-coffee-600">
              <KeyRound size={16} className="text-stone-400" />
              <input
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                required
                autoComplete="current-password"
                placeholder="Masukkan password saat ini"
                className="w-full bg-transparent text-sm outline-none placeholder:text-stone-400"
              />
            </span>
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Password baru
            <span className="mt-2 flex items-center gap-2 rounded-xl border border-coffee-200 px-3 py-3 focus-within:border-coffee-600">
              <KeyRound size={16} className="text-stone-400" />
              <input
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                required
                minLength={12}
                autoComplete="new-password"
                placeholder="Minimal 12 karakter"
                className="w-full bg-transparent text-sm outline-none placeholder:text-stone-400"
              />
            </span>
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Konfirmasi password baru
            <span className="mt-2 flex items-center gap-2 rounded-xl border border-coffee-200 px-3 py-3 focus-within:border-coffee-600">
              <KeyRound size={16} className="text-stone-400" />
              <input
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                required
                minLength={12}
                autoComplete="new-password"
                placeholder="Ulangi password baru"
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
            {loading ? 'Menyimpan...' : 'Simpan password baru'}
          </button>
        </form>
      </div>
    </section>
  )
}

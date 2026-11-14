import { Loader2 } from 'lucide-react'
import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { bootstrapSession } from '../../lib/auth'
import { useAuthStore } from '../../stores/authStore'

type AuthBootstrapProps = {
  children: ReactNode
}

export function AuthBootstrap({ children }: AuthBootstrapProps) {
  const status = useAuthStore((state) => state.status)

  useEffect(() => {
    void bootstrapSession()
  }, [])

  if (status === 'unknown') {
    return (
      <div className="grid min-h-screen place-items-center bg-coffee-50 px-6">
        <div className="flex flex-col items-center gap-4 text-center">
          <img
            src="/hanora-logo.jpeg"
            alt="Logo HANORA CAFE"
            className="h-16 w-16 rounded-full object-cover shadow-soft"
          />
          <div className="flex items-center gap-2 text-sm font-semibold text-coffee-800">
            <Loader2 size={16} className="animate-spin" />
            Memeriksa sesi...
          </div>
          <p className="text-xs text-stone-500">
            Menyiapkan pengalaman HANORA CAFE.
          </p>
        </div>
      </div>
    )
  }

  return <>{children}</>
}

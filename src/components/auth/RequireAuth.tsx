import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuthStore } from '../../stores/authStore'

type AllowedRole = 'Owner' | 'Cashier'

type RequireAuthProps = {
  children: ReactNode
  roles?: AllowedRole[]
}

export function RequireAuth({ children, roles }: RequireAuthProps) {
  const status = useAuthStore((state) => state.status)
  const user = useAuthStore((state) => state.user)
  const location = useLocation()

  if (status === 'unknown') {
    return null
  }

  if (status === 'anonymous' || !user) {
    const from =
      location.pathname === '/ganti-password' ? undefined : location.pathname

    return (
      <Navigate
        to="/masuk"
        state={from ? { from } : undefined}
        replace
      />
    )
  }

  if (user.mustChangePassword && location.pathname !== '/ganti-password') {
    return <Navigate to="/ganti-password" replace />
  }

  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/403" replace />
  }

  return <>{children}</>
}
